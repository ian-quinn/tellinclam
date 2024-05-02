using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;
using System.Diagnostics;
using Tellinclam.Serialization;

namespace Tellinclam
{
    public class TCFindPath : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCFindPath()
          : base("Path Finder", "Path",
            "Get the path between possible source points and end points",
            "Clam", "Lab")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            
            pManager.AddCurveParameter("Lines", "Ls",
                "List of line segments representing trunk network", GH_ParamAccess.list);
            pManager.AddCurveParameter("Terminals", "T",
                "List of space boundary as terminals", GH_ParamAccess.list);
            pManager.AddCurveParameter("Sources", "S",
                "List of space boundary as sources", GH_ParamAccess.list);
            // for internal information flow, this input  will be replaced by JSON file
            pManager.AddIntegerParameter("Zoned index", "n",
                "List of space index from one zone cluster", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // for visualization and debug
            pManager.AddLineParameter("Complete Graph", "G", 
                "Connecting terminals to the main trunk by minimum-cost Manhattan distance.", GH_ParamAccess.list);
            pManager.AddLineParameter("Sub-Graph", "G_", 
                "Subset of original graph including terminals and candidate steiner points by Floyd-Warshell.", GH_ParamAccess.list);
            pManager.AddLineParameter("Steiner Tree", "S", 
                "Minimum tree connecting terminals with steiner points from original graph", GH_ParamAccess.list);
            pManager.AddPointParameter("Graph Centroid", "C",
                "The pseudo centroid of graph for AHU layout that minimizes (almost) the branches", GH_ParamAccess.item);
            // for internal information flow
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
            List<Curve> crvs = new List<Curve>() { };
            List<Curve> sinks = new List<Curve>() { };
            List<Curve> sources = new List<Curve>() { };
            List<int> target_idx = new List<int>() { };
            if (!DA.GetDataList(0, crvs) || !DA.GetDataList(1, sinks) || 
                !DA.GetDataList(2, sources) || !DA.GetDataList(3, target_idx))
            {
                return;
            }

            List<Line> edges = new List<Line>() { };
            List<Point3d> terminals = new List<Point3d>() { };
            List<Point3d> shafts = new List<Point3d>() { };
            foreach (Curve crv in crvs)
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
            foreach (Curve sink in sinks)
            {
                if (!sink.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not valid.");
                    continue;
                }
                if (!sink.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not closed.");
                    continue;
                }
                if (sink.IsPolyline())
                    if (sink.TryGetPolyline(out Polyline pline))
                    {
                        Point3d centroid = pline.CenterPoint();
                        terminals.Add(centroid);
                    }
            }
            foreach (Curve source in sources)
            {
                if (!source.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not valid.");
                    continue;
                }
                if (!source.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not closed.");
                    continue;
                }
                if (source.IsPolyline())
                    if (source.TryGetPolyline(out Polyline pline))
                    {
                        Point3d centroid = pline.CenterPoint();
                        shafts.Add(centroid);
                    }

            }

            List<Point3d> terminals_zoned = new List<Point3d>() { };
            foreach (int idx in target_idx)
                terminals_zoned.Add(terminals[idx]);

            List<Line> edges_complete = Algorithms.PathFinding.GetTerminalConnection(edges, terminals_zoned, out List<Line> cons);
            List<Line> edges_subgraph = Algorithms.PathFinding.GetSubGraph(edges_complete, terminals_zoned, out _);
            // minimum spanning tree applied on edges_subgraph
            List<Line> edges_steiner = Algorithms.PathFinding.GetSteinerTree(
                edges_subgraph, terminals_zoned, new List<Point3d>() { }, PathFinding.algoEnum.MST);
            PathFinding.Graph<int> graph = PathFinding.RebuildGraph(edges_steiner);
            
            PathFinding.Graph<int> trunk = new PathFinding.Graph<int>(true);
            Point3d ahu = PathFinding.GetPseudoRootOfGraph(graph);
            graph.Graft();

            DA.SetDataList(0, edges_complete);
            DA.SetDataList(1, edges_subgraph);
            DA.SetDataList(2, edges_steiner);
            DA.SetData(3, ahu);

            // only for the geometry test
            DA.SetData(4, "");
            //DA.SetData(4, SerializeJSON.InitiateSystemGraph(new List<PathFinding.Graph<int>>() { trunk }, new List<PathFinding.Graph<int>>() { graph }, 
            //    new List<List<int>>() { target_idx }, "sample_system", true));
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
        public override Guid ComponentGuid => new Guid("14CAC4D3-4736-4FE3-B3FF-3A5BC3097C57");
    }
}