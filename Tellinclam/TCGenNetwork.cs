using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;
using System.Diagnostics;
using Tellinclam.Serialization;
using Rhino.Render.DataSources;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using System.Security.Cryptography;
using System.Collections;
using System.IO;
using static Tellinclam.Serialization.SchemaJSON;
using System.Text.Json;
using Rhino.Geometry.Intersect;
using System.Diagnostics.Eventing.Reader;

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

            pManager.AddCurveParameter("Lines", "Lines",
                "List of Line segments representing trunk network", GH_ParamAccess.list);
            pManager.AddCurveParameter("Boundaries", "Space",
                "List of space boundary (closed Polyline as Curve) as terminals", GH_ParamAccess.list);
            pManager.AddPointParameter("Door locations", "Door",
                "List of door location as entry points (which will be paired automatically with each space)", GH_ParamAccess.list);
            pManager.AddTextParameter("Function Tag", "func",
                "Space tag indicating functions for test only", GH_ParamAccess.list);
            pManager.AddNumberParameter("Space load profile", "loads",
                "List of space loads for AHU sizing and further balanced partitioning", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Zoned index", "zones",
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
            List<Curve> spaceCrvs = new List<Curve>();
            List<Point3d> doorPts = new List<Point3d>();
            List<string> tags = new List<string>();
            List<double> spaceLoads = new List<double>();
            GH_Structure<GH_Integer> zoneIds = new GH_Structure<GH_Integer>() { };
            if (!DA.GetDataList(0, guideCrvs) || !DA.GetDataList(1, spaceCrvs) || !DA.GetDataList(2, doorPts) ||
                !DA.GetDataList(3, tags) || !DA.GetDataList(4, spaceLoads) || !DA.GetDataTree(5, out zoneIds))
            {
                return;
            }
            int netGenMode = 0;
            DA.GetData(6, ref netGenMode);

            // initialize ------------------------------------------------------------------------------------
            // force the main guidelines to be Line segments (to prevent some errors)
            List<Line> edges = new List<Line>() { };
            foreach (Curve crv in guideCrvs)
            {
                if (!crv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not valid.");
                    continue;
                }
                if (crv.IsLinear())
                {
                    edges.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
                }
            }

            List<double> areas = new List<double>() { };
            List<int> shaft_ids = new List<int>();
            // this is a data tree
            // one space may have several entry points
            List<List<Point3d>> nested_entry_pts = new List<List<Point3d>>() { };
            List<Point3d> shaft_entry_pts = new List<Point3d>() { };
            foreach (Curve spaceCrv in spaceCrvs)
            {
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

                // get the room area -----------------------------------------------------------------
                if (spaceCrv.IsPolyline())
                {
                    if (spaceCrv.TryGetPolyline(out Polyline pline))
                    {
                        areas.Add(Basic.GetPolyArea(pline.ToList()));
                    }
                }
                else
                {
                    Brep[] allBreps = Brep.CreatePlanarBreps(spaceCrv, _tol);
                    var amp = AreaMassProperties.Compute(allBreps[0]);
                    areas.Add(amp.Area);
                }

                // prepare the entry point list for this space ----------------------------------------
                List<Point3d> entry_pts = new List<Point3d>() { };
                int entry_counter = 0;
                // if the space is on level 0, assign the door location point to it
                // if not, it acts like a circulation area and should be excluded
                if (tags[spaceCrvs.IndexOf(spaceCrv)] == "Lv0")
                {
                    // check if the space boundary intersects with the guideline
                    // if so, take the intersection points as the entry points instead of door locations
                    foreach (Line edge in edges)
                    {
                        Curve edge_crv = new LineCurve(edge);
                        var ccx = Intersection.CurveCurve(spaceCrv, edge_crv, _tol, _tol);
                        if (ccx != null & ccx.Count > 0)
                        {
                            entry_pts.Add(ccx[0].PointA);
                            entry_counter++;
                        }
                    }
                    // if nothing found, take door locations as entry points
                    if (entry_counter == 0)
                    {
                        foreach (Point3d pt in doorPts)
                        {
                            double t;
                            // in future replace this with manhattan distance 
                            if (spaceCrv.ClosestPoint(pt, out t, _tol))
                            {
                                entry_pts.Add(pt);
                                entry_counter++;
                            }
                        }
                    }
                }

                // if there is no door accessing this space (open area), take centroid instead
                // this applies to both functional spaces and shafts
                if (entry_counter == 0)
                {
                    if (spaceCrv.IsPolyline())
                        if (spaceCrv.TryGetPolyline(out Polyline pline))
                        {
                            Point3d centroid = pline.CenterPoint();
                            entry_pts.Add(centroid);
                        }
                }

                if (tags[spaceCrvs.IndexOf(spaceCrv)] == "LvS")
                    shaft_entry_pts.AddRange(entry_pts);

                nested_entry_pts.Add(entry_pts);
            }

            // parse the input (the input is a nested integer list indicating a zoning plan)
            // each nested list contains the space id within the same thermal zone
            List<List<int>> nested_ids = new List<List<int>>() { };
            // in debug mode (netGenMode < 0), each space apart from the shaft/mech forms a zone
            if (netGenMode < 0)
            {
                for (int i = 0; i < areas.Count; i++)
                {
                    if (!shaft_ids.Contains(i))
                    {
                        nested_ids.Add(new List<int>() { i });
                    }
                }
                netGenMode = -netGenMode; // dispatch to the normal partitions
            }
            else
            {
                foreach (var branch in zoneIds.Branches)
                {
                    nested_ids.Add(branch.Select(s => s.Value).ToList());
                }
            }


            // -------------------------------------------------------------------------------------


            // generation of network-B, zone level
            List<List<Line>> zone_networks = new List<List<Line>>() { };
            List<PathFinding.Graph<int>> zone_graphs = new List<PathFinding.Graph<int>>() { };
            // in this AHUs, different types of systems will go dispatch
            List<Point3d> AHUs = new List<Point3d>() { };
            List<double> zoneLoads = new List<double>();
            for (int i = 0; i < nested_ids.Count; i++)
            {
                // zone level, has multiple spaces
                // this is a flatten list for all terminal candidates
                List<List<Point3d>> zoned_entry_pts = new List<List<Point3d>>() { };
                foreach (int idx in nested_ids[i])
                {
                    // space level, has multiple entries
                    List<Point3d> space_pts = nested_entry_pts[idx];
                    zoned_entry_pts.Add(space_pts);
                }
                // get all possible connections for all terminal candidates
                List<Line> zone_edge_complete = PathFinding.GetTerminalConnection(edges, 
                    Util.FlattenList(zoned_entry_pts), out List<Line> cons);
                // when Manhattan distance is applied, the output cons will be a list of polylines!

                // if a zone has only one space, just connect the entry point to the guideline
                // however, connection with the same length may have greater distribution cost
                //
                //      space_1                        ────┬──────┬───
                // ──┬────────┬────                   ┌────┼────┐ |
                //   | con_1  | con_2                 |    ●────┼─┤
                //   └────────┴───────> source        └─────────┘ |
                //  space has 2 doors                not cover this situation?
                //
                if (nested_ids[i].Count == 1)
                {
                    // select the shorter one from cons
                    Line con_min = cons.OrderByDescending(l => l.Length).LastOrDefault();
                    // or you need to select the minimum path from this entry to the source
                    // or the sum(graph.edges) reaches minimum after leaf trimming

                    zone_networks.Add(new List<Line>() { con_min }); // only for visualization
                    // take the midpoint of line:(entry point, guideline) as location of AHU
                    // Point3d AHU = cons[min_idx].PointAt(1) - 0.5 * cons[min_idx].Direction / cons[min_idx].Length;
                    // or take the entry point as the location of AHU?
                    Point3d AHU = con_min.PointAt(0);
                    AHUs.Add(AHU);
                    zoneLoads.Add(spaceLoads[nested_ids[i][0]]);

                    PathFinding.Graph<int> graph = new PathFinding.Graph<int>(true);
                    PathFinding.Node<int> root = graph.AddNode(0, 0);
                    graph.Nodes.Last().Coords = AHU;
                    graph.Nodes.Last().isRoot = true;
                    PathFinding.Node<int> terminal = graph.AddNode(1, 0);
                    graph.Nodes.Last().Coords = cons[0].PointAt(0);
                    graph.AddEdge(root, terminal, cons[0].Length);
                    zone_graphs.Add(graph);
                }
                // if a zone has several spaces, find the centroid of this MST then connect it to the guideline
                // the MST aggregates spaces with the minimum length cost, so it removes the redundant entry points of space
                else
                {
                    // try to get the sub graph of each possible composition
                    List<List<Point3d>> terminal_combinations = Util.GetCombinations<Point3d>(zoned_entry_pts);
                    double min_length = double.PositiveInfinity;
                    int combination_id = 0;
                    List<Line> min_subgraph = new List<Line>() { };
                    foreach (List<Point3d> terminal_combination in terminal_combinations)
                    {
                        List<Line> candidate_subgraph = PathFinding.GetSubGraph(zone_edge_complete, terminal_combination, out double sum_length);
                        if (sum_length < min_length)
                        {
                            min_length = sum_length;
                            min_subgraph = candidate_subgraph;
                            combination_id = terminal_combinations.IndexOf(terminal_combination);
                        }
                    }
                    //List<Line> zone_edge_subgraph = Algorithms.PathFinding.GetSubGraph(zone_edge_complete, space_pts);

                    // minimum spanning tree applied on edges_subgraph
                    List<Line> zone_network = Algorithms.PathFinding.GetSteinerTree(
                        min_subgraph, terminal_combinations[combination_id], new List<Point3d>() { }, PathFinding.algoEnum.MST);
                    zone_networks.Add(zone_network); // only for visualization

                    PathFinding.Graph<int> zone_graph = PathFinding.RebuildGraph(zone_network);
                    Point3d AHU = PathFinding.GetPseudoRootOfGraph(zone_graph);
                    AHUs.Add(AHU); // only for visualization
                    zoneLoads.Add(spaceLoads.Where((load, id) => nested_ids[i].Contains(id)).Sum());

                    zone_graph.Graft();
                    zone_graphs.Add(zone_graph); // for JSON serialization
                }
            }

            // generation of network-A, system level
            // try to find the economic shaft point for the entire graph
            // upgrade problem: find X trees from a graph that minimize the average length and depth
            // PENDING FOR UPDATE
            // for now, assuming each AHU will go to the nearest shaft
            // however, you should consider the total conditioning volume and make it even (as possible)
            List<List<List<Line>>> sys_forests = new List<List<List<Line>>>() { };
            List<List<List<Line>>> sys_forests_trunk = new List<List<List<Line>>>();
            // generate system zones based on grouped zones
            List<PathFinding.Graph<int>> sys_graphs = new List<PathFinding.Graph<int>>() { };

            // in this step, all possible candidate points become valid ones
            List<Line> sys_edge_complete = PathFinding.GetTerminalConnection(edges, 
                Util.ConcateLists(AHUs, shaft_entry_pts), out List<Line> _);

            // in this graph, try to find which shaft is the nearest for which AHU
            PathFinding.Graph<int> sys_whole_graph = PathFinding.RebuildGraph(sys_edge_complete);
            List<PathFinding.Node<int>> shaft_nodes = new List<PathFinding.Node<int>>() { };
            List<int> source_ids = new List<int>();
            List<List<List<double>>> sys_flows = new List<List<List<double>>>();
            List<Point3d> optSpace = new List<Point3d>();

            foreach (PathFinding.Node<int> node in sys_whole_graph.Nodes)
            {
                // 20240425 somehow this RebuildGraph() process may lead to self-pointing
                // no time to debug it I just remove the node itself from its neighbors
                if (node.Neighbors.Contains(node))
                    node.RemoveNeighbors(node);
                foreach (Point3d pt in shaft_entry_pts)
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

            // DISPATCH-1 Dijkstra organize AHU by shortest path to shafts
            if (netGenMode == 0)
            {
                List<List<Point3d>> sys_zones = new List<List<Point3d>>() { };
                // group all points in AHUs by their nearest shaft points
                for (int i = 0; i < shaft_entry_pts.Count; i++)
                    sys_zones.Add(new List<Point3d>() { });

                foreach (Point3d pt in AHUs)
                {
                    // locate the terminal point
                    foreach (PathFinding.Node<int> node in sys_whole_graph.Nodes)
                    {
                        if (pt.DistanceTo(node.Coords) < _tol) // valid node
                        {
                            double min_dist = double.PositiveInfinity;
                            int min_id = 0;
                            for (int i = 0; i < shaft_nodes.Count; i++)
                            {
                                sys_whole_graph.GetShortestPathDijkstra(node, shaft_nodes[i], out double distance);
                                if (distance < min_dist)
                                {
                                    min_dist = distance;
                                    min_id = i;
                                }
                            }
                            sys_zones[min_id].Add(pt);
                        }
                    }
                }

                for (int i = 0; i < sys_zones.Count; i++)
                {
                    // critical step in this process is to transform the steiner tree problem to spanning tree problem
                    // by joining the relay node inside the graph
                    List<Line> sys_edge_subgraph = PathFinding.GetSubGraph(sys_edge_complete,
                        Util.ConcateLists(sys_zones[i], new List<Point3d>() { shaft_entry_pts[i] }), out _);
                    List<Line> sys_network = PathFinding.GetSteinerTree(sys_edge_subgraph, sys_zones[i],
                        new List<Point3d>() { shaft_entry_pts[i] }, PathFinding.algoEnum.SPT);
                    sys_forests.Add(new List<List<Line>>() { sys_network });
                }
            }
            // MIP for BCP problems
            else
            {
                var BCP = new List<List<List<Tuple<int, int>>>>();
                var optVals = new List<double[]>();
                if (netGenMode == 1)
                    IntegerPrograms.BalancedConnectedPartition(sys_whole_graph, source_ids.Count, source_ids, out BCP, out sys_flows, out optVals);
                else
                    IntegerPrograms.BalancedConnectedPartition(sys_whole_graph, netGenMode, new List<int>() { }, out BCP, out sys_flows, out optVals);
                foreach (List<List<Tuple<int ,int>>> solution in BCP)
                {
                    var sys_forest = new List<List<Line>>();
                    var sys_forest_trunk = new List<List<Line>>();

                    foreach (List<Tuple<int, int>> partition in solution)
                    {
                        List<Line> sys_tree = new List<Line>();
                        List<Line> sys_trunk = new List<Line>();
                        foreach (Tuple<int, int> connection in partition)
                        {
                            // from IntegerPrograms.cs, connections may have phantom sources with .Item1 outside the Nodes list
                            // by default, Nodes are indexed from 0 to Nodes.Count
                            // the source will be offset ↙ a bit to its entry point in the network
                            if (connection.Item1 >= sys_whole_graph.Nodes.Count)
                                sys_tree.Add(new Line(
                                sys_whole_graph.Nodes[connection.Item2].Coords - new Vector3d(-1, -1, 0),
                                sys_whole_graph.Nodes[connection.Item2].Coords));
                            else
                                sys_tree.Add(new Line(
                                sys_whole_graph.Nodes[connection.Item1].Coords,
                                sys_whole_graph.Nodes[connection.Item2].Coords));
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
                        sys_forest.Add(sys_tree);
                        sys_forest_trunk.Add(sys_trunk);
                    }
                    sys_forests.Add(sys_forest);
                    sys_forests_trunk.Add(sys_forest_trunk);
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
            // batch from sys_networks to sys_graphs
            // only take the optimum solution forest
            foreach (List<Line> sys_tree in sys_forests[0])
            {
                PathFinding.Graph<int> sys_graph = PathFinding.RebuildGraph(sys_tree);
                // how rookie finds the root/terminal node by comparing coordinates
                foreach (Point3d shaft_entry_pt in shaft_entry_pts)
                {
                    foreach (PathFinding.Node<int> junc in sys_graph.Nodes)
                    {
                        if (junc.Coords.DistanceTo(shaft_entry_pt) < _tol)
                            junc.isRoot = true;
                    }
                }
                sys_graph.Graft();
                sys_graphs.Add(sys_graph);
            }

            // 20240415 Temperal code for testing -------------------------------------------------------------------------------
            // generate a sample graph including all space entry points and loads for partitioning
            // parse this graph into D3.js format // the data model of PathFinding.Graph needs update
            List<SchemaJSON.node> jsonNodes = new List<SchemaJSON.node>() { };
            List<SchemaJSON.link> jsonLinks = new List<SchemaJSON.link>() { };

            foreach (PathFinding.Node<int> node in sys_whole_graph.Nodes)
            {
                jsonNodes.Add(new SchemaJSON.node
                {
                    id = node.Value,
                    weight = 0.0
                });
            }
            // batch edges
            foreach (PathFinding.Edge<int> edge in sys_whole_graph.GetEdges())
            {
                // 20240425 GetEdges() returns bi-directional edges
                // for undirected graph this should be one edge with any source/target setup
                jsonLinks.Add(new SchemaJSON.link
                {
                    source = edge.From.Value,
                    target = edge.To.Value,
                    weight = edge.Weight
                });
            }
            var d3Graph = new SchemaJSON.graph { nodes = jsonNodes, links = jsonLinks };
            string d3JSON = JsonSerializer.Serialize(d3Graph, new JsonSerializerOptions { WriteIndented = true });

            // -------------------------------------------------------------------------------------------------------

            List<Point3d> space_zone_entry_pts = new List<Point3d>() { };
            foreach (List<Point3d> pts in nested_entry_pts)
            {
                space_zone_entry_pts.Add(pts[0]);
            }
            List<Line> guidelines = PathFinding.GetTerminalConnection(edges,
                    space_zone_entry_pts, out List<Line> sub_guidelines);

            // -------------------------------------------------------------------------------------------------------

            DA.SetDataList(0, guidelines);
            DA.SetDataTree(1, Util.ListToTree(sys_forests));
            DA.SetDataTree(2, Util.ListToTree(sys_forests_trunk));
            DA.SetDataTree(3, Util.ListToTree(sys_flows));
            DA.SetDataTree(4, Util.ListToTree(zone_networks));
            DA.SetDataList(5, AHUs);
            DA.SetDataList(6, zoneLoads);
            
            // pairing each system network and the zones it Zcontrols
            string sysJSON = SerializeJSON.InitiateSystem(sys_graphs, zone_graphs, nested_ids, nested_entry_pts, AHUs, areas, true);
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