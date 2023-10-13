using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;
using Rhino.UI;
using Rhino;
using Rhino.DocObjects;

namespace Tellinclam
{
    public class TCDittoRoomEntry : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCDittoRoomEntry()
          : base("Assign door location to rooms", "Entry",
            "Assign door location to rooms",
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
            pManager.AddPointParameter("Pts", "Pts", "-", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Crv", "Crv", "-", GH_ParamAccess.list);
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
            if (!DA.GetDataList(0, pts) || !DA.GetDataList(1, crvs))
            {
                return;
            }

            List<List<Point3d>> nested_locs = new List<List<Point3d>>() { };
            foreach (Curve crv in crvs)
            {
                if (crv is null)
                    continue;
                List<Point3d> door_locs = new List<Point3d>() { };
                foreach (Point3d pt in pts)
                {
                    double t;
                    if (crv.ClosestPoint(pt, out t, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
                        door_locs.Add(pt);
                }
                nested_locs.Add(door_locs);
            }

            DA.SetDataTree(0, Util.ListToTree(nested_locs));
            DA.SetDataList(1, crvs);
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
        public override Guid ComponentGuid => new Guid("3F57FFEC-CE9F-4DB0-A5AA-CDE42D26BB94");
    }
}