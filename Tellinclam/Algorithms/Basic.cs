using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry.Intersect;
using Rhino;
using Grasshopper.Kernel.Geometry;
using Rhino.Render.DataSources;

namespace Tellinclam.Algorithms
{
    internal class Basic
    {
        public enum segIntersectEnum
        {
            IntersectOnBoth,
            IntersectOnA,
            IntersectOnB,
            IntersectOnLine,
            ColineDisjoint,
            ColineOverlap,
            ColineJoint,
            ColineAContainB,
            ColineBContainA,
            Parallel,
            Intersect,
            Coincident
        }

        static double _eps = 0.00001;

        /// <summary>
        /// Return the angle in degree (0~2PI) of two vector by calculating arctangent
        /// this return the clockwise angle from vec1 to vec2
        /// </summary>
        public static double VectorAngle2PI(Vector3d vec1, Vector3d vec2)
        {
            // for angle 0 ~ PI use Math.Atan
            // for angle 0 ~ 2PI use Math.Atan2
            double angle = Math.Atan2(vec1.Y, vec1.X) - Math.Atan2(vec2.Y, vec2.X);
            //angle = angle * 360 / (2 * Math.PI);
            if (angle < 0)
                angle += 2 * Math.PI;
            return angle;
        }

        /// <summary>
        /// Return the angle (0~PI) of two vector by calculating arccosin
        /// The two vector must be on the same plane.
        /// </summary>
        public static double VectorAnglePI(Vector3d vec1, Vector3d vec2)
        {
            double value = Math.Round(vec1 * vec2 / vec1.Length / vec2.Length, 6);
            double angle = Math.Acos(value);
            //angle = angle * 180 / Math.PI;
            return angle;
        }
        /// <summary>
        /// Return the angle (0~PI) of two vector by calculating arccosin
        /// </summary>
        public static double VectorAnglePI_2(Vector3d vec1, Vector3d vec2)
        {
            Vector3d normal = new Vector3d(0, 0, 1);
            double angle_delta = Vector3d.VectorAngle(vec1, vec2, normal);
            // VectorAngle2PI is clockwise angle, not counterclockwise
            //double angle_delta = VectorAngle2PI(vec1, vec2);
            if (Math.Abs(angle_delta) < 0.00001) angle_delta = 0;
            if (angle_delta > Math.PI) angle_delta = 2 * Math.PI - angle_delta;
            if (angle_delta > Math.PI / 2) angle_delta = Math.PI - angle_delta;
            return angle_delta;
        }

        public static Vector3d GetPendicularUnitVec(Vector3d vec, bool isClockwise)
        {
            Vector3d pendicularVec = new Vector3d(-vec.Y, vec.X, 0);
            if (isClockwise)
                pendicularVec = new Vector3d(vec.Y, -vec.X, 0);
            pendicularVec.Unitize();
            return pendicularVec;
        }

        /// <summary>
        /// Get the area of a simple polygon by the X, Y coordinates of vertices. This is the 
        /// actually the z-plane projection of the original polygon.
        /// </summary>
        public static double GetPolyArea(List<Point3d> pts)
        {
            var count = pts.Count;

            double area0 = 0;
            double area1 = 0;
            for (int i = 0; i < count; i++)
            {
                var x = pts[i].X;
                var y = i + 1 < count ? pts[i + 1].Y : pts[0].Y;
                area0 += x * y;

                var a = pts[i].Y;
                var b = i + 1 < count ? pts[i + 1].X : pts[0].X;
                area1 += a * b;
            }
            return Math.Abs(0.5 * (area0 - area1));
        }

        /// <summary>
        /// Check if the 2D polygon is clockwise (z coordinate is omitted)
        /// </summary>
        public static bool IsClockwise(Polyline pline)
        {
            var count = pline.Count;

            double area0 = 0;
            double area1 = 0;
            for (int i = 0; i < count; i++)
            {
                var x = pline[i].X;
                var y = i + 1 < count ? pline[i + 1].Y : pline[0].Y;
                area0 += x * y;

                var a = pline[i].Y;
                var b = i + 1 < count ? pline[i + 1].X : pline[0].X;
                area1 += a * b;
            }
            double ans = area0 - area1;
            if (ans < 0) return true;
            return false;
        }

        /// <summary>
        /// Point on the edge of a poly returns true. The poly includes the boundary
        /// </summary>
        public static bool IsPtInPoly(Point3d pt, Polyline pline, bool includeOn)
        {
            // Polyline inherits from Collection
            int GetQuadrant(Point3d v, Point3d _pt)
            {
                return v.X > _pt.X ? v.Y > _pt.Y ? 0 : 3 : v.Y > _pt.Y ? 1 : 2;
            }

            double X_intercept(Point3d pt1, Point3d pt2, double y)
            {
                return pt2.X - (pt2.Y - y) * ((pt1.X - pt2.X) / (pt1.Y - pt2.Y));
            }

            void AdjustDelta(ref int _delta, Point3d v, Point3d next_v, Point3d _pt)
            {
                switch (_delta)
                {
                    case 3: _delta = -1; break;
                    case -3: _delta = 1; break;
                    case 2:
                    case -2:
                        if (X_intercept(v, next_v, _pt.Y) > _pt.X)
                            _delta = -_delta;
                        break;
                }
            }

            int quad = GetQuadrant(pline[0], pt);
            int angle = 0;
            int onEdgeCounter = 0;
            int next_quad, delta;
            for (int i = 0; i < pline.Count; i++)
            {
                Point3d v = pline[i];
                Point3d next_v = pline[i + 1 < pline.Count ? i + 1 : 0];
                next_quad = GetQuadrant(next_v, pt);
                delta = next_quad - quad;

                AdjustDelta(ref delta, v, next_v, pt);
                angle = angle + delta;
                quad = next_quad;

                // more efficient methods are needed
                double distance = PtDistanceToSeg(pt, new Line(v, next_v), out Point3d plummet, out double stretch);
                if (distance < _eps && stretch >= 0 && stretch <= 1)
                    onEdgeCounter++;
            }
            if (includeOn)
                return onEdgeCounter > 0 || angle == 4 || angle == -4;
            else if (onEdgeCounter > 0)
                return false;
            else
                return angle == 4 || angle == -4;
        }

        // by default the polyline should be closed
        public static bool IsPolyInPoly(Polyline plineA, Polyline plineB)
        {
            foreach (Point3d pt in plineA)
                if (!IsPtInPoly(pt, plineB, false))
                    return false;
            // if any edge of polygon A intersects with polygon B, deny it
            for (int i = 0; i < plineA.Count - 1; i++)
            {
                Curve segA = new LineCurve(new Line(plineA[i], plineA[i + 1]));
                for (int j = 0; j < plineB.Count - 1; j++)
                {
                    Curve segB = new LineCurve(new Line(plineB[j], plineB[j + 1]));
                    var ccx = Intersection.CurveCurve(segA, segB, 0.0001, 0.0001);
                    if (ccx.Count > 0)
                        return false;
                }
            }
            return true;
        }

        //public static List<Curve> ShatterCrvs(List<Curve> crvs)
        //{
        //    List<Curve> shatteredCrvs = new List<Curve>();

        //    for (int i = 0; i <= crvs.Count - 1; i++)
        //    {
        //        List<double> breakParams = new List<double>();
        //        for (int j = 0; j <= crvs.Count - 1; j++)
        //        {
        //            if (i != j)
        //            {
        //                CurveIntersections CI = Intersection.CurveCurve(
        //                  crvs[i], crvs[j], RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
        //                  RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
        //                foreach (IntersectionEvent IE in CI)
        //                {
        //                    breakParams.Add(IE.ParameterA);
        //                }
        //            }
        //        }
        //        shatteredCrvs.AddRange(crvs[i].Split(breakParams));
        //    }
        //    return shatteredCrvs;
        //}

        /// <summary>
        /// this returns closest point of 3D lines, not intersection point
        /// </summary>
        //public static List<Point3d> GetIntersectionOfLines(List<Line> lines)
        //{
        //    List<Point3d> pts = new List<Point3d>() { };
        //    for (int i = 0; i < lines.Count - 1; i++)
        //    {
        //        for (int j = 0; j < lines.Count - 1; j++)
        //        {
        //            if (i != j)
        //            {
        //                if (Intersection.LineLine(lines[i], lines[j], 
        //                    out double paramA, out double paramB, 
        //                    RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, true))
        //                {
        //                    pts.Add(lines[i].PointAt(paramA));
        //                }
        //            }
        //        }
        //    }
        //    return pts;
        //}
        

        public static segIntersectEnum SegIntersection(Point3d p1, Point3d p2, Point3d p3, Point3d p4, 
            double tol_theta, double tol_d, 
            out Point3d intersection, out double t1, out double t2)
        {
            // represents stretch vector of seg1 vec1 = (dx12, dy12)
            double dx12 = p2.X - p1.X;
            double dy12 = p2.Y - p1.Y;
            // represents stretch vector of seg2 vec2 = (dx34, dy34)
            double dx34 = p4.X - p3.X;
            double dy34 = p4.Y - p3.Y;

            segIntersectEnum intersect;
            intersection = new Point3d(double.NaN, double.NaN, 0);

            // checker as cross product of vec1 and vec2
            double denominator = dy12 * dx34 - dx12 * dy34;
            // co-line checker as cross product of (p3 - p1) and (p2 - p1)
            // this value represents the area of the parallelogram.
            // If near to zero,  the parallel edges are co-lined
            double stretch = (p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12;
            t1 = 0;
            t2 = 0;

            if (Math.Abs(denominator) < tol_theta && Math.Abs(stretch) > tol_d)
                return segIntersectEnum.Parallel;
            if (Math.Abs(denominator) < tol_theta && Math.Abs(stretch) < tol_d)
            {
                // express endpoints of seg2 in terms of seg1 parameter
                double s1 = ((p3.X - p1.X) * dx12 + (p3.Y - p1.Y) * dy12) / (dx12 * dx12 + dy12 * dy12);
                double s2 = s1 + (dx12 * dx34 + dy12 * dy34) / (dx12 * dx12 + dy12 * dy12);
                if (s1 > s2)
                {
                    Util.Swap(ref s1, ref s2);
                    Util.Swap(ref p3, ref p4);
                }

                if (Math.Abs(s1) < tol_d) s1 = 0;
                if (Math.Abs(s2) < tol_d) s2 = 0;
                if (Math.Abs(s1 - 1) < tol_d) s1 = 1;
                if (Math.Abs(s2 - 1) < tol_d) s2 = 1;

                if (s1 == 0 && s2 == 1)
                    return segIntersectEnum.Coincident;
                if (s1 > 1 || s2 < 0)
                    return segIntersectEnum.ColineDisjoint;
                if ((s1 >= 0 && s1 <= 1) || (s2 >= 0 && s2 <= 1))
                    if ((s1 >= 0 && s1 <= 1) && (s2 >= 0 && s2 <= 1))
                        return segIntersectEnum.ColineAContainB;
                    else
                    {
                        if (s1 == 1)
                            return segIntersectEnum.ColineJoint;
                        if (s1 == 0)
                            return segIntersectEnum.ColineBContainA;
                        if (s2 == 0)
                            return segIntersectEnum.ColineJoint;
                        if (s2 == 1)
                            return segIntersectEnum.ColineBContainA;
                        return segIntersectEnum.ColineOverlap;
                    }
                else
                {
                    return segIntersectEnum.ColineBContainA;
                }
            }

            intersect = segIntersectEnum.IntersectOnLine;

            t1 = ((p1.X - p3.X) * dy34 + (p3.Y - p1.Y) * dx34) / denominator;
            t2 = ((p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12) / -denominator;
            //fractile = t1;

            if (t1 > 10000 || t2 > 10000)
            {
                //Debug.Print($"GBMethod:: Wrong at intersection checking");
            }

            if ((t1 >= 0 - tol_d) && (t1 <= 1 + tol_d))
                intersect = segIntersectEnum.IntersectOnA;
            if ((t2 >= 0 - tol_d) && (t2 <= 1 + tol_d))
                intersect = segIntersectEnum.IntersectOnB;
            if ((t1 >= 0 - tol_d) && (t1 <= 1 + tol_d) && (t2 >= 0 - tol_d) && (t2 <= 1 + tol_d))
                intersect = segIntersectEnum.IntersectOnBoth;

            intersection = new Point3d(p1.X + dx12 * t1, p1.Y + dy12 * t1, 0);

            return intersect;
        }
        public static segIntersectEnum SegIntersection(Line a, Line b, 
            double tol_theta, double tol_d, 
            out Point3d intersection, out double t1, out double t2)
        {
            Point3d p1 = a.PointAt(0);
            Point3d p2 = a.PointAt(1);
            Point3d p3 = b.PointAt(0);
            Point3d p4 = b.PointAt(1);
            intersection = new Point3d();
            return SegIntersection(p1, p2, p3, p4, tol_theta, tol_d, out intersection, out t1, out t2);
        }

        public static List<Point3d> GetIntersectionOfLines(List<Line> lines)
        {
            List<Point3d> pts = new List<Point3d>() { };
            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = 0; j < lines.Count; j++)
                {
                    if (i != j)
                    {
                        var llx = SegIntersection(lines[i], lines[j],
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                            out Point3d intersection, out double t1, out double t2);
                        if (llx == segIntersectEnum.Intersect || llx == segIntersectEnum.IntersectOnA ||
                            llx == segIntersectEnum.IntersectOnB || llx == segIntersectEnum.IntersectOnBoth ||
                            llx == segIntersectEnum.IntersectOnLine)
                            pts.Add(intersection);
                    }
                }
            }
            return pts;
        }

        public static List<Line> BreakLineAtEvaluations(Line line, List<double> evas)
        {
            List<double> _evas = new List<double>() { 0, 1 };
            foreach (double eva in evas)
            {
                if (eva > 0 && eva < 1)
                    _evas.Add(eva);
            }
            _evas.Sort();
            List<Line> shatters = new List<Line>() { };
            for (int i = 0; i < _evas.Count - 1; i++)
            {
                Point3d start = line.PointAt(0) + line.Direction * _evas[i];
                Point3d end = line.PointAt(0) + line.Direction * _evas[i + 1];
                shatters.Add(new Line(start, end));
            }
            return shatters;
        }

        public static List<Line> BreakLinesAtIntersection(List<Line> lines)
        {
            List<Line> shatters = new List<Line>() { };
            for (int i = 0; i < lines.Count; i++)
            {
                List<double> evas = new List<double>() { };
                for (int j = 0; j < lines.Count; j++)
                {
                    if (i != j)
                    {
                        var llx = SegIntersection(lines[i], lines[j],
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                            out Point3d intersection, out double t1, out double t2);
                        // aware of the coline, overlapping situation
                        if (llx == segIntersectEnum.IntersectOnBoth)
                            evas.Add(t1);
                    }
                }
                shatters.AddRange(BreakLineAtEvaluations(lines[i], evas));
            }
            return shatters;
        }

        /// <summary>
        /// Calculate the distance between the point and the segment.
        /// Output the projected point and the ratio that the point is evaluated by the segment.
        /// </summary>
        public static double PtDistanceToSeg(Point3d pt, Line line,
          out Point3d plummet, out double stretch)
        {
            double dx = line.PointAt(1).X - line.PointAt(0).X;
            double dy = line.PointAt(1).Y - line.PointAt(0).Y;
            Point3d origin = line.PointAt(0);

            if ((dx == 0) && (dy == 0)) // zero length segment
            {
                plummet = origin;
                stretch = 0;
                dx = pt.X - origin.X;
                dy = pt.Y - origin.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            // Calculate the t that minimizes the distance.
            stretch = ((pt.X - origin.X) * dx + (pt.Y - origin.Y) * dy) /
              (dx * dx + dy * dy);

            plummet = new Point3d(origin.X + stretch * dx, origin.Y + stretch * dy, 0);
            //plummet = new line.PointAt(stretch);
            dx = pt.X - plummet.X;
            dy = pt.Y - plummet.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double PtDistanceToRay(
            Point3d pt, Point3d origin, Vector3d vec,
            out Point3d plummet, out double stretch)
        {
            double dx = vec.X;
            double dy = vec.Y;

            // Calculate the t that minimizes the distance.
            double t = ((pt.X - origin.X) * dx + (pt.Y - origin.Y) * dy) /
              (dx * dx + dy * dy);

            Point3d closest = new Point3d(origin.X + t * dx, origin.Y + t * dy, 0);
            plummet = closest;
            dx = pt.X - (origin.X + t * dx);
            dy = pt.Y - (origin.Y + t * dy);
            //stretch = t * Math.Sqrt(dx * dx + dy * dy);
            stretch = closest.DistanceTo(origin);
            if (t < 0)
                stretch = -stretch;
            //Rhino.RhinoApp.WriteLine("this distance is: " + stretch.ToString());
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// By default, this function rotate the line segment arount the mid point 
        /// to stay in line with the givin vector
        /// </summary>
        public static Line SegProjDirection(Line line, Vector3d dir)
        {
            Point3d pivot = (line.PointAt(0) + line.PointAt(1)) / 2;

            double dist1 = PtDistanceToRay(line.PointAt(0), pivot, dir, out Point3d start, out double s1);
            double dist2 = PtDistanceToRay(line.PointAt(1), pivot, dir, out Point3d end, out double s2);

            return new Line(start, end);
        }

        /// <summary>
        /// Calculate the distance between two line segments if they are parallel (with 1 degree tolerance).
        /// Output the length of their overlapping region. Output the projected line segment as the overlapping region.
        /// </summary>
        public static double SegProjectToSeg(Line subj, Line obj, double tol_theta, out double overlap, out Line proj)
        {
            Point3d start = subj.PointAt(0);
            Point3d end = subj.PointAt(1);
            double angle_delta = VectorAnglePI_2(subj.Direction, obj.Direction);

            double d1 = PtDistanceToSeg(start, obj, out Point3d plummet1, out double t1);
            double d2 = PtDistanceToSeg(end, obj, out Point3d plummet2, out double t2);
            if (t1 > t2)
                Util.Swap(ref t1, ref t2);
            if (t2 < 0 || t1 > 1)
                overlap = 0;
            else if (t1 < 0 && t2 > 1)
                overlap = 1;
            else if (t1 < 0)
                overlap = t2;
            else if (t2 > 1)
                overlap = 1 - t1;
            else
                overlap = t2 - t1;
            proj = new Line(plummet1, plummet2);

            if (angle_delta < tol_theta)
            {
                return d1 <= d2 ? d1 : d2;
            }
            else
            {
                return 0;
            }

        }

        //public static segIntersectEnum SegFusion(Line a, Line b, double tol_d, double tol_theta, out Line fusion)
        //{
        //    Point3d p1 = a.PointAt(0);
        //    Point3d p2 = a.PointAt(1);
        //    Point3d p3 = b.PointAt(0);
        //    Point3d p4 = b.PointAt(1);
        //    // represents stretch vector of seg1 vec1 = (dx12, dy12)
        //    double dx12 = p2.X - p1.X;
        //    double dy12 = p2.Y - p1.Y;
        //    // represents stretch vector of seg2 vec2 = (dx34, dy34)
        //    double dx34 = p4.X - p3.X;
        //    double dy34 = p4.Y - p3.Y;

        //    fusion = new Line(Point3d.Origin, Point3d.Origin);

        //    // checker as cross product of vec1 and vec2
        //    double denominator = dy12 * dx34 - dx12 * dy34;
        //    // co-line checker as cross product of (p3 - p1) and vec1/vec
        //    double stretch = (p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12;
        //    // check the gap between two almost parallel segments
        //    double gap = SegProjectToSeg(a, b, tol_theta, out double overlap, out Line proj);
        //    //if (gap < 0.000001 && overlap > 0.000001)
        //    //{
        //    //    Debug.Print("GBMethod:: experiencing the gap");
        //    //    double d1 = PtDistanceToSeg(p1, b, out gbXYZ plummet1, out double s1);
        //    //    double d2 = PtDistanceToSeg(p1, b, out gbXYZ plummet2, out double s2);
        //    //    p1 = plummet1;
        //    //    p2 = plummet2;
        //    //}

        //    if (Math.Abs(denominator) < _eps && Math.Abs(stretch) > _eps)
        //    {
        //        return segIntersectEnum.Parallel;
        //    }
        //    if (Math.Abs(denominator) < _eps && Math.Abs(stretch) < _eps)
        //    {
        //        //Debug.Print($"GBMethod:: Seg fused {a} {b}");
        //        // express endpoints of seg2 in terms of seg1 parameter
        //        double s1 = ((p3.X - p1.X) * dx12 + (p3.Y - p1.Y) * dy12) / (dx12 * dx12 + dy12 * dy12);
        //        double s2 = s1 + (dx12 * dx34 + dy12 * dy34) / (dx12 * dx12 + dy12 * dy12);
        //        if (s1 > s2)
        //        {
        //            Util.Swap(ref s1, ref s2);
        //            Util.Swap(ref p3, ref p4);
        //        }

        //        if (Math.Abs(s1) < tol_d) s1 = 0;
        //        if (Math.Abs(s2) < tol_d) s2 = 0;
        //        if (Math.Abs(s1 - 1) < tol_d) s1 = 1;
        //        if (Math.Abs(s2 - 1) < tol_d) s2 = 1;

        //        if (s1 == 0 && s2 == 1)
        //        {
        //            fusion = new Line(p1, p2);
        //            return segIntersectEnum.Coincident;
        //        }

        //        if (s1 > 1 || s2 < 0)
        //            return segIntersectEnum.ColineDisjoint;

        //        if ((s1 >= 0 && s1 <= 1) || (s2 >= 0 && s2 <= 1))
        //            if ((s1 >= 0 && s1 <= 1) && (s2 >= 0 && s2 <= 1))
        //            {
        //                fusion = new Line(p1, p2);
        //                return segIntersectEnum.ColineAContainB;
        //            }
        //            else
        //            {
        //                if (s1 == 1)
        //                {
        //                    fusion = new Line(p1, p4);
        //                    return segIntersectEnum.ColineJoint;
        //                }
        //                if (s1 == 0)
        //                {
        //                    fusion = new Line(p3, p4);
        //                    return segIntersectEnum.ColineBContainA;
        //                }
        //                if (s2 == 0)
        //                {
        //                    fusion = new Line(p3, p2);
        //                    return segIntersectEnum.ColineJoint;
        //                }
        //                if (s2 == 1)
        //                {
        //                    fusion = new Line(p3, p4);
        //                    return segIntersectEnum.ColineBContainA;
        //                }
        //                if (s1 > 0 && s1 < 1)
        //                {
        //                    fusion = new Line(p1, p4);
        //                    return segIntersectEnum.ColineOverlap;
        //                }
        //                if (s2 > 0 && s2 < 1)
        //                {
        //                    fusion = new Line(p3, p2);
        //                    return segIntersectEnum.ColineOverlap;
        //                }
        //            }
        //        else
        //        {
        //            fusion = new Line(p3, p4);
        //            return segIntersectEnum.ColineBContainA;
        //        }
        //    }

        //    return segIntersectEnum.Intersect;
        //}

        /// <summary>
        /// Fuse line segments if they are co-linear and overlapping.
        /// </summary>
        //public static List<Line> SegsFusion(List<Line> segs, double tol_theta)
        //{
        //    List<Line> _segs = new List<Line>();
        //    // 0 length segment that happens to be an intersection point
        //    // will be colined with two joining segments not parallel
        //    foreach (Line seg in segs)
        //        if (seg.Length > _eps)
        //            _segs.Add(new Line(seg.PointAt(0), seg.PointAt(1)));

        //    for (int i = _segs.Count - 1; i >= 1; i--)
        //    {
        //        for (int j = i - 1; j >= 0; j--)
        //        {
        //            segIntersectEnum result = SegFusion(_segs[i], _segs[j], _eps, tol_theta, out Line fusion);
        //            if (result == segIntersectEnum.ColineAContainB ||
        //                result == segIntersectEnum.ColineBContainA ||
        //                result == segIntersectEnum.ColineJoint ||
        //                result == segIntersectEnum.ColineOverlap)
        //            {
        //                if (fusion != null)
        //                {
        //                    _segs[j] = fusion;
        //                    _segs.RemoveAt(i);
        //                    break;
        //                }
        //            }
        //        }
        //    }
        //    return _segs;
        //}

        public static List<Line> SegsWelding(List<Line> segs, double tol_off, double tol_gap, double tol_theta)
        {
            List<Line> linePool = new List<Line>();
            foreach (Line line in segs)
            {
                linePool.Add(line);
            }
            List<Line> weldings = new List<Line>();
            List<List<Line>> lineGroups = new List<List<Line>>() { };
            while (linePool.Count > 0)
            {
                List<Line> lineGroup = new List<Line>() { linePool[0] };
                //Rhino.RhinoApp.Write("Initializing... "); displayCurve(crvPool[0]);
                linePool.RemoveAt(0);
                for (int i = 0; i < lineGroup.Count; i++)
                {
                    //Rhino.RhinoApp.WriteLine("Iteration... " + i.ToString());
                    //if (i >= crvGroup.Count - 1) { break; }
                    for (int j = linePool.Count - 1; j >= 0; j--)
                    {
                        var llx = SegIntersection(lineGroup[i], linePool[j], _eps, _eps, 
                            out Point3d sect, out double t1, out double t2);
                        if (llx == segIntersectEnum.ColineAContainB || 
                            llx == segIntersectEnum.ColineBContainA || 
                            llx == segIntersectEnum.ColineJoint || 
                            llx == segIntersectEnum.ColineOverlap || 
                            llx == segIntersectEnum.Coincident)
                        {
                            lineGroup.Add(linePool[j]);
                            linePool.RemoveAt(j);
                        }
                        else if (llx == segIntersectEnum.ColineDisjoint)
                        {
                            double d1 = lineGroup[i].PointAt(0).DistanceTo(linePool[j].PointAt(0));
                            double d2 = lineGroup[i].PointAt(1).DistanceTo(linePool[j].PointAt(0));
                            double d3 = lineGroup[i].PointAt(0).DistanceTo(linePool[j].PointAt(1));
                            double d4 = lineGroup[i].PointAt(1).DistanceTo(linePool[j].PointAt(1));
                            if (d1 < tol_gap || d2 < tol_gap || d3 < tol_gap || d4 < tol_gap)
                            {
                                lineGroup.Add(linePool[j]);
                                linePool.RemoveAt(j);
                            }
                        }
                        else
                        {
                            double distance = SegDistanceToSeg(lineGroup[i], linePool[j],
                                out double overlap, out Line proj);
                            double delta_angle = VectorAnglePI_2(lineGroup[i].Direction,
                                linePool[j].Direction);
                            // BUG note here the overlap does not allow tol_gap to exist
                            if (distance < tol_off && delta_angle < tol_theta && overlap > 0)
                            {
                                lineGroup.Add(linePool[j]);
                                linePool.RemoveAt(j);
                            }
                        }
                    }
                }
                lineGroups.Add(lineGroup);
            }

            foreach (List<Line> lineGroup in lineGroups)
            {
                if (lineGroup.Count == 1)
                    weldings.Add(lineGroup[0]);
                else
                {
                    List<double> evas = new List<double>() { 0, 1 };
                    for (int i = 1; i < lineGroup.Count; i++)
                    {
                        double dist1 = PtDistanceToSeg(lineGroup[i].PointAt(0), lineGroup[0],
                            out Point3d p1, out double s1);
                        double dist2 = PtDistanceToSeg(lineGroup[i].PointAt(1), lineGroup[0],
                            out Point3d p2, out double s2);
                        evas.Add(s1); evas.Add(s2);
                    }
                    evas.Sort();
                    weldings.Add(new Line(
                        lineGroup[0].Direction * evas[0] + lineGroup[0].PointAt(0),
                        lineGroup[0].Direction * evas.Last() + lineGroup[0].PointAt(0)));
                }
            }
            return weldings;
        }


        //public static List<Line> ShatterLines(List<Line> lines)
        //{
        //    List<Line> shatteredLines = new List<Line>() { };
        //    List<Curve> crvs = new List<Curve>() { };
        //    foreach (Line line in lines)
        //        crvs.Add(new LineCurve(line));

        //    List<Curve> shatters = ShatterCrvs(crvs);

        //    foreach (Curve crv in shatters)
        //    {
        //        if (crv.IsLinear())
        //            shatteredLines.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
        //    }
        //    return shatteredLines;
        //}

        public static double SegDistanceToSeg(Line subj, Line obj, out double overlap, out Line proj)
        {
            Point3d start = subj.PointAt(0);
            Point3d end = subj.PointAt(1);
            double angle = VectorAnglePI_2(subj.Direction, obj.Direction);
            //Debug.Print($"GBMethod::SegDistanceToSeg check angle {angle}");

            double d1 = PtDistanceToSeg(start, obj, out Point3d plummet1, out double t1);
            double d2 = PtDistanceToSeg(end, obj, out Point3d plummet2, out double t2);
            if (t1 > t2)
                Util.Swap(ref t1, ref t2);
            if (t2 < 0 || t1 > 1)
                overlap = 0;
            else if (t1 < 0 && t2 > 1)
                overlap = 1;
            else if (t1 < 0)
                overlap = t2;
            // overlap = 0 - t1;
            else if (t2 > 1)
                overlap = 1 - t1;
            // overlap = 1 - t2;
            else
                overlap = t2 - t1;
            proj = new Line(plummet1, plummet2);

            var llx = SegIntersection(subj, obj, _eps, _eps, out Point3d sect, out double _t1, out double _t2);
            if (llx == segIntersectEnum.Parallel ||
                llx == segIntersectEnum.IntersectOnLine)
                return (d1 + d2) / 2;
            else
                return 0;

            //return d1 <= d2 ? d1 : d2;
            //else
            //{
            //    proj = new Line(Point3d.Origin, Point3d.Origin);
            //    overlap = 0;
            //    return double.PositiveInfinity;
            //}

        }

        public static List<Line> RemoveDupLines(List<Line> lines, double tol, out List<int> ids)
        {
            ids = new List<int>();
            if (lines.Count == 0)
                return lines;
            
            List<Line> lines_ = new List<Line>() { };
            foreach (Line line in lines) 
            { 
                if (line != null)
                    if (line.IsValid)
                        lines_.Add(line); 
            }
            for (int i = lines_.Count - 1; i >= 1; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    double distance_1 = lines_[i].PointAt(0).DistanceTo(lines_[j].PointAt(0));
                    double distance_2 = lines_[i].PointAt(0).DistanceTo(lines_[j].PointAt(1));
                    double distance_3 = lines_[i].PointAt(1).DistanceTo(lines_[j].PointAt(0));
                    double distance_4 = lines_[i].PointAt(1).DistanceTo(lines_[j].PointAt(1));
                    if (distance_1 < tol && distance_4 < tol ||
                        distance_2 < tol && distance_3 < tol)
                    {
                        lines_.RemoveAt(i);
                        ids.Add(i);
                        break;
                    }
                }
            }
            return lines_;
        }

        public static List<Point3d> RemoveDupPoints(List<Point3d> pts, double tol)
        {
            if (pts.Count == 0)
                return pts;

            List<Point3d> pts_ = new List<Point3d>() { };
            foreach (Point3d pt in pts) 
            { 
                if (pt != null)
                    if (pt.IsValid)
                        pts_.Add(pt); 
            }
            for (int i = pts_.Count - 1; i >= 1; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    double distance = pts_[i].DistanceTo(pts_[j]);
                    if (distance < tol)
                    {
                        pts_.RemoveAt(i);
                        break;
                    }
                }
            }
            return pts_;
        }

        public static List<Point3d> PtProjToXY(List<Point3d> pts)
        {
            List<Point3d> pts_ = new List<Point3d>() { };
            foreach (Point3d pt in pts)
                pts_.Add(new Point3d(pt.X, pt.Y, 0));
            return pts_;
        }
    }
}
