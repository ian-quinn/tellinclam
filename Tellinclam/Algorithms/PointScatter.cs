using CGAL.Wrapper;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tellinclam.Algorithms
{
    internal class PointScatter
    {
        public static List<Point3d> GetLayout(Polyline pline, List<double> sizes, double coolR, double scaleF, int maxIter, 
            bool flag_tune)
        {
            List<Point3d> pts = new List<Point3d>() { };
            for (int i = 0; i < pline.Count - 1; i++)
            {
                pts.Add(pline[i]);
            }

            List<Point3d> vtx = OptBoundingBox.ObbWithPoint2d(Basic.PtProjToXY(pts));
            Polyline box = new Polyline(new Point3d[] { vtx[0], vtx[1], vtx[2], vtx[3], vtx[0] });

            List<Line> sketches = GetSeeds(new Line(vtx[0], vtx[1]), new Line(vtx[1], vtx[2]), pline, sizes);

            int[,] adjMat = SkeletonPrune.GetAdjMat(sketches, out Point3d[] seeds, out int[] degrees);

            if (flag_tune)
            {
                Point3d[] seeds_adj = ForceDirectedPlacement(seeds.ToArray(), adjMat, pline, coolR, scaleF, maxIter);
                return seeds_adj.ToList();
            }
            else
                return seeds.ToList();
        }

        public static List<Line> GetSeeds(Line axis_x, Line axis_y, Polyline bound, List<double> sizes)
        {
            Curve boundCrv = new PolylineCurve(bound);
            double areaPoly = Basic.GetPolyArea(bound.ToList());

            // traverse all sizes of vent and all possible configurations
            // col: | size | xtick | ytick | overlapping | coverage |
            // row: each iteration (max sizes.Count * 9)
            double[,] sizingMat = new double[sizes.Count * 9, 5];
            int iteration = 0;
            foreach (double size in sizes)
            {
                List<int> xticks = new List<int>() { };
                List<int> yticks = new List<int>() { };

                xticks.Add((int)Math.Round(axis_x.Length / size));
                if (xticks[0] >= 2) xticks.Add(xticks[0] - 1);
                xticks.Add(xticks[0] + 1);

                yticks.Add((int)Math.Round(axis_y.Length / size));
                if (yticks[0] >= 2) yticks.Add(yticks[0] - 1);
                yticks.Add(yticks[0] + 1);

                foreach (int xtick in xticks)
                {
                    foreach (int ytick in yticks)
                    {
                        List<Line> grids = new List<Line>() { };
                        for (int i = 0; i < 2 * ytick; i++)
                        {
                            if (i % 2 == 0)
                                continue;
                            Line grid_y = new Line(axis_x.PointAt(0), axis_x.PointAt(1));
                            var xf = Transform.Translation(axis_y.Direction / (2 * ytick) * i);
                            grid_y.Transform(xf);
                            grids.Add(grid_y);
                        }
                        for (int i = 0; i < 2 * xtick; i++)
                        {
                            if (i % 2 == 0)
                                continue;
                            Line grid_x = new Line(axis_y.PointAt(0), axis_y.PointAt(1));
                            var xf = Transform.Translation(-axis_x.Direction / (2 * xtick) * i);
                            grid_x.Transform(xf);
                            grids.Add(grid_x);
                        }

                        List<Point3d> sections = Basic.GetIntersectionOfLines(grids);
                        List<Curve> circles = new List<Curve>() { };
                        foreach (Point3d section in sections)
                        {
                            if (Basic.IsPtInPoly(section, bound, true))
                            {
                                Circle ventR = new Circle(section, size / 2);
                                circles.Add(new ArcCurve(ventR));
                            }
                        }
                        Curve[] unions = Curve.CreateBooleanUnion(circles, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (unions.Length == 0) continue;
                        Brep[] uniContours = Brep.CreatePlanarBreps(unions[0], RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (uniContours.Length == 0) continue;
                        double overlappingRate = uniContours[0].GetArea() + 4 * Math.PI * size * size / areaPoly;
                        Curve[] subs = Curve.CreateBooleanDifference(boundCrv, unions[0], RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (subs.Length == 0) continue;
                        Brep[] subContours = Brep.CreatePlanarBreps(unions[0], RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (subContours.Length == 0) continue;
                        double coverageRate = subContours[0].GetArea() / areaPoly;

                        sizingMat[iteration, 0] = size;
                        sizingMat[iteration, 1] = xtick;
                        sizingMat[iteration, 2] = ytick;
                        sizingMat[iteration, 3] = overlappingRate;
                        sizingMat[iteration, 4] = coverageRate;
                        iteration++;
                    }
                }
            }

            int sizingId = -1;
            double maxCoverageOverlapRatio = 0;
            for (int i = 0; i < sizingMat.GetLength(0); i++)
            {
                if (sizingMat[i, 3] / sizingMat[i, 4] > maxCoverageOverlapRatio)
                {
                    maxCoverageOverlapRatio = sizingMat[i, 3] / sizingMat[i, 4];
                    sizingId = i;
                }
            }

            // try calculate the final layout

            List<Line> slices = new List<Line>() { };
            for (int i = 0; i < 2 * sizingMat[sizingId, 2]; i++)
            {
                if (i % 2 == 0)
                    continue;
                Line grid_y = new Line(axis_x.PointAt(0), axis_x.PointAt(1));
                var xf = Transform.Translation(axis_y.Direction / (2 * sizingMat[sizingId, 2]) * i);
                grid_y.Transform(xf);
                slices.Add(grid_y);
            }
            for (int i = 0; i < 2 * sizingMat[sizingId, 1]; i++)
            {
                if (i % 2 == 0)
                    continue;
                Line grid_x = new Line(axis_y.PointAt(0), axis_y.PointAt(1));
                var xf = Transform.Translation(-axis_x.Direction / (2 * sizingMat[sizingId, 1]) * i);
                grid_x.Transform(xf);
                slices.Add(grid_x);
            }

            List<Line> edges = Basic.BreakLinesAtIntersection(slices);
            int[,] adjMat = Algorithms.SkeletonPrune.GetAdjMat(edges, out Point3d[] vts,
                out int[] degrees);
            for (int i = vts.Length - 1; i >= 0; i--)
            {
                if (degrees[i] == 1 || !Basic.IsPtInPoly(vts[i], bound, true))
                    vts[i] = new Point3d(0, 0, 1);
            }
            List<Line> sketch = SkeletonPrune.RegenEdges(vts, adjMat);
            return sketch;
        }

        public static Point3d[] ForceDirectedPlacement(Point3d[] vtx, int[,] adjMat, Polyline bound, 
            double coolingRate, double scalingFactor, int maxIteration)
        {
            // during the displacement, the graph edge relationship will not change
            int iteration = 0;

            bool flag_equilibrium = false;
            double k = scalingFactor * Math.Sqrt(Basic.GetPolyArea(bound.ToList()) / vtx.Length);
            double t = k;

            Vector3d[] displacements = new Vector3d[vtx.Length];

            while (!flag_equilibrium && iteration < maxIteration)
            {
                PolylineCurve bound_crv = new PolylineCurve(bound);

                for (int i = 0; i < displacements.Length; i++)
                {
                    displacements[i] = new Vector3d(0, 0, 0);
                }

                // calculate attractive force
                for (int i = 0; i < vtx.Length; i++)
                {
                    List<int> adj_vtx_ids = new List<int>() { };
                    for (int j = 0; j < adjMat.GetLength(1); j++)
                    {
                        if (adjMat[i, j] >= 0)
                            adj_vtx_ids.Add(j);
                    }
                    foreach (int id in adj_vtx_ids)
                    {
                        // note the force is pointing from vtx_[i] to vtx_[id]
                        Vector3d delta = vtx[id] - vtx[i];
                        //displacements[i] += (delta / delta.Length) * Math.Pow(delta.Length, 2) / k;
                        displacements[i] += (delta / delta.Length) * delta.Length / k;
                    }
                    displacements[i].Z = 0;
                }

                // calculate repulsive force from other vertices
                for (int i = 0; i < vtx.Length; i++)
                {
                    for (int j = 0; j < vtx.Length; j++)
                    {
                        if (i != j)
                        {
                            // the repulsive force exists only when the two points can view each other
                            LineCurve view = new LineCurve(new Line(vtx[i], vtx[j]));
                            CurveIntersections events = Intersection.CurveCurve(view, bound_crv,
                                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                            // there must be two intersections if the view gets blocked
                            if (events.Count > 1)
                                continue;

                            // note the force is pointing backward
                            Vector3d delta = vtx[j] - vtx[i];
                            displacements[i] += -(delta / delta.Length) * Math.Pow(k, 2) / delta.Length;
                            //displacements[i] += -(delta / delta.Length) * k / delta.Length;
                        }
                    }
                    displacements[i].Z = 0;
                }

                // calculate repulsive from boundaries
                // populate edges of the boundary
                
                List<Line> edges = new List<Line>() { };
                for (int i = 0; i < bound.Count - 1; i++)
                    edges.Add(new Line(bound[i], bound[i + 1]));
                for (int i = 0; i < vtx.Length; i++)
                {
                    for (int j = 0; j < edges.Count; j++)
                    {
                        Point3d pt_nearest = edges[j].ClosestPoint(vtx[i], true);
                        // the repulsive force exists only when the two points can view each other
                        LineCurve view = new LineCurve(new Line(vtx[i], pt_nearest));
                        CurveIntersections events = Intersection.CurveCurve(view, bound_crv,
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (events.Count > 1)
                            continue;

                        // note the force is pointing backward
                        Vector3d delta = pt_nearest - vtx[i];
                        //displacements[i] += -(delta / delta.Length) * k / delta.Length;
                        displacements[i] += -(delta / delta.Length) * Math.Pow(k, 2) / delta.Length;
                    }
                    displacements[i].Z = 0;
                }
                

                // really?
                // take this as a safety lock to avoid meaningless iterations
                double max_dist = 0;
                for (int i = 0; i < displacements.Length; i++)
                {
                    if (displacements[i].Length > max_dist)
                        max_dist = displacements[i].Length;
                }
                if (max_dist > 3 * k)
                    flag_equilibrium = true;

                // update the positions of vertices
                // the max distance is limited by temperature t (a certain length)
                // this value gets smaller and smaller, thus 'cooling' the whole system
                for (int i = 0; i < vtx.Length; i++)
                {
                    // check if the movement will bring the vertex outside the frame
                    // if it does, pick their intersection point as the final movement result
                    Point3d testPt = vtx[i] + displacements[i] / displacements[i].Length * 
                        Math.Min(displacements[i].Length, t);
                    if (!Basic.IsPtInPoly(testPt, bound, true))
                    {
                        Curve trace = new LineCurve(new Line(testPt, vtx[i]));
                        var CI = Intersection.CurveCurve(trace, bound_crv, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (CI != null && CI.Count > 0)
                        {
                            Point3d ptOnEdge = CI[0].PointA;
                            vtx[i] = ptOnEdge;
                            continue;
                        }
                    }
                    vtx[i] += displacements[i] / displacements[i].Length *
                        Math.Min(displacements[i].Length, t);
                }

                // update the temperature
                t = Math.Max(t * coolingRate, 0.001);

                iteration++;
            }


            return vtx;
        }
    }
}
