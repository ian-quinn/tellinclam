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
                "List of door location as entry points (which will be paired automatically with each room)", GH_ParamAccess.list);
            pManager.AddTextParameter("Function Tag", "func",
                "Room tag indicating functions for test only", GH_ParamAccess.list);
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
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
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
            // convert the main conduit chases to Line
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
            // one room may have several entry points
            List<List<Point3d>> nested_entry_pts = new List<List<Point3d>>() { };
            List<Point3d> shaft_entry_pts = new List<Point3d>() { };
            foreach (Curve room_crv in spaceCrvs)
            {
                if (!room_crv.IsValid || room_crv is null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not valid.");
                    continue;
                }
                if (!room_crv.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not closed.");
                    continue;
                }

                // get the room area
                if (room_crv.IsPolyline())
                {
                    if (room_crv.TryGetPolyline(out Polyline pline))
                    {
                        areas.Add(Basic.GetPolyArea(pline.ToList()));
                    }
                }
                else
                {
                    Brep[] allBreps = Brep.CreatePlanarBreps(room_crv);
                    var amp = AreaMassProperties.Compute(allBreps[0]);
                    areas.Add(amp.Area);
                }

                // prepare the entry point list for this room
                List<Point3d> entry_pts = new List<Point3d>() { };
                int entry_counter = 0;

                // if the room is on the last level, assign door locations to it
                // if not, it acts like a circulation area and should be excluded
                if (tags[spaceCrvs.IndexOf(room_crv)] == "Lv0")
                {
                    foreach (Point3d pt in doorPts)
                    {
                        double t;
                        if (room_crv.ClosestPoint(pt, out t, 0.001))
                        {
                            entry_pts.Add(pt);
                            entry_counter++;
                        }
                    }
                }
                
                // if there is no door accessing this room
                if (entry_counter == 0)
                {
                    if (room_crv.IsPolyline())
                        if (room_crv.TryGetPolyline(out Polyline pline))
                        {
                            Point3d centroid = pline.CenterPoint();
                            entry_pts.Add(centroid);
                        }
                }

                if (tags[spaceCrvs.IndexOf(room_crv)] == "LvS")
                    shaft_entry_pts.AddRange(entry_pts);

                nested_entry_pts.Add(entry_pts);
            }

            // parse the input (input is a nested integer list indicating a zoning plan)
            // each nested list contain the space id of the same thermal zone
            List<List<int>> nested_ids = new List<List<int>>() { };
            // if in debug mode, each space apart from the shaft/mech forms a zone
            if (netGenMode < 0)
            {
                foreach (double area in areas)
                {
                    if (!shaft_ids.Contains(areas.IndexOf(area)))
                    {
                        nested_ids.Add(new List<int>() { areas.IndexOf(area) });
                    }
                }
                netGenMode = -netGenMode; // direct to the normal partitions
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
                if (nested_ids[i].Count == 1) // if a space forms a zone itself, just connect the entry point to the chases
                {
                    // select the shorter connections from cons
                    int min_idx = 0;
                    if (cons.Count > 1)
                    {
                        double min_dist = double.PositiveInfinity;
                        foreach (Line con in cons)
                        {
                            if (con.Length < min_dist)
                            {
                                min_idx = cons.IndexOf(con);
                                min_dist = con.Length;
                            }
                        }
                    }
                    zone_networks.Add(new List<Line>() { cons[min_idx] }); // only for visualization
                    Point3d ahu = cons[min_idx].PointAt(1) - 0.5 * cons[min_idx].Direction / cons[min_idx].Length;
                    AHUs.Add(ahu);
                    zoneLoads.Add(spaceLoads[nested_ids[i][0]]);

                    PathFinding.Graph<int> graph = new PathFinding.Graph<int>(true, true);
                    PathFinding.Node<int> root = graph.AddNode(0, 0);
                    graph.Nodes.Last().Coords = ahu;
                    graph.Nodes.Last().isRoot = true;
                    PathFinding.Node<int> terminal = graph.AddNode(1, 0);
                    graph.Nodes.Last().Coords = cons[0].PointAt(0);
                    graph.AddEdge(root, terminal, (float)cons[0].Length);
                    zone_graphs.Add(graph);
                }
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
                    Point3d ahu = PathFinding.GetPseudoRootOfGraph(zone_graph);
                    AHUs.Add(ahu); // only for visualization
                    zoneLoads.Add(spaceLoads.Where((load, id) => nested_ids[i].Contains(id + 1)).Sum());

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
            List<List<Line>> sys_networks = new List<List<Line>>() { };
            // generate system zones based on grouped zones
            List<PathFinding.Graph<int>> sys_graphs = new List<PathFinding.Graph<int>>() { };

            // in this step, all possible candidate points become valid ones
            List<Line> sys_edge_complete = PathFinding.GetTerminalConnection(edges, 
                Util.ConcateLists(AHUs, shaft_entry_pts), out List<Line> connections);

            // in this graph, try to find which shaft is the nearest for which AHU
            PathFinding.Graph<int> sys_whole_graph = PathFinding.RebuildGraph(sys_edge_complete);
            List<PathFinding.Node<int>> shaft_nodes = new List<PathFinding.Node<int>>() { };
            List<int> source_ids = new List<int>();
            List<List<double>> sys_flows = new List<List<double>>();
            
            foreach (PathFinding.Node<int> node in sys_whole_graph.Nodes)
            {
                if (node.Neighbors.Contains(node))
                    node.RemoveNeighbors(node);
                foreach (Point3d pt in shaft_entry_pts)
                {
                    if (pt.DistanceTo(node.Coords) < 0.000001)
                    {
                        shaft_nodes.Add(node);
                        source_ids.Add(node.Index);
                    }
                }
                for (int i = 0; i < AHUs.Count; i++)
                {
                    if (AHUs[i].DistanceTo(node.Coords) < 0.000001)
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
                        if (pt.DistanceTo(node.Coords) < 0.000001) // valid node
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
                    sys_networks.Add(sys_network);

                }
            }
            else
            {
                var BCPs = new List<List<Tuple<int, int>>>();
                if (netGenMode == 1)
                    BCPs = IntegerPrograms.BalancedConnectedPartition(sys_whole_graph, source_ids.Count, source_ids, out sys_flows);
                else
                    BCPs = IntegerPrograms.BalancedConnectedPartition(sys_whole_graph, netGenMode, new List<int>() { }, out sys_flows);
                foreach (List<Tuple<int, int>> partition in BCPs)
                {
                    List<Line> sys_network = new List<Line>();
                    List<double> sys_flow = new List<double>();
                    foreach (Tuple<int, int> edge in partition)
                    {
                        sys_network.Add(new Line(
                            sys_whole_graph.Nodes[edge.Item1].Coords,
                            sys_whole_graph.Nodes[edge.Item2].Coords));
                    }
                    sys_networks.Add(sys_network);
                }
            }
            
            // batch from sys_networks to sys_graphs
            foreach (List<Line> sys_network in sys_networks)
            {
                PathFinding.Graph<int> sys_graph = PathFinding.RebuildGraph(sys_network);
                // how rookie finds the root/terminal node by comparing coordinates
                foreach (Point3d shaft_entry_pt in shaft_entry_pts)
                {
                    foreach (PathFinding.Node<int> junc in sys_graph.Nodes)
                    {
                        if (junc.Coords.DistanceTo(shaft_entry_pt) < 0.0001)
                            junc.isRoot = true;
                    }
                }
                sys_graph.Graft();
                sys_graphs.Add(sys_graph);
            }


            // 20240415 Temperal code for testing -------------------------------------------------------------------------------
            // generate a sample graph including all space entry points and loads for partitioning
            // in the future, the space loads will be aggregated for AHU sizing and the graph after 
            // such a nodes merging will go for balanced partitioning

            // represent each space-zone with one entry point
            List<Point3d> space_zone_entry_pts = new List<Point3d>() { };
            foreach (List<Point3d> pts in nested_entry_pts)
            {
                space_zone_entry_pts.Add(pts[0]);
            }

            List<Line> guidelines = PathFinding.GetTerminalConnection(edges,
                    space_zone_entry_pts, out List<Line> sub_guidelines);
            PathFinding.Graph<int> guide_graph = PathFinding.RebuildGraph(guidelines);

            // 20240425 something wrong with the graph generation causing self-connected edge
            // no time to debug it I just remove the node itself from its neighbors
            foreach (PathFinding.Node<int> node in guide_graph.Nodes)
                if (node.Neighbors.Contains(node))
                    node.RemoveNeighbors(node);

            // parse this graph into D3.js format // the data model of PathFinding.Graph needs update
            List<SchemaJSON.node> jsonNodes = new List<SchemaJSON.node>() { };
            List<SchemaJSON.link> jsonLinks = new List<SchemaJSON.link>() { };
            List<Point3d> nodelist = new List<Point3d>() { };

            foreach (PathFinding.Node<int> node in guide_graph.Nodes)
            {
                // visualize the node index by a sequential list
                nodelist.Add(node.Coords);

                bool matched_flag = false;
                for (int i = 0; i < space_zone_entry_pts.Count; i++)
                {
                    if (space_zone_entry_pts[i].DistanceTo(node.Coords) < 0.000001)
                    {
                        // update graph with load information
                        node.Weight = spaceLoads[i];

                        jsonNodes.Add(new SchemaJSON.node
                        {
                            id = node.Value,
                            weight = spaceLoads[i]
                        });
                        matched_flag = true;
                    }
                }
                if (!matched_flag)
                {
                    jsonNodes.Add(new SchemaJSON.node
                    {
                        id = node.Value,
                        weight = 0.0
                    });
                }
            }
            // batch edges
            foreach (PathFinding.Edge<int> edge in guide_graph.GetEdges())
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

            DA.SetDataList(0, guidelines);
            DA.SetDataTree(1, Util.ListToTree(sys_networks));
            DA.SetDataTree(2, Util.ListToTree(sys_flows));
            DA.SetDataTree(3, Util.ListToTree(zone_networks));
            DA.SetDataList(4, AHUs);
            DA.SetDataList(5, zoneLoads);
            
            // pairing each system network and the zones it controls
            string sysJSON = SerializeJSON.InitiateSystem(sys_graphs, zone_graphs, nested_ids, nested_entry_pts, AHUs, areas, true);
            DA.SetData(6, sysJSON);
            DA.SetData(7, d3JSON);
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