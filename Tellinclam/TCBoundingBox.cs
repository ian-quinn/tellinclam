using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;
using System.Linq;

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
            pManager.AddPointParameter("Points", "Pts", "List of points", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Method", "m", "Method to generate the box. Input integer 0, 1, 2, or 3. Right click to see all options.", GH_ParamAccess.item);
            Param_Integer param = pManager[1] as Param_Integer;
            param.AddNamedValue("3D min box by CGAL", 0);
            param.AddNamedValue("2D min box by CGAL", 1);
            param.AddNamedValue("2D convex hull", 2);
            param.AddNamedValue("2D box Calipers", 3);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Vertices", "Vtx", "Vertex of the bounding box", GH_ParamAccess.list);
            pManager.AddBrepParameter("Brep box", "Box", "The optimal bounding box in brep", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> pts = new List<Point3d>() { };
            int method = 3;
            if (!DA.GetDataList(0, pts) || !DA.GetData(1, ref method))
            {
                return;
            }

            if (method == 0)
            {
                List<Point3d> vtx = OptBoundingBox.ObbAsPoint3d(pts);
                Plane plane = new Plane(vtx[0], vtx[1], vtx[2]);
                Brep box = new Box(plane, pts).ToBrep();
                DA.SetDataList(0, vtx);
                DA.SetData(1, box);
            }

            if (method == 1)
            {
                List<Point3d> vtx2 = OptBoundingBox.ObbWithPoint2d(Basic.PtProjToXY(pts));
                Polyline box2 = new Polyline(new Point3d[] { vtx2[0], vtx2[1], vtx2[2], vtx2[3], vtx2[0] });
                DA.SetDataList(0, vtx2);
                DA.SetData(1, box2);
            }

            if (method == 2)
            {
                List<Point3d> pts_XY = new List<Point3d>() { };
                foreach (Point3d pt in pts)
                {
                    pts_XY.Add(new Point3d(pt.X, pt.Y, 0));
                }
                List<Point3d> vtx3 = BoundingHull.GetConvexHull(pts_XY);
                vtx3.Add(vtx3[0]);
                Polyline poly = new Polyline(vtx3);
                Brep[] breps = Brep.CreatePlanarBreps(poly.ToNurbsCurve(), 0.000001);
                DA.SetDataList(0, vtx3);
                DA.SetData(1, breps[0]);
            }

            if (method == 3)
            {
                List<Point3d> pts_XY = new List<Point3d>() { };
                foreach (Point3d pt in pts)
                {
                    pts_XY.Add(new Point3d(pt.X, pt.Y, 0));
                }
                List<Point3d> vtx4 = BoundingHull.GetMinimalRectHull(pts_XY);
                vtx4.Add(vtx4[0]);
                Polyline poly = new Polyline(vtx4);
                Brep[] breps = Brep.CreatePlanarBreps(poly.ToNurbsCurve(), 0.000001);
                DA.SetDataList(0, vtx4);
                DA.SetData(1, breps[0]);
            }
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