using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public class TCStraightSkeleton : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCStraightSkeleton()
          : base("Get Stright Skeleton of Polygons", "Stright-Skel",
            "Stright Skeleton wrapper for CGAL",
            "Clam", "Basic")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Polylines", "Poly", 
                "Non self-intersected or cross-intersected polylines as input", GH_ParamAccess.list);
            pManager.AddNumberParameter("time", "time", "test input", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Vertices", "Vtx", "Vertices of the straight skeleton", GH_ParamAccess.tree);
            pManager.AddLineParameter("Skeleton", "Skt", "Inter straight skeleton of the polygon", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Height", "hgt", "The time cost offseting this skeleton edge", GH_ParamAccess.tree);
            pManager.AddLineParameter("Bisector", "Bis", "Trace of polygon vertice moving inward", GH_ParamAccess.tree);
            pManager.AddLineParameter("Contour", "Ctr", "Boundary contour with certain offset", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Polys", "Ply", "Boundary contour with certain offset", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> crvs = new List<Curve>() { };
            double time = 1;
            DA.GetData(1, ref time);
            if (!DA.GetDataList(0, crvs))
            {
                return;
            }

            List<Polyline> plines = new List<Polyline>() { };

            foreach (Curve crv in crvs)
            {
                // curve self-intersection check

                if (!crv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not valid.");
                    continue;
                }
                if (!crv.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "One polyline is not closed.");
                    continue;
                }
                if (crv.IsPolyline())
                    if (crv.TryGetPolyline(out Polyline pline))
                        plines.Add(pline);
            }

            List<List<Point3d>> nestedVertices = new List<List<Point3d>>() { };
            List<List<Line>> nestedSkeletons = new List<List<Line>>() { };
            List<List<double>> nestedHeights = new List<List<double>>() { };
            List<List<Line>> nestedBisectors = new List<List<Line>>() { };
            List<List<Line>> nestedContours = new List<List<Line>>() { };

            List<List<Polyline>> nestedPolys = new List<List<Polyline>>() { };

            List<List<Polyline>> MCRs = new List<List<Polyline>>() { };
            foreach (Polyline pline in plines)
            {
                if (Basic.IsClockwise(pline))
                    pline.Reverse();
                MCRs.Add(new List<Polyline>() { pline.Duplicate() });
            }
                
            bool[] redundantMcr = new bool[plines.Count];
            for (int i = plines.Count - 1; i >= 0; i--)
            {
                for (int j = plines.Count - 1; j >= 0; j--)
                {
                    if (i != j)
                        if (Basic.IsPolyInPoly(plines[i], plines[j]))
                        {
                            // the inner holes should be clockwise
                            plines[i].Reverse();
                            MCRs[j].Add(plines[i]);
                            redundantMcr[i] = true;
                        }
                }
            }

            for (int i = 0; i < MCRs.Count; i++)
            {
                if (redundantMcr[i])
                    continue;
                var edges = StraightSkeleton.SsAsPoint3d(MCRs[i]);
                nestedSkeletons.Add(edges.Item1);
                List<double> heights = new List<double>() { };
                foreach (Tuple<double, double> pair in edges.Item2)
                {
                    //if (pair.Item1 == pair.Item2)
                    //    heights.Add(pair.Item1);
                    //else
                    heights.Add((pair.Item1 + pair.Item2) / 2);
                }
                nestedHeights.Add(heights);
                nestedBisectors.Add(edges.Item3);
                nestedContours.Add(edges.Item4);
            }

            for (int i = 0; i < MCRs.Count; i++)
            {
                if (redundantMcr[i])
                    continue;
                var polys = StraightSkeleton.OffsetPolygon(MCRs[i], time);
                List<Polyline> polylines = new List<Polyline>() { };
                foreach (List<Point3d> poly in polys)
                {
                    poly.Add(poly[0]);
                    polylines.Add(new Polyline(poly));
                }
                nestedPolys.Add(polylines);
            }

            // p

            foreach (List<Line> skeletons in nestedSkeletons)
            {
                List<int> degrees;
                nestedVertices.Add(Algorithms.SkeletonPrune.GetNodes(skeletons, out degrees));
            }

            DA.SetDataTree(0, Util.ListToTree(nestedVertices));
            DA.SetDataTree(1, Util.ListToTree(nestedSkeletons));
            DA.SetDataTree(2, Util.ListToTree(nestedHeights));
            DA.SetDataTree(3, Util.ListToTree(nestedBisectors));
            DA.SetDataTree(4, Util.ListToTree(nestedContours));

            DA.SetDataTree(5, Util.ListToTree(nestedPolys));
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
                return Properties.Resources.ss;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("DF1DF1AA-CEAA-4EB7-8385-26F0A95F1C07");
    }
}