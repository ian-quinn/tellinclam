using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public class TCDittoFoilCollapse : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCDittoFoilCollapse()
          : base("Test module for convex/concave hull algorithms", "Hull",
            "Testing convex/concave hull algorithms",
            "Clam", "Lab")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("-", "Pts", "-", GH_ParamAccess.list);
            pManager.AddCurveParameter("-", "Crv", "-", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Extension box", "CHull", "Extension box", GH_ParamAccess.list);
            pManager.AddPointParameter("Extension box", "RHull", "Extension box", GH_ParamAccess.list);
            pManager.AddPointParameter("Extension box", "FHull", "Extension box", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> pts = new List<Point3d>() { };
            List<Curve> crvs = new List<Curve>() { };
            List<Line> edges = new List<Line>() { };
            if (!DA.GetDataList(0, pts) || !DA.GetDataList(1, crvs))
            {
                return;
            }

            foreach (Curve crv in crvs)
            {
                // curve self-intersection check

                if (!crv.IsValid)
                {
                    continue;
                }
                if (crv.IsLinear())
                {
                    edges.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
                }
            }

            //List<PolylineCurve> boxes = new List<PolylineCurve>() { };
            //foreach (Line edge in edges)
            //{
            //    boxes.Add(EdgeAlign.GetExpansionBox(edge, exp));
            //}


            var vts = BoundingHull.GetConvexHull(pts);
            var mb = BoundingHull.GetMinimalRectHull(pts);
            var foil = BoundingHull.GetMinFoilHull(edges);

            DA.SetDataList(0, vts);
            DA.SetDataList(1, mb);
            DA.SetDataList(2, foil);
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
                return Properties.Resources.ditto;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("584FFC41-6546-4FC2-9433-EF53380607EB");
    }
}