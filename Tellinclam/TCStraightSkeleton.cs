using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;
using Rhino;
using System.Linq;

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
            pManager.AddCurveParameter("Polygons", "Ply", 
                "Non self-intersected or cross-intersected polylines as input", GH_ParamAccess.list);
            pManager.AddNumberParameter("Wavefront time", "t", 
                "Time value for the wavefront polygon generation", GH_ParamAccess.item);
            pManager.AddNumberParameter("Prune threshold", "¦Î", 
                "Skeletons stretching out the wavefront of this time value will be removed", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Vertices", "Vtx", "Vertices of the straight skeleton", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Times", "time", "The time cost offseting this skeleton edges", GH_ParamAccess.tree);
            pManager.AddLineParameter("Skeletons", "Skt", "Inter straight skeleton of the polygon", GH_ParamAccess.tree);
            pManager.AddLineParameter("Bisectors", "Bis", "Trace of polygon vertice moving inward", GH_ParamAccess.tree);
            pManager.AddLineParameter("Contours", "Ctr", "Boundary contour with certain offset", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Wavefronts", "Wvf", "Boundary contour with certain offset", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double _tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            List<Curve> crvs = new List<Curve>() { };
            double ti = 1;
            double xi = 0;
            DA.GetData(1, ref ti);
            DA.GetData(2, ref xi);
            if (!DA.GetDataList(0, crvs))
            {
                return;
            }
            // prepare inputs
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
            List<List<Polyline>> MCRs = new List<List<Polyline>>();
            foreach (Polyline pline in plines)
            {
                if (Basic.IsClockwise(pline))
                    pline.Reverse();
                MCRs.Add(new List<Polyline>() { pline.Duplicate() });
            }
            bool[] redundantMCR = new bool[plines.Count];
            for (int i = 0; i < plines.Count; i++)
            {
                for (int j = 0; j < plines.Count; j++)
                {
                    if (i != j)
                        if (Basic.IsPolyInPoly(plines[i], plines[j]))
                        {
                            // the inner holes should be clockwise
                            plines[i].Reverse();
                            MCRs[j].Add(plines[i]);
                            redundantMCR[i] = true;
                        }
                }
            }

            // prepare outputs
            List<List<Point3d>> nestedNodes = new List<List<Point3d>>();
            List<List<Line>> nestedSkeletons = new List<List<Line>>();
            List<List<double>> nestedTimes = new List<List<double>>();
            List<List<Line>> nestedBisectors = new List<List<Line>>();
            List<List<Line>> nestedContours = new List<List<Line>>();
            List<List<Polyline>> nestedWavefronts = new List<List<Polyline>>();

            for (int i = 0; i < MCRs.Count; i++)
            {
                if (redundantMCR[i])
                    continue;
                var SS = StraightSkeleton.SsAsPoint3d(MCRs[i]);
                
                // note that the output bisectors must be in reversed pairs
                // because they all come from the superpositioned monotone polygons
                List<Line> skeletons = Basic.RemoveDupLines(SS.Item1, _tol, out List<int> del_ids);

                int[,] adjMat = EdgeAlign.GetAdjMat(skeletons, out Point3d[] vts, out int[] vts_degree);
                double[] vts_time = new double[vts.Length];

                for (int j = 0; j < SS.Item1.Count; j++)
                {
                    if (del_ids.Contains(j))
                        continue;
                    for (int k = 0; k < vts.Length; k++)
                    {
                        if (SS.Item1[j].PointAt(0).DistanceTo(vts[k]) < _tol)
                            vts_time[k] = SS.Item2[j].Item1;
                        if (SS.Item1[j].PointAt(1).DistanceTo(vts[k]) < _tol)
                            vts_time[k] = SS.Item2[j].Item2;
                    }
                }
                // take average heights as the height of an edge
                //foreach (Tuple<double, double> pair in edges.Item2)
                //{
                //    heights.Add((pair.Item1 + pair.Item2) / 2);
                //}

                // loop to remove all strays under certain height
                // the height indicates the offset distance from the contour
                // usually for indoor circulation area, this value should not 
                // be smaller than 0.75, or else the corridor width is too narrow
                List<int> removable_edge_idx = new List<int>() { };
                bool flag_rerun = true;
                int counter = 0;
                while (flag_rerun && counter < 50)
                {
                    counter += 1;
                    int trimmable_counter = 0;
                    for (int j = 0; j < vts.Count(); j++)
                    {
                        if (vts_time[j] < xi && vts_degree[j] == 1)
                        {
                            trimmable_counter++;
                            for (int k = 0; k < adjMat.GetLength(1); k++)
                            {
                                if (adjMat[j, k] >= 0)
                                {
                                    removable_edge_idx.Add(adjMat[j, k]);
                                    adjMat[j, k] = -1;
                                    adjMat[k, j] = -1;
                                    vts_degree[j] -= 1;
                                    vts_degree[k] -= 1;
                                    continue;
                                }
                            }
                            continue;
                        }
                    }
                    if (trimmable_counter == 0)
                        flag_rerun = false;
                }
                // remove edges stretching out the wavefront with time = epsilon
                for (int j = skeletons.Count - 1; j >= 0; j--)
                    if (removable_edge_idx.Contains(j))
                        skeletons.RemoveAt(j);

                // then 
                nestedSkeletons.Add(skeletons);
                nestedNodes.Add(vts.ToList());
                nestedTimes.Add(vts_time.ToList());
                nestedBisectors.Add(Basic.RemoveDupLines(SS.Item3, _tol, out _));
                nestedContours.Add(Basic.RemoveDupLines(SS.Item4, _tol, out _));
            }

            // generate the wavefronts 
            for (int i = 0; i < MCRs.Count; i++)
            {
                if (redundantMCR[i])
                    continue;
                // if time == 0, take the original MCR as the offset polygons
                if (ti > 0)
                {
                    var polys = StraightSkeleton.OffsetPolygon(MCRs[i], ti);
                    List<Polyline> polylines = new List<Polyline>() { };
                    foreach (List<Point3d> poly in polys)
                    {
                        poly.Add(poly[0]);
                        polylines.Add(new Polyline(poly));
                    }
                    nestedWavefronts.Add(polylines);
                }
                else
                    nestedWavefronts.Add(MCRs[i]);
            }

            DA.SetDataTree(0, Util.ListToTree(nestedNodes));
            DA.SetDataTree(1, Util.ListToTree(nestedTimes));
            DA.SetDataTree(2, Util.ListToTree(nestedSkeletons));
            DA.SetDataTree(3, Util.ListToTree(nestedBisectors));
            DA.SetDataTree(4, Util.ListToTree(nestedContours));
            DA.SetDataTree(5, Util.ListToTree(nestedWavefronts));
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