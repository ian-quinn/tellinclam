using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public class TCBoundingBox : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCBoundingBox()
          : base("Get Optimal Bounding Box", "OBB",
            "Optimal Bounding Box wrapper for CGAL",
            "Clam", "Basic")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "Pts",
                "List of points", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Vertices", "Vtx", "Vertex of the bounding box", GH_ParamAccess.list);
            pManager.AddBrepParameter("3D Box", "Box", "The optimal bounding box in brep", GH_ParamAccess.item);
            pManager.AddCurveParameter("Box on XY plane", "Ply", "The 2D bounding box in polyline", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> pts = new List<Point3d>() { };
            if (!DA.GetDataList(0, pts))
            {
                return;
            }

            List<Point3d> vtx = OptBoundingBox.ObbAsPoint3d(pts);
            Plane plane = new Plane(vtx[0], vtx[1], vtx[2]);
            Brep box = new Box(plane, pts).ToBrep();

            List<Point3d> vtx2 = OptBoundingBox.ObbWithPoint2d(Basic.PtProjToXY(pts));
            Polyline box2 = new Polyline(new Point3d[] { vtx2[0], vtx2[1], vtx2[2], vtx2[3], vtx2[0] });

            DA.SetDataList(0, vtx);
            DA.SetData(1, box);
            DA.SetData(2, box2);
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
                return Properties.Resources.obb;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("02556853-39FB-4E8D-A774-4EA97E6DD7B8");
    }
}