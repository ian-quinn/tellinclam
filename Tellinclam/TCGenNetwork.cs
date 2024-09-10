using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Tellinclam.Algorithms;
using Tellinclam.JSON;

namespace Tellinclam
{
    public class TCGenNetwork : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCGenNetwork()
          : base("System Network Plumbing", "GenNetwork",
            "Get the whole plumbing network based on current zonging plan",
            "Clam", "Basic")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            pManager.AddCurveParameter("Guide line", "Guide",
                "List of Line segments representing trunk network", GH_ParamAccess.list);
            pManager.AddCurveParameter("Space boundary", "Space",
                "List of space boundary (closed Polyline as Curve) as terminals", GH_ParamAccess.list);
            pManager.AddPointParameter("Door location", "Door",
                "List of door location as entry points (which will be paired automatically with each space)", GH_ParamAccess.list);
            pManager.AddTextParameter("Space function tag", "func",
                "Space tag indicating functions for test only", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Space load profile (W)", "load",
                "List of space loads for AHU sizing and further balanced partitioning", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Zoned index", "zone",
                "Nested lists including all space index of each zoning cluster", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Select mode for level-1 network generation", "mode",
                "0: Dijkstra (default)\n1: BCP_k with predefined sources\n>=2: BCP_k with anonymous sources\n-n: same as n but without zoning", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("System network", "netG",
                "The global piping/ducting guidelines", GH_ParamAccess.list);
            pManager.AddLineParameter("System network", "netA",
                "Minimum tree connecting AHU within current floorplan", GH_ParamAccess.tree);
            pManager.AddLineParameter("Critical path", "path", 
                "The maximum path of each system network", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Flow label", "flow",
                "The flow on each edge for testing", GH_ParamAccess.tree);
            pManager.AddLineParameter("Zone network", "netB",
                "Minimum tree connecting terminals within current thermal zone", GH_ParamAccess.tree);
            pManager.AddPointParameter("Equipment Position", "equip",
                "Pre layout of AHU for each thermal zone", GH_ParamAccess.list);
            pManager.AddNumberParameter("Ideal Capacity", "capa",
                "Ideal peak load handled by each terminal equipment (AHU)", GH_ParamAccess.list);
            pManager.AddTextParameter("JSON file", "json",
                "The JSON file for internal information flow", GH_ParamAccess.item);
            pManager.AddTextParameter("Standard JSON format for 3.js graph", "D3.js",
                "The JSON file for G(V,E) with w(v) of each AHU and guidelines", GH_ParamAccess.item);
            pManager.AddPointParameter("Optimization space", "vals",
                "A point list representing optimal or sub-optimal solutions", GH_ParamAccess.list);
            
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double _tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            List<Curve> guideCrvs = new List<Curve>();
            // spaceCrvs does not support MCR regions, each space only has one boundary curve
            List<Curve> spaceCrvs = new List<Curve>();
            List<Point3d> doorPts = new List<Point3d>();
            List<string> funcTags = new List<string>();
            List<GH_Interval> spaceLoads = new List<GH_Interval>();
            GH_Structure<GH_Integer> zoneIds = new GH_Structure<GH_Integer>() { };
            if (!DA.GetDataList(0, guideCrvs) || !DA.GetDataList(1, spaceCrvs) || !DA.GetDataList(2, doorPts) ||
                !DA.GetDataList(3, funcTags) || !DA.GetDataList(4, spaceLoads) || !DA.GetDataTree(5, out zoneIds))
                return;
            if (spaceCrvs.Count != funcTags.Count || spaceCrvs.Count != spaceLoads.Count)
                return;
            int solverMode = 0;
            DA.GetData(6, ref solverMode);

            List<double> heatLoads = spaceLoads.Select(load => load.Value.T0).ToList();
            List<double> coolLoads = spaceLoads.Select(load => load.Value.T1).ToList();

            // initialize ------------------------------------------------------------------------------------
            // force the main guidelines to be Line segments (to prevent some errors)
            List<Line> guides = new List<Line>() { };
            foreach (Curve crv in guideCrvs)
            {
                if (!crv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not valid.");
                    continue;
                }
                if (crv.IsLinear())
                {
                    guides.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
                }
            }

            // -------------------------------------------------------------------------------------
            // prepare the basic info for spaces

            List<double> spaceAreas = new List<double>();
            List<int> shaft_ids = new List<int>();
            // one space may have several entry points (doors or centroids)
            List<List<Point3d>> nested_entryPts = new List<List<Point3d>>();
            List<Point3d> entryPts_shaft = new List<Point3d>();
            for (int i = 0; i < spaceCrvs.Count; i++)
            {
                Curve spaceCrv = spaceCrvs[i];
                if (!spaceCrv.IsValid || spaceCrv is null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not valid.");
                    continue;
                }
                if (!spaceCrv.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not closed.");
                    continue;
                }

                // get the room area
                if (spaceCrv.IsPolyline())
                {
                    if (spaceCrv.TryGetPolyline(out Polyline pline))
                    {
                        spaceAreas.Add(Basic.GetPolyArea(pline.ToList()));
                    }
                }
                else
                {
                    Brep[] allBreps = Brep.CreatePlanarBreps(spaceCrv, _tol);
                    var amp = AreaMassProperties.Compute(allBreps[0]);
                    spaceAreas.Add(amp.Area);
                }

                // prepare the entry point list for this space
                List<Point3d> entryPts = new List<Point3d>();
                // rule-0: for non-distribution space, take door locations as entry points
                // rule-1: for rest space with no entry points, take centroid as entry
                // rule-2: shaft/mech can have entry points
                // rule-3: conditioned space can have entry points

                if (!funcTags[i].Contains("oz:DistributionSpace"))
                {
                    // check if the space boundary intersects with the guideline
                    // if so, take the intersection points as the entry points instead of door locations
                    foreach (Line line in guides)
                    {
                        Curve line_ = new LineCurve(line);
                        var ccx = Intersection.CurveCurve(spaceCrv, line_, _tol, _tol);
                        if (ccx != null & ccx.Count > 0)
                            entryPts.Add(ccx[0].PointA);
                    }
                    // if nothing found, take door locations as entry points
                    if (entryPts.Count == 0)
                    {
                        foreach (Point3d pt in doorPts)
                        {
                            // in future replace this with manhattan distance 
                            if (spaceCrv.ClosestPoint(pt, out double t, _tol))
                                entryPts.Add(pt);
                        }
                    }
                }
                // for space not for distribution, or has no door loc paired
                // take the centroid as the entry point
                if (entryPts.Count == 0)
                {
                    if (spaceCrv.IsPolyline())
                        if (spaceCrv.TryGetPolyline(out Polyline pline))
                        {
                            Point3d centroid = pline.CenterPoint();
                            entryPts.Add(centroid);
                        }
                }

                // whether to add the entry points to the space?
                // if conditioned, yes, if shaft/mech, yes
                if (heatLoads[i] > 0 || coolLoads[i] > 0)
                    nested_entryPts.Add(entryPts);
                else
                    nested_entryPts.Add(new List<Point3d> ());
                // additionally, record the source node for net-A
                if (funcTags[i].Contains("oz:ServiceShaft") ||
                    funcTags[i].Contains("oz:MechanicalRoom"))
                    entryPts_shaft.AddRange(entryPts);
            }

            // nested_spaceIds -> a zoning scheme 
            // spaceIds -> one zone (contains id of the spaces within the zone)
            // spaceId -> the id of the space
            List<List<int>> nested_spaceIds = new List<List<int>>();
            // in debug mode (solverMode < 0), each space apart from the shaft/mech forms a zone
            if (solverMode < 0)
            {
                for (int i = 0; i < spaceAreas.Count; i++)
                {
                    if (!shaft_ids.Contains(i))
                    {
                        nested_spaceIds.Add(new List<int>() { i });
                    }
                }
                solverMode = -solverMode; // dispatch to the normal partitions
            }
            else
            {
                foreach (var branch in zoneIds.Branches)
                {
                    nested_spaceIds.Add(branch.Select(s => s.Value).ToList());
                }
            }


            // -------------------------------------------------------------------------------------
            // outputs 3-level networks
            // network_G the complete graph for all entry points connected to the guidelines
            // network_A the system level pipes/ducts connecting shaft to AHU
            // network_B the zone level ducts connecting AHU to space entry points for further distributions

            // generation of network-B, zone level
            // zoneForest -> only one scheme of zone level connections, one zone, one tree

            List<List<Line>> zoneForest = new List<List<Line>>();
            List<PathFinding.Graph<int>> zoneGraphs = new List<PathFinding.Graph<int>>();
            // in this AHUs, different types of systems will go dispatch
            List<Point3d> AHUs = new List<Point3d>();
            List<double> zoneLoads = new List<double>();
            foreach (List<int> spaceIds in nested_spaceIds)
            {
                // zone level, has multiple spaces
                // populate a list that flattens all entry points of spaces within current zone
                List<List<Point3d>> entryPts_inzone = spaceIds.ConvertAll(id => nested_entryPts[id]);

                // get all possible connections for all terminal candidates (jumpWires)
                // jump wires are for temporal connections :)
                List<Line> zoneGuides = PathFinding.GetTerminalConnection(guides, 
                    Util.FlattenList(entryPts_inzone), out List<Line> jumpwires);
                // when Manhattan distance entryPts_zone applied, the output cons will be a list of polylines!

                // if a zone has only one space, just connect the entry point to the guideline
                // however, connection with the same length may have greater distribution cost
                //
                //      space_1                        ────┬──────┬───
                // ──┬────────┬────                   ┌────┼────┐ |
                //   | con_1  | con_2                 |    ●────┼─┤
                //   └────────┴───────> source        └─────────┘ |
                //  space has 2 doors                not cover this situation?
                
                if (spaceIds.Count == 1)
                {
                    // select the shorter one from cons
                    Line minJumpwire = jumpwires.OrderByDescending(l => l.Length).LastOrDefault();
                    // or you need to select the minimum path from this entry to the source
                    // or the sum(graph.edges) reaches minimum after leaf trimming

                    zoneForest.Add(new List<Line>() { }); // this zone has no tree
                    // take the midpoint of line:(entry point, guideline) as location of AHU
                    // Point3d AHU = cons[min_idx].PointAt(1) - 0.5 * cons[min_idx].Direction / cons[min_idx].Length;
                    // or take the entry point as the location of AHU?
                    Point3d AHU = minJumpwire.PointAt(0);
                    AHUs.Add(AHU);
                    zoneLoads.Add(coolLoads[spaceIds[0]]);
                    // ducting at zone level only has distributions within this single space
                    // presume it has fixed length, representing the total ducting length in this space
                    // calculate the length by 4 * perimeter / area
                    PathFinding.Graph<int> zoneGraph = new PathFinding.Graph<int>(true);
                    PathFinding.Node<int> root = zoneGraph.AddNode(0, 0);
                    zoneGraph.Nodes.Last().Coords = AHU;
                    zoneGraph.Nodes.Last().isRoot = true;
                    PathFinding.Node<int> terminal = zoneGraph.AddNode(1, 0);
                    zoneGraph.Nodes.Last().Coords = AHU;
                    double diameter = 4 * spaceAreas[spaceIds[0]] / spaceCrvs[spaceIds[0]].GetLength();
                    zoneGraph.AddEdge(root, terminal, diameter);
                    zoneGraphs.Add(zoneGraph);
                }
                // if a zone has several spaces, find the centroid of this MST then connect it to the guideline
                // the MST aggregates spaces with the minimum length cost, so it removes the redundant entry points of space
                else
                {
                    // try to get the sub graph of each possible composition
                    List<List<Point3d>> terminal_combinations = Util.GetCombinations<Point3d>(entryPts_inzone);
                    double min_length = double.PositiveInfinity;
                    int combination_id = 0;
                    List<Line> min_subgraph = new List<Line>();
                    foreach (List<Point3d> terminal_combination in terminal_combinations)
                    {
                        List<Line> candidate_subgraph = PathFinding.GetSubGraph(zoneGuides, 
                            terminal_combination, out double sum_length);
                        if (sum_length < min_length)
                        {
                            min_length = sum_length;
                            min_subgraph = candidate_subgraph;
                            combination_id = terminal_combinations.IndexOf(terminal_combination);
                        }
                    }
                    //List<Line> zone_edge_subgraph = Algorithms.PathFinding.GetSubGraph(zone_edge_complete, space_pts);

                    // minimum spanning tree applied on edges_subgraph
                    List<Line> zoneTree = Algorithms.PathFinding.GetSteinerTree(min_subgraph, 
                        terminal_combinations[combination_id], new List<Point3d>() { }, PathFinding.algoEnum.MST);
                    zoneForest.Add(zoneTree); // only for visualization

                    PathFinding.Graph<int> zoneGraph = PathFinding.RebuildGraph(zoneTree);
                    Point3d AHU = PathFinding.GetPseudoRootOfGraph(zoneGraph);
                    AHUs.Add(AHU); // only for visualization
                    zoneLoads.Add(coolLoads.Where((load, id) => spaceIds.Contains(id)).Sum());

                    zoneGraph.Graft();
                    zoneGraphs.Add(zoneGraph); // for JSON serialization
                }
            }

            // -------------------------------------------------------------------------------------

            // generation of network-A, system level
            // DISPATCH-1: Dijkstra shortest path -> only one solution for AHU connections
            // DISPATCH-2: BCP -> several solutions with optimum for AHU connections
            // PENDING FOR UPDATE: system zoning needs restrictions on operation schedule and load difference

            // naming conventions:
            // sysForests   -> contains multiple solutions of the BCP problem, if Dijkstra, sysForests.Count = 1
            // └ sysForest  -> each forest is one solution, containing all edges of network-A
            //  ├ sysTree   -> each tree is one distribution system, from shaft/mech to AHU terminals
            //  └ sysTrunk  -> the critical path of a distribution system (a tree)
            // sysGraphs    -> contains graphs ready for serialization

            List<List<List<Line>>> sysForests = new List<List<List<Line>>>() { };
            List<List<List<Line>>> sysForests_trunk = new List<List<List<Line>>>();
            List<PathFinding.Graph<int>> sysGraphs = new List<PathFinding.Graph<int>>() { };
            // generate system zones based on grouped zones
            // in this step, all possible candidate points become valid ones
            List<Line> sysGuidelines = PathFinding.GetTerminalConnection(guides, 
                Util.ConcateLists(AHUs, entryPts_shaft), out List<Line> _);

            // in this graph, try to find which shaft is the nearest for which AHU
            PathFinding.Graph<int> sysCompleteGraph = PathFinding.RebuildGraph(sysGuidelines);
            List<PathFinding.Node<int>> shaft_nodes = new List<PathFinding.Node<int>>() { };
            List<int> source_ids = new List<int>();
            List<List<List<double>>> sysForests_flow = new List<List<List<double>>>();
            List<Point3d> optSpace = new List<Point3d>();

            foreach (PathFinding.Node<int> node in sysCompleteGraph.Nodes)
            {
                // 20240425 somehow this RebuildGraph() process may lead to self-pointing
                // no time to debug it I just remove the node itself from its neighbors
                if (node.Neighbors.Contains(node))
                    node.RemoveNeighbors(node);
                foreach (Point3d pt in entryPts_shaft)
                {
                    if (pt.DistanceTo(node.Coords) < _tol)
                    {
                        shaft_nodes.Add(node);
                        source_ids.Add(node.Index);
                    }
                }
                for (int i = 0; i < AHUs.Count; i++)
                {
                    if (AHUs[i].DistanceTo(node.Coords) < _tol)
                    {
                        node.Weight = zoneLoads[i];
                    }
                }
            }

            // DISPATCH-1 AHU organized by Dijkstra shortest path
            // there is only one solution thus we have sysForests.Count = 1
            sysForests.Add(new List<List<Line>> { });
            if (solverMode == 0)
            {
                // by default, sysZones.Count = entryPts_shaft.Count
                // allocate each AHU to its nearest shaft/mech entry point
                List<List<Point3d>> sysZones = new List<List<Point3d>>() { };
                for (int i = 0; i < entryPts_shaft.Count; i++)
                    sysZones.Add(new List<Point3d>() { });

                foreach (Point3d pt in AHUs)
                {
                    // locate the terminal point
                    foreach (PathFinding.Node<int> node in sysCompleteGraph.Nodes)
                    {
                        if (pt.DistanceTo(node.Coords) < _tol) // valid node
                        {
                            double min_dist = double.PositiveInfinity;
                            int min_id = 0;
                            for (int i = 0; i < shaft_nodes.Count; i++)
                            {
                                sysCompleteGraph.GetShortestPathDijkstra(node, shaft_nodes[i], out double distance);
                                if (distance < min_dist)
                                {
                                    min_dist = distance;
                                    min_id = i;
                                }
                            }
                            sysZones[min_id].Add(pt);
                        }
                    }
                }

                for (int i = 0; i < sysZones.Count; i++)
                {
                    // critical step in this process is to transform the steiner tree problem to spanning tree problem
                    // by joining the relay node inside the graph
                    List<Line> sysSubGuides = PathFinding.GetSubGraph(sysGuidelines,
                        Util.ConcateLists(sysZones[i], new List<Point3d>() { entryPts_shaft[i] }), out _);
                    List<Line> sysTree = PathFinding.GetSteinerTree(sysSubGuides, sysZones[i],
                        new List<Point3d>() { entryPts_shaft[i] }, PathFinding.algoEnum.SPT);
                    // by Dijkstra, each forest 
                    sysForests[0].Add(sysTree);
                }
            }
            // DISPATCH-2 
            // Mixed Integer Programming for the BCP problems (Balanced Connected graph Partition)
            else
            {
                var BCP = new List<List<List<Tuple<int, int>>>>();
                var optVals = new List<double[]>();
                if (solverMode == 1)
                    IntegerPrograms.BalancedConnectedPartition(sysCompleteGraph, source_ids.Count, source_ids, 
                        out BCP, out sysForests_flow, out optVals);
                else
                    // when > 1, solverMode equals to the number of partitions anticipated
                    IntegerPrograms.BalancedConnectedPartition(sysCompleteGraph, solverMode, new List<int>() { }, 
                        out BCP, out sysForests_flow, out optVals);
                foreach (List<List<Tuple<int ,int>>> solution in BCP)
                {
                    var sysForest = new List<List<Line>>();
                    var sysForest_trunk = new List<List<Line>>();

                    foreach (List<Tuple<int, int>> partition in solution)
                    {
                        List<Line> sys_tree = new List<Line>();
                        List<Line> sys_trunk = new List<Line>();
                        foreach (Tuple<int, int> connection in partition)
                        {
                            // from IntegerPrograms.cs, connections may have phantom sources with
                            // .Item1 outside the Nodes list
                            // by default, Nodes are indexed from 0 to Nodes.Count
                            // the source will be offset ↙ a bit to its entry point in the network
                            if (connection.Item1 >= sysCompleteGraph.Nodes.Count)
                                sys_tree.Add(new Line(
                                sysCompleteGraph.Nodes[connection.Item2].Coords - new Vector3d(-1, -1, 0),
                                sysCompleteGraph.Nodes[connection.Item2].Coords));
                            else
                                sys_tree.Add(new Line(
                                sysCompleteGraph.Nodes[connection.Item1].Coords,
                                sysCompleteGraph.Nodes[connection.Item2].Coords));
                        }

                        // 20240509 temporary code to calculate the longest path in this tree
                        // the arc with the maximum flow must point to the source node
                        Point3d this_src_point = sys_tree[0].PointAt(1);
                        PathFinding.Graph<int> this_tree = PathFinding.RebuildGraph(sys_tree);
                        var this_src_node = this_tree.Nodes[0];
                        foreach (PathFinding.Node<int> junc in this_tree.Nodes)
                        {
                            if (junc.Coords.DistanceTo(this_src_point) < _tol)
                                this_src_node = junc;
                        }
                        List<PathFinding.Edge<int>> this_path = this_tree.GetFurthestPathDijkstra(this_src_node, out _);
                        foreach (var step in this_path)
                        {
                            sys_trunk.Add(new Line(step.From.Coords, step.To.Coords));
                        }
                        sysForest.Add(sys_tree);
                        sysForest_trunk.Add(sys_trunk);
                    }
                    sysForests.Add(sysForest);
                    sysForests_trunk.Add(sysForest_trunk);
                }
                foreach (double[] optVal in optVals)
                {
                    if (optVal.Length == 1)
                        optSpace.Add(new Point3d(optVal[0], 0, 0));
                    else if (optVal.Length == 2)
                        optSpace.Add(new Point3d(optVal[0], optVal[1], 0));
                    else if (optVal.Length == 3)
                        optSpace.Add(new Point3d(optVal[0], optVal[1], optVal[2]));
                }
            }

            // there can only be one solutions for JSON serialization
            // take the first solution, for Dijkstra it is the only one, for BCP, it is the optimum
            foreach (List<Line> sysTree in sysForests[0])
            {
                PathFinding.Graph<int> sysGraph = PathFinding.RebuildGraph(sysTree);
                // how rookie finds the root/terminal node by comparing coordinates
                foreach (Point3d pt in entryPts_shaft)
                {
                    foreach (PathFinding.Node<int> junc in sysGraph.Nodes)
                    {
                        if (junc.Coords.DistanceTo(pt) < _tol)
                            junc.isRoot = true;
                    }
                }
                sysGraph.Graft();
                sysGraphs.Add(sysGraph);
            }

            // 20240415 Temperal code for testing -------------------------------------------------------------------------------
            // generate a sample graph including all space entry points and loads for partitioning
            // parse this graph into D3.js format // the data model of PathFinding.Graph needs update
            List<Node> jsonNodes = new List<Node>() { };
            List<Link> jsonLinks = new List<Link>() { };

            foreach (PathFinding.Node<int> node in sysCompleteGraph.Nodes)
            {
                jsonNodes.Add(new Node
                {
                    id = node.Value,
                    weight = 0.0
                });
            }
            // batch edges
            foreach (PathFinding.Edge<int> edge in sysCompleteGraph.GetEdges())
            {
                // 20240425 GetEdges() returns bi-directional edges
                // for undirected graph this should be one edge with any source/target setup
                jsonLinks.Add(new Link
                {
                    source = edge.From.Value,
                    target = edge.To.Value,
                    weight = edge.Weight
                });
            }
            var d3Graph = new Graph { nodes = jsonNodes, links = jsonLinks };
            string d3JSON = JsonSerializer.Serialize(d3Graph, new JsonSerializerOptions { WriteIndented = true });

            // -------------------------------------------------------------------------------------------------------

            List<Point3d> entryPts_sample = nested_entryPts
                .Where(sublist => sublist.Any()).Select(sublist => sublist.First()).ToList();
            List<Line> networkG = PathFinding.GetTerminalConnection(guides, entryPts_sample, out List<Line> _);

            // -------------------------------------------------------------------------------------------------------

            DA.SetDataList(0, networkG);
            DA.SetDataTree(1, Util.ListToTree(sysForests));
            DA.SetDataTree(2, Util.ListToTree(sysForests_trunk));
            DA.SetDataTree(3, Util.ListToTree(sysForests_flow));
            DA.SetDataTree(4, Util.ListToTree(zoneForest));
            DA.SetDataList(5, AHUs);
            DA.SetDataList(6, zoneLoads);
            
            // pairing each system network and the zones it Zcontrols
            string sysJSON = SerializeJSON.InitiateSystem(sysGraphs, zoneGraphs,
                funcTags, nested_spaceIds, nested_entryPts, AHUs, spaceAreas, true);
            DA.SetData(7, sysJSON);
            DA.SetData(8, d3JSON);
            DA.SetDataList(9, optSpace);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.network;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("7BFF2D53-6E46-41E9-A03B-BCA0473607B2");
    }
}