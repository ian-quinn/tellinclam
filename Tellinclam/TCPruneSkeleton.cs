using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using CGAL.Wrapper;
using Tellinclam.Algorithms;
using Rhino.Geometry.Intersect;
using Rhino;

namespace Tellinclam
{
    public class TCPruneSkeleton : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCPruneSkeleton()
          : base("Prune Skeleton", "PruneSkel",
            "Trim branches and simplify the trunk as the polygon centerlines",
            "Clam", "Basic")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Skeletons", "Skt",
                "The generated skeletons for trimming", GH_ParamAccess.list);
            pManager.AddCurveParameter("Polygons", "Ply",
                "The polygon boundary for the skeleton generation", GH_ParamAccess.list);
            pManager.AddCurveParameter("Wavefronts", "Wvf",
                "The offset wavefront for skeleton generation", GH_ParamAccess.list);
            pManager.AddNumberParameter("Offset Tolerance", "d",
                "The width of alignment window (for QT clustering)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Box ratio", "η",
                "The radio of the directional distance and the offset distance for fuzzy clustering", GH_ParamAccess.item);
            //pManager.AddNumberParameter("Distance Tolerance", "tol_θ",
            //    ""The angle between edges (for QT clustering)", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Raw Guidelines", "Union", "Skeletons merged with contours", GH_ParamAccess.list);
            pManager.AddLineParameter("Aligned Guidelines", "Guide", "Aligned guidelines", GH_ParamAccess.list);
            pManager.AddPointParameter("Debug_vts", "Vtx", "Vertices of the network graph (for debug)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Debug_degree", "deg", "Degree value of each vertex (for debug)", GH_ParamAccess.list);
            pManager.AddLineParameter("Debug_axis", "Axis", "Axis for each cluster", GH_ParamAccess.list);
            pManager.AddLineParameter("Debug_bundles", "Bds", "Nested lines that almost colined (for debug", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double _tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            List<Curve> skts = new List<Curve>();
            List<Curve> polys = new List<Curve>();
            List<Curve> ctrs = new List<Curve>();
            double tol_d = 0.2;
            double eta = 1;
            // 0.01 - default angle tolerance when merging two almost parallel edges
            double tol_theta = 0.01;

            if (!DA.GetDataList(0, skts) || !DA.GetDataList(1, polys) || !DA.GetDataList(2, ctrs))
            {
                return;
            }

            DA.GetData(3, ref tol_d);
            DA.GetData(4, ref eta);

            List<Line> skeletons = new List<Line>() { };
            List<Polyline> contours = new List<Polyline>() { };

            foreach (Curve crv in skts)
            {
                // curve self-intersection check

                if (!crv.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "At least one line is not valid.");
                    continue;
                }
                if (crv.IsLinear())
                {
                    skeletons.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
                }
            }
            foreach (Curve crv in ctrs)
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
                    if (crv.TryGetPolyline(out Polyline contour))
                        contours.Add(contour);
            }

            // remove duplicate lines (not stable?)
            //List<Line> skeletons_ = Basic.RemoveDupLines(skeletons, 0.0001);
            //List<Line> bisectors_ = Basic.RemoveDupLines(bisectors, 0.0001);

            // remove the skeletons inside contours, then merge them with contours
            // shatter skeletons by contours, check the containment of all segments
            List<Line> union = new List<Line>();
            foreach (Line edge in skeletons)
            {
                // if an edge intersects with no contour, drop it to the union
                int counter = 0;
                foreach (Curve ctr in ctrs)
                {
                    List<Line> shatters = new List<Line>();
                    LineCurve edge_ = new LineCurve(edge.PointAt(0), edge.PointAt(1));
                    CurveIntersections ccx = Intersection.CurveCurve(edge_, ctr, _tol, _tol);
                    if (ccx.Count > 0)
                    {
                        // the parameters are not normalized. It relates with the curve length
                        List<double> parameters = new List<double>() { 0, edge_.GetLength() };
                        foreach (IntersectionEvent evt in ccx)
                            if (evt.IsPoint && !parameters.Contains(evt.ParameterA)) 
                                parameters.Add(evt.ParameterA);
                        parameters.Sort();
                        if (parameters.Count > 2)
                        {
                            counter += 1; // current edge intersected, do not drop it in the union
                            // note that Line.PointAt() takes normalized parameter [0,1]
                            // while Curve.PointAt() takes parameter with length [0,crv.length]
                            // likewise, Intersection.CurveCurve() returns events with parameter not normalized
                            for (int i = 0; i < parameters.Count - 1; i++)
                                shatters.Add(new Line(edge_.PointAt(parameters[i]), edge_.PointAt(parameters[i + 1])));
                            foreach (Line shatter in shatters)
                            {
                                PointContainment checker = ctr.Contains(shatter.PointAt(0.5), Plane.WorldXY, _tol);
                                // be ware of the tolerance, if it is too small, the containment can be coincident
                                if (checker == PointContainment.Outside)
                                    union.Add(shatter);
                            }
                        }
                    }
                    else
                    {
                        // if not intersected but contained in the contour, do not drop it in the union
                        if (ctr.Contains(edge.PointAt(0.5), Plane.WorldXY, _tol) == PointContainment.Inside)
                            counter += 1;
                    }
                }
                if (counter == 0)
                    union.Add(edge);
            }

            // append contours to the union
            foreach (Polyline contour in contours)
            {
                Point3d[] vertices = contour.ToArray();
                for (int i = 0; i < vertices.Length - 1; i++)
                {
                    union.Add(new Line(vertices[i], vertices[i + 1]));
                }
            }

            // the input lines should be well shattered without crossings
            List<Line> edges = SkeletonPrune.Align(union, polys, tol_d, tol_theta, eta, 
                out List<Point3d> vts, out List<int> vts_degree, out List<Line> axes, out List<List<Line>> bundles);

            DA.SetDataList(0, union);
            DA.SetDataList(1, edges);
            DA.SetDataList(2, vts);
            DA.SetDataList(3, vts_degree);
            DA.SetDataList(4, axes);
            DA.SetDataTree(5, Util.ListToTree(bundles));
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
                return Properties.Resources.prune;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("46D5B4FF-E0FE-48EA-9392-A6E6CF5DCD1E");
    }
}