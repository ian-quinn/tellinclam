using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry.Intersect;
using Rhino;

namespace Tellinclam.Algorithms
{
    internal class Basic
    {
        static double _eps = 0.000001;

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
                double distance = PtDistanceToLine_2(pt, new Line(v, next_v), out Point3d plummet, out double stretch);
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

        /// <summary>
        /// Calculate the distance between the point and the segment.
        /// Output the projected point and the ratio that the point is evaluated by the segment.
        /// </summary>
        public static double PtDistanceToLine_2(Point3d pt, Line line,
          out Point3d plummet, out double stretch)
        {
            double dx = line.PointAt(1).X - line.PointAt(0).X;
            double dy = line.PointAt(1).Y - line.PointAt(0).Y;
            Point3d origin = line.PointAt(0);

            if (dx == 0 && dy == 0) // zero length segment
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

        /// <summary>
        /// Return the angle (0~360) of two vector by calculating arctangent
        /// </summary>
        public static double VectorAngle(Vector3d vec1, Vector3d vec2)
        {
            //double x1 = endPt1[0] - connectingPt[0]; //Vector 1 - x
            //double y1 = endPt1[1] - connectingPt[1]; //Vector 1 - y
            //double x2 = endPt2[0] - connectingPt[0]; //Vector 2 - x
            //double y2 = endPt2[1] - connectingPt[1]; //Vector 2 - y

            // for angle 0 ~ 180 use Math.Atan
            // for angle 0 ~ 360 use Math.Atan2
            double angle = Math.Atan2(vec1.Y, vec1.X) - Math.Atan2(vec2.Y, vec2.X);
            angle = angle * 360 / (2 * Math.PI);

            if (angle < 0)
                angle += 360;

            return angle;
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

        public static List<Curve> ShatterCrvs(List<Curve> crvs)
        {
            List<Curve> shatteredCrvs = new List<Curve>();

            for (int i = 0; i <= crvs.Count - 1; i++)
            {
                List<double> breakParams = new List<double>();
                for (int j = 0; j <= crvs.Count - 1; j++)
                {
                    if (i != j)
                    {
                        CurveIntersections CI = Intersection.CurveCurve(
                          crvs[i], crvs[j], RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                          RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        foreach (IntersectionEvent IE in CI)
                        {
                            breakParams.Add(IE.ParameterA);
                        }
                    }
                }
                shatteredCrvs.AddRange(crvs[i].Split(breakParams));
            }

            return shatteredCrvs;
        }

        public static List<Point3d> GetIntersectionOfLines(List<Line> lines)
        {
            List<Point3d> pts = new List<Point3d>() { };
            for (int i = 0; i < lines.Count - 1; i++)
            {
                for (int j = 0; j < lines.Count - 1; j++)
                {
                    if (i != j)
                    {
                        if (Intersection.LineLine(lines[i], lines[j], 
                            out double paramA, out double paramB, 
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, true))
                        {
                            pts.Add(lines[i].PointAt(paramA));
                        }
                    }
                }
            }
            return pts;
        }

        public static List<Line> ShatterLines(List<Line> lines)
        {
            List<Line> shatteredLines = new List<Line>() { };
            List<Curve> crvs = new List<Curve>() { };
            foreach (Line line in lines)
                crvs.Add(new LineCurve(line));

            List<Curve> shatters = ShatterCrvs(crvs);

            foreach (Curve crv in shatters)
            {
                if (crv.IsLinear())
                    shatteredLines.Add(new Line(crv.PointAtStart, crv.PointAtEnd));
            }
            return shatteredLines;
        }

        public static double PtDistanceToRay(
            Point3d pt, Point3d origin, Vector3d vec, out double stretch)
        {
            double dx = vec.X;
            double dy = vec.Y;

            // Calculate the t that minimizes the distance.
            double t = ((pt.X - origin.X) * dx + (pt.Y - origin.Y) * dy) /
              (dx * dx + dy * dy);

            Point3d closest = new Point3d(origin.X + t * dx, origin.Y + t * dy, 0);
            dx = pt.X - (origin.X + t * dx);
            dy = pt.Y - (origin.Y + t * dy);
            //stretch = t * Math.Sqrt(dx * dx + dy * dy);
            stretch = closest.DistanceTo(origin);
            if (t < 0)
                stretch = -stretch;
            //Rhino.RhinoApp.WriteLine("this distance is: " + stretch.ToString());
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static List<Line> RemoveDupLines(List<Line> lines, double tol = 0.001)
        {
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
