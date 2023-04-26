using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public class TCScatterPoint : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCScatterPoint()
          : base("Scatter Points", "SP",
            "Get mesh grid responsive to the polygon boundary",
            "Clam", "Basic")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Space Boundary", "Poly",
                "The space boundary represented by polylines", GH_ParamAccess.list);
            pManager.AddNumberParameter("Vent Diameter as List", "R",
                "A list of module number of coverage diameter for all sizes of vent", GH_ParamAccess.list);
            pManager.AddNumberParameter("Coverage", "cool",
                "The coverage radius of the vent", GH_ParamAccess.item, 0.9);
            pManager.AddNumberParameter("Coverage", "scale",
                "The coverage radius of the vent", GH_ParamAccess.item, 0.5);
            pManager.AddIntegerParameter("Coverage", "iter",
                "The coverage radius of the vent", GH_ParamAccess.item, 500);
            pManager.AddBooleanParameter("Adjust position?", "tune?",
                "Whether to adjust the layout after scattering points", GH_ParamAccess.item, false);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Responsive vents", "Vt", "Layout of vents", GH_ParamAccess.tree);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> crvs = new List<Curve>() { };
            List<double> coverD = new List<double>() { };
            double coolR = 2;
            double scaleF = 1;
            int maxIter = 100;
            bool flag_tune = false;
            if (!DA.GetDataList(0, crvs))
            {
                return;
            }
            DA.GetDataList(1, coverD);
            DA.GetData(2, ref coolR);
            DA.GetData(3, ref scaleF);
            DA.GetData(4, ref maxIter);
            DA.GetData(5, ref flag_tune);

            List<Polyline> plines = new List<Polyline>() { };

            foreach (Curve crv in crvs)
            {
                // curve self-intersection check

                if (!crv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one polyline is not valid.");
                    continue;
                }
                if (!crv.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one polyline is not closed.");
                    continue;
                }
                if (crv.IsPolyline())
                    if (crv.TryGetPolyline(out Polyline pline))
                        plines.Add(pline);
            }

            List<List<Point3d>> pts = new List<List<Point3d>>() { };
            foreach (Polyline pline in plines)
            {
                pts.Add(PointScatter.GetLayout(pline, coverD, coolR, scaleF, maxIter, flag_tune));
            }

            DA.SetDataTree(0, Util.ListToTree(pts));
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
                return Properties.Resources.vent;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("085039EC-B835-488E-AA5F-6DE951973EE1");
    }
}