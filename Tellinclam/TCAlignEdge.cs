using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public class TCAlignEdge : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCAlignEdge()
          : base("Get line segments aligned", "AE",
            "Get line segments aligned",
            "Clam", "Util")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Edges", "Es", "List of edges", GH_ParamAccess.list);
            pManager.AddNumberParameter("Distance", "tol_d", "Distance threshold for lines collapsing to axis", GH_ParamAccess.item);
            pManager.AddNumberParameter("Radius", "tol_c", "Range threshold for points collapsing to axis intersections", GH_ParamAccess.item);
            pManager.AddNumberParameter("Theta", "tol_0", "Angle tolerance for axis extraction", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Edges", "Es", "Aligned edges", GH_ParamAccess.list);
            pManager.AddVectorParameter("Directions", "Dirs", "Directions", GH_ParamAccess.list);
            pManager.AddLineParameter("Axes", "Axes", "Reconstructed axes", GH_ParamAccess.list);
            pManager.AddLineParameter("Line Groups", "Grps", "Line Groups for axes", GH_ParamAccess.tree);
            pManager.AddPointParameter("Axes Intersections", "X", "Intersections of Axes candidates", GH_ParamAccess.list);
            pManager.AddPointParameter("Point alignment 1", "vt1", "End points after edges offset", GH_ParamAccess.list);
            pManager.AddPointParameter("Point alignment 2", "vt2", "End points after edges extension", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> crvs = new List<Curve>() { };
            List<Line> edges = new List<Line>() { };
            double tol_d = 0.5;
            double tol_c = 0.5;
            double tol_theta = 5 / 180 * Math.PI;
            if (!DA.GetDataList(0, crvs) || !DA.GetData(1, ref tol_d) ||
                !DA.GetData(2, ref tol_c) || !DA.GetData(3, ref tol_theta))
            {
                return;
            }

            foreach (Curve crv in crvs)
            {
                // curve self-intersection check

                if (!crv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "At least one line is not valid.");
                    continue;
                }
                if (crv.IsLinear())
                {
                    edges.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
                }
            }

            List<Line> shatters = Basic.BreakLinesAtIntersection(edges);

            List<Line> aligned = EdgeAlign.Align(shatters, tol_d, tol_c, tol_theta, 
                out List<Vector3d> dirs, out List<Line> axes, out List<List<Line>> groups, 
                out List<Point3d> X, out List<Point3d> vt1, out List<Point3d> vt2);

            DA.SetDataList(0, aligned);
            DA.SetDataList(1, dirs);
            DA.SetDataList(2, axes);
            DA.SetDataTree(3, Util.ListToTree(groups));
            DA.SetDataList(4, X);
            DA.SetDataList(5, vt1);
            DA.SetDataList(6, vt2);
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
                return Properties.Resources.align;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("EDB62C02-52AD-4263-B08F-4EACBEAF9E2A");
    }
}