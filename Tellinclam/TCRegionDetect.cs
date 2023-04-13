using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;
using Tellinclam;
using Tellinclam.Algorithms;

namespace Ovenbird
{
    public class TCRegionDetect : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCRegionDetect()
          : base("Region from Intersected Lines", "R",
            "Sort out space boundaries and surface relations from a bunch of fixed wall centerlines",
            "Clam", "Basic")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Linelike Curves", "Crvs",
                "Lineline-Curves as input floorplan sketches", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Outer Shell", "Shell",
                "Floorplan outer shell (clockwise, closed polyline)", GH_ParamAccess.item);
            pManager.AddCurveParameter("Space Boundaries", "Spaces",
                "Space Boundaries (counter-clockwise, closed polyline)", GH_ParamAccess.list);
            pManager.AddLineParameter("Trimmed Lines", "Orphans", 
                "Lines removed apart from the space boundaries", GH_ParamAccess.list);
            pManager.AddCurveParameter("Shattered Lines", "Shatters",
                "Splitted lines based on curve-curve intersections", GH_ParamAccess.list);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> lineCrvs = new List<Curve>();

            if (!DA.GetDataList(0, lineCrvs))
                return;

            if (lineCrvs == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please check if the inputs are qualified");
                return;
            }

            List<Curve> shatters = Basic.ShatterCrvs(lineCrvs);
            List<Line> lines = new List<Line>() { };
            foreach (Curve crv in shatters)
            {
                if (!crv.IsValid)
                    continue;
                if (crv.IsLinear())
                    lines.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
            }

            RegionDetect.GetRegion(lines, out List<Polyline> regions, out List<Line> orphans);

            DA.SetData(0, regions[0]);
            DA.SetDataList(1, regions.Skip(0));
            DA.SetDataList(2, orphans);
            DA.SetDataList(3, lines);
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
                return Tellinclam.Properties.Resources.region;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("5B898405-B556-45C8-83DA-38601C463258");
    }
}