using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public class TCRemoveDup : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCRemoveDup()
          : base("RemoveDuplicateX", "Del",
            "Remove duplicate points or line segments",
            "Clam", "Util")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            //pManager.AddPointParameter("Points", "Pts", "List of Points", GH_ParamAccess.list);
            pManager.AddLineParameter("Lines", "L", "List of Lines", GH_ParamAccess.list);
            pManager.AddNumberParameter("Tolerance", "tol", "Distance limit", GH_ParamAccess.item, 0.0001);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            //pManager.AddPointParameter("Points", "Pts_", "List with duplicate points removed", GH_ParamAccess.list);
            pManager.AddLineParameter("Lines", "L", "List with duplicate lines removed", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> pts = new List<Point3d>() { };
            List<Line> lines = new List<Line>() { };
            double tol = 0.0001;
            if (!DA.GetDataList(0, lines))
            {
                return;
            }
            DA.GetData(1, ref tol);

            //var pts_ = Basic.RemoveDupPoints(pts, tol);
            var lines_ = Basic.RemoveDupLines(lines, tol);

            //DA.SetDataList(0, pts_);
            DA.SetDataList(0, lines_);
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
                return Properties.Resources.cull_line;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("FF7C829A-8E7B-4EC2-8DF4-2418230425AB");
    }
}