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
            pManager.AddCurveParameter("Boundaries", "Room",
                "List of space boundary (closed Polyline as Curve) as terminals", GH_ParamAccess.list);
            pManager.AddTextParameter("Function Tag", "func",
                "Room tag indicating functions for test only", GH_ParamAccess.list);
            pManager.AddPointParameter("Door locations", "Door",
                "List of door location as entry points (which will be paired automatically with each room)", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Zoned index", "zones",
                "Nested lists including all space index of each zoning cluster", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("System network", "netA",
                "Minimum tree connecting AHU within current system", GH_ParamAccess.tree);
            pManager.AddLineParameter("Zone network", "netB",
                "Minimum tree connecting terminals within current thermal zone", GH_ParamAccess.tree);
            pManager.AddPointParameter("Equipment Position", "relay",
                "Pre layout of AHU for each thermal zone", GH_ParamAccess.list);
            pManager.AddTextParameter("JSON file", "json",
                "The JSON file for internal information flow", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> network_crvs = new List<Curve>() { };
            List<Curve> room_crvs = new List<Curve>() { };
            List<string> tags = new List<string>() { };
            List<Point3d> door_pts = new List<Point3d>() { };
            GH_Structure<GH_Integer> zone_ids = new GH_Structure<GH_Integer>() { };
            if (!DA.GetDataList(0, network_crvs) || !DA.GetDataList(1, room_crvs) || !DA.GetDataList(2, tags) ||
                !DA.GetDataList(3, door_pts) || !DA.GetDataTree(4, out zone_ids))
            {
                return;
            }

            // convert the main conduit chases to Line
            List<Line> edges = new List<Line>() { };
            foreach (Curve crv in network_crvs)
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
            // this is a data tree
            // one room may have several entry points
            List<List<Point3d>> nested_entry_pts = new List<List<Point3d>>() { };
            List<Point3d> shaft_entry_pts = new List<Point3d>() { };
            foreach (Curve room_crv in room_crvs)
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
                if (tags[room_crvs.IndexOf(room_crv)] == "Lv0")
                {
                    foreach (Point3d pt in door_pts)
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

                if (tags[room_crvs.IndexOf(room_crv)] == "LvS")
                    shaft_entry_pts.AddRange(entry_pts);

                nested_entry_pts.Add(entry_pts);
            }

            // parse the input (input is a nested integer list indicating a zoning plan)
            // each nested list contain the space id of the same thermal zone
            List<List<int>> nested_ids = new List<List<int>>() { };
            foreach (var branch in zone_ids.Branches)
            {
                nested_ids.Add(branch.Select(s => s.Value).ToList());
            }

            // generation of network-B, zone level
            List<List<Line>> zone_networks = new List<List<Line>>() { };
            List<PathFinding.Graph<int>> zone_graphs = new List<PathFinding.Graph<int>>() { };
            // in this AHUs, different types of systems will go dispatch
            List<Point3d> AHUs = new List<Point3d>() { };
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
                    PathFinding.Graph<int> graph = new PathFinding.Graph<int>(true, true);
                    PathFinding.Node<int> root = graph.AddNode(0);
                    graph.Nodes.Last().Coords = ahu;
                    PathFinding.Node<int> terminal = graph.AddNode(1);
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

            // in this step, all possible candidate points become valid ones
            List<Line> sys_edge_complete = PathFinding.GetTerminalConnection(edges, 
                Util.ConcateLists(AHUs, shaft_entry_pts), out List<Line> connections);
            
            // in this graph, try to find which shaft is the nearest for which AHU
            PathFinding.Graph<int> sys_whole_graph = PathFinding.RebuildGraph(sys_edge_complete);

            List<List<Point3d>> sys_zones = new List<List<Point3d>>() { };
            List<PathFinding.Node<int>> shaft_nodes = new List<PathFinding.Node<int>>() { };
            foreach (Point3d pt in shaft_entry_pts)
            {
                foreach (PathFinding.Node<int> node in sys_whole_graph.Nodes)
                {
                    if (pt.DistanceTo(node.Coords) < 0.000001)
                        shaft_nodes.Add(node);
                }
            }
            
            for (int i = 0; i < shaft_entry_pts.Count; i++)
                sys_zones.Add(new List<Point3d>() { });

            foreach (Point3d pt in AHUs)
            {
                // locate the terminal point
                foreach (PathFinding.Node<int> node in sys_whole_graph.Nodes)
                {
                    if (pt.DistanceTo(node.Coords) < 0.000001) // valid node
                    {
                        float min_dist = float.PositiveInfinity;
                        int min_id = 0;
                        for (int i = 0; i < shaft_nodes.Count; i++)
                        {
                            sys_whole_graph.GetShortestPathDijkstra(node, shaft_nodes[i], out float distance);
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

            // generate system zones based on grouped zones
            List<PathFinding.Graph<int>> sys_graphs = new List<PathFinding.Graph<int>>() { };
            for (int i = 0; i < sys_zones.Count; i++)
            {
                // critical step in this process is to transform the steiner tree problem to spanning tree problem
                // by joining the relay node inside the graph
                List<Line> sys_edge_subgraph = PathFinding.GetSubGraph(sys_edge_complete, 
                    Util.ConcateLists(sys_zones[i], new List<Point3d>() { shaft_entry_pts[i] }), out _);
                List<Line> sys_network = PathFinding.GetSteinerTree(sys_edge_subgraph, sys_zones[i], 
                    new List<Point3d>() { shaft_entry_pts[i] }, PathFinding.algoEnum.SPT);

                PathFinding.Graph<int> sys_graph = PathFinding.RebuildGraph(sys_network);
                foreach (PathFinding.Node<int> junc in sys_graph.Nodes)
                {
                    if (junc.Coords.DistanceTo(shaft_entry_pts[i]) < 0.0001)
                        junc.isRoot = true;
                }
                sys_graph.Graft();
                sys_networks.Add(sys_network);
                sys_graphs.Add(sys_graph);
            }

            DA.SetDataTree(0, Util.ListToTree(sys_networks));
            DA.SetDataTree(1, Util.ListToTree(zone_networks));
            DA.SetDataList(2, AHUs);
            // pairing each system network and the zones it controls
            
            string jsonString = SerializeJSON.InitiateSystem(sys_graphs, zone_graphs, nested_ids, nested_entry_pts, AHUs, areas, true);

            DA.SetData(3, jsonString);
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
                return Properties.Resources.clam;
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