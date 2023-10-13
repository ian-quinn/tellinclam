using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public class TCPruneSkeleton : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCPruneSkeleton()
          : base("Prune Skeleton", "PruneSkel",
            "Trim branches and simplify the trunk as the polygon centerlines",
            "Clam", "Basic")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Skeletons", "Skt",
                "Non self-intersected or cross-intersected polylines as input", GH_ParamAccess.list);
            pManager.AddCurveParameter("Bisectors", "Bis",
                "Non self-intersected or cross-intersected polylines as input", GH_ParamAccess.list);
            pManager.AddNumberParameter("Height Tolerance", "tol_h",
                "Non self-intersected or cross-intersected polylines as input", GH_ParamAccess.item);
            pManager.AddNumberParameter("Node Collapse Tolerance", "tol_d",
                "Non self-intersected or cross-intersected polylines as input", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Centerline", "Edge", "Centerlines collapsed from skeletons", GH_ParamAccess.list);
            pManager.AddPointParameter("Debug_vts", "Vtx", "Vertices of the network graph (for debug)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Debug_height", "ht", "Height value of each vertex (for debug)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Debug_degree", "deg", "Degree value of each vertex (for debug)", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> skes = new List<Curve>() { };
            List<Curve> secs = new List<Curve>() { };
            double tol_h = 1;
            double tol_d = 0.2;
            // 0.01 - default angle tolerance when merging two almost parallel edges
            double tol_theta = 0.01;

            if (!DA.GetDataList(0, skes) || !DA.GetDataList(1, secs))
            {
                return;
            }

            DA.GetData(2, ref tol_h);
            DA.GetData(3, ref tol_d);

            List<Line> skeletons = new List<Line>() { };
            List<Line> bisectors = new List<Line>() { };

            foreach (Curve crv in skes)
            {
                // curve self-intersection check

                if (!crv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "At least one line is not valid.");
                    continue;
                }
                if (crv.IsLinear())
                {
                    skeletons.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
                }
            }
            foreach (Curve crv in secs)
            {
                // curve self-intersection check

                if (!crv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "At least one line is not valid.");
                    continue;
                }
                if (crv.IsLinear())
                {
                    bisectors.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
                }
            }

            // remove duplicate lines
            List<Line> skeletons_ = Basic.RemoveDupLines(skeletons, 0.0001);
            List<Line> bisectors_ = Basic.RemoveDupLines(bisectors, 0.0001);

            // the input lines should be well shattered without crossings

            List<Line> edges = SkeletonPrune.Prune(skeletons_, bisectors_, tol_h, tol_d, tol_theta,
                out List<Point3d> vts, out List<double> vts_height, out List<int> vts_degree);

            DA.SetDataList(0, edges);
            DA.SetDataList(1, vts);
            DA.SetDataList(2, vts_height);
            DA.SetDataList(3, vts_degree);
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
                return Properties.Resources.prune;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("46D5B4FF-E0FE-48EA-9392-A6E6CF5DCD1E");
    }
}