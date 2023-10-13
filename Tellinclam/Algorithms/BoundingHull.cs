using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Tellinclam.Algorithms
{
    public class BoundingHull
    {
        public static double Cross(Point3d O, Point3d A, Point3d B)
        {
            return (A.X - O.X) * (B.Y - O.Y) - (A.Y - O.Y) * (B.X - O.X);
        }

        public static List<Point3d> GetConvexHull(List<Point3d> points)
        {
            if (points == null)
                return null;

            if (points.Count() <= 1)
                return points;

            int n = points.Count(), k = 0;
            List<Point3d> H = new List<Point3d>(new Point3d[2 * n]);

            points.Sort((a, b) =>
                    a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

            // Build lower hull
            for (int i = 0; i < n; ++i)
            {
                while (k >= 2 && Cross(H[k - 2], H[k - 1], points[i]) <= 0)
                    k--;
                H[k++] = points[i];
            }

            // Build upper hull
            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && Cross(H[k - 2], H[k - 1], points[i]) <= 0)
                    k--;
                H[k++] = points[i];
            }

            return H.Take(k - 1).ToList();
        }

        static double Angle(Point3d p1, Point3d p2)
        {
            return Math.Atan2(p2[1] - p1[1], p2[0] - p1[0]);
        }

        static public Point3d PtCoordTrans(Point3d pt, double theta)
        {
            return new Point3d(
                pt.X * Math.Cos(theta) - pt.Y * Math.Sin(theta),
                pt.X * Math.Sin(theta) + pt.Y * Math.Cos(theta),
                0
                );
        }
        
        // WIP
        static public List<Point3d> GetMinimalRectHull(List<Point3d> pts)
        {
            List<Point3d> vts = GetConvexHull(pts);

            double min_area = double.PositiveInfinity;
            List<Point3d> hull = new List<Point3d>() { }; 

            for (int i = 0; i < vts.Count; i++)
            {
                int j = (i + 1) % vts.Count;

                double theta = Angle(vts[i], vts[j]);

                List<double> coord_x = new List<double>() { };
                List<double> coord_y = new List<double>() { };
                foreach (Point3d vt in vts)
                {
                    Point3d vt_trans = PtCoordTrans(vt, theta);
                    coord_x.Add(vt_trans.X);
                    coord_y.Add(vt_trans.Y);
                }

                coord_x.Sort(); coord_y.Sort();

                double area = (coord_x.Last() - coord_x[0]) * (coord_y.Last() - coord_x[0]);

                if (area < min_area)
                {
                    min_area = area;
                    Point3d p1 = PtCoordTrans(new Point3d(coord_x[0], coord_y[0], 0), -theta);
                    Point3d p2 = PtCoordTrans(new Point3d(coord_x.Last(), coord_y[0], 0), -theta);
                    Point3d p3 = PtCoordTrans(new Point3d(coord_x.Last(), coord_y.Last(), 0), -theta);
                    Point3d p4 = PtCoordTrans(new Point3d(coord_x[0], coord_y.Last(), 0), -theta);
                    hull = new List<Point3d>() { p1, p2, p3, p4 };
                }
            }

            return hull;
        }

        // WIP
        static public List<Point3d> GetMinFoilHull(List<Line> lines)
        {
            List<Point3d> pts = new List<Point3d>() { };
            foreach (Line line in lines)
            {
                pts.Add(line.PointAt(0));
                pts.Add(line.PointAt(1));
            }
            List<Point3d> hull = GetConvexHull(pts);
            int i = 0;
            while (i < hull.Count - 1 && i < 300)
            {
                int j = (i + 1) % hull.Count;
                int mid = -1;
                for (int k = 0; k < pts.Count; k++)
                {
                    if (hull[i].DistanceTo(pts[k]) == 0 || hull[j].DistanceTo(pts[k]) == 0)
                        continue;
                    int containment = 0;
                    Polyline patch = new Polyline(new List<Point3d>() { hull[i], pts[k], hull[j] });
                    if (Basic.IsPtInPoly(pts[k], new Polyline(hull), true) && !Basic.IsPtInPoly(pts[k], new Polyline(hull), false))
                        continue;
                    for (int l = 0; l < lines.Count; l++)
                    {
                        //LineCurve crv = new LineCurve(lines[l]);
                        //var ccx = Intersection.CurveCurve(crv, new PolylineCurve(patch),
                        //    RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                        //    RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        //if (ccx.Count > 0)
                        //    if (ccx[0].IsPoint)
                        //        containment++;

                        int intersection = 0;
                        var llx1 = Basic.SegIntersection(lines[l], new Line(patch[0], patch[2]),
                                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 
                                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                                out Point3d sect1, out double t1, out double t2);
                        if (llx1 == Basic.segIntersectEnum.Coincident ||
                            llx1 == Basic.segIntersectEnum.ColineOverlap ||
                            llx1 == Basic.segIntersectEnum.ColineAContainB ||
                            llx1 == Basic.segIntersectEnum.ColineBContainA)
                            intersection++;
                        var llx2 = Basic.SegIntersection(lines[l], new Line(patch[0], patch[1]),
                                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 
                                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                                out Point3d sect2, out double t3, out double t4);
                        if (llx2 == Basic.segIntersectEnum.IntersectOnBoth)
                            if (t4 > 0 && t4 < 1)
                                intersection++;
                        var llx3 = Basic.SegIntersection(lines[l], new Line(patch[1], patch[2]),
                                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 
                                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                                out Point3d sect3, out double t5, out double t6);
                        if (llx3 == Basic.segIntersectEnum.IntersectOnBoth)
                            if (t6 > 0 && t6 < 1)
                                intersection++;
                        //for (int m = 0; m < patch.Count; m++)
                        //{
                        //    int n = (m + 1) % patch.Count;
                        //    var llx = Basic.SegIntersection(lines[l], new Line(patch[m], patch[n]),
                        //        RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, out Point3d sect, out double t1, out double t2);
                        //    if (llx == Basic.segIntersectEnum.IntersectOnBoth)
                        //        if (t2 > 0 && t2 < 1)
                        //            intersection++;
                        //}
                        if (intersection > 0)
                            containment++;
                    }
                    if (containment == 0)
                    {
                        mid = k;
                        break;
                    }
                }
                if (mid >= 0)
                    hull.Insert(j, pts[mid]);
                i++;
            }
            return hull;
        }
    }
}
