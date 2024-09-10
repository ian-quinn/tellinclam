using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;
using System.Linq;

namespace Tellinclam
{
    public class TCPolyVertice : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCPolyVertice()
          : base("Get Polyline Vertice", "PolyV",
            "Get the vertices from a polyline type object",
            "Clam", "Util")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Polyline", "Ply", "Polyline", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Vertices", "Vtx", "Vertices", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve crv = null;
            List<Point3d> vtx = new List<Point3d>();
            if (!DA.GetData(0, ref crv))
                return;

            // curve self-intersection check
            if (!crv.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not valid.");
            }
            if (!crv.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not closed.");
            }
            if (crv.IsPolyline())
                if (crv.TryGetPolyline(out Polyline pline))
                    vtx = pline.ToList();

            DA.SetDataList(0, vtx);
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
                return Properties.Resources.weld;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("971509DA-54F3-4C6D-B39E-DB222734C33A");
    }
}