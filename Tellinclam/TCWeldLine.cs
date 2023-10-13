using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public class TCWeldLine : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCWeldLine()
          : base("Line Welding", "Weld",
            "Weld co-lined line segments within certain angle difference",
            "Clam", "Util")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("-", "Ls", "-", GH_ParamAccess.list);
            pManager.AddNumberParameter("-", "0", "-", GH_ParamAccess.item);
            pManager.AddNumberParameter("-", "d", "-", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Extension box", "Ls", "Extension box", GH_ParamAccess.list);
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
            double tol_theta = 0.00001;
            double tol_d = 0.00001;
            if (!DA.GetDataList(0, crvs) || !DA.GetData(1, ref tol_theta) || !DA.GetData(2, ref tol_d))
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


            var fusions = Basic.SegsWelding(edges, tol_d, tol_d, tol_theta);

            DA.SetDataList(0, fusions);
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
        public override Guid ComponentGuid => new Guid("FFF1CD5F-92E2-4C5C-8041-7079BED6E7FE");
    }
}