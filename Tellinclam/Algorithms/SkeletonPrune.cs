using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tellinclam.Algorithms
{
    internal class SkeletonPrune
    {
        /// <summary>
        /// Align skeletons. Step 1: Align edges by QT and DBSCAN clustering based on the offset/directional distance
        /// between edges. Step 2: extend the node of graph (degree = 1) to the polygon boundary 
        /// </summary>
        /// <param name="skeletons"></param>
        /// <param name="polylines"></param>
        /// <param name="tol_d"> the offset distance between edges</param>
        /// <param name="tol_theta"> the angle between two edges</param>
        /// <param name="eta"> the radio of directional/offset distance</param>
        /// <param name="debug_vts"></param>
        /// <param name="debug_vts_degree"></param>
        /// <param name="debug_bundles"></param>
        /// <returns></returns>
        public static List<Line> Align(List<Line> skeletons, List<Curve> polylines, double tol_d, double tol_theta, double eta,  
            out List<Point3d> debug_vts, out List<int> debug_vts_degree, out List<Line> debug_axes, out List<List<Line>> debug_bundles)
        {

            // do not edit the original skeletons or bisectors
            List<Line> edges = Util.DeepCopy(skeletons);
            // flatten all edges then solve the adjacency
            // get graph in form of vertex list and adjacency matrix
            // the matrix records the id of the edge connecting these nodes
            // update the node list and their degrees
            int[,] adjMat = GetAdjMat(edges, out Point3d[] vts, out int[] vts_degree);

            // initiate outputs
            debug_vts = vts.ToList();
            debug_vts_degree = vts_degree.ToList();
            debug_bundles = new List<List<Line>>();
            debug_axes = new List<Line>();

            // align prevalent edges to form axes (d) then move relevant points to them
            // note that the vector should be replaced by prevalent ones
            List<Line> axes_y = GetAxes(edges, new Vector3d(0, 1, 0), tol_d, tol_theta, eta, out _);
            List<Line> axes_x = GetAxes(edges, new Vector3d(1, 0, 0), tol_d, tol_theta, eta, out debug_bundles);
            // extend these axes to the boundary
            for (int i = 0; i < axes_y.Count; i++)
                axes_y[i] = ExtendLineToBoundary(polylines, axes_y[i]);
            for (int i = 0; i < axes_x.Count; i++)
                axes_x[i] = ExtendLineToBoundary(polylines, axes_x[i]);
            debug_axes.AddRange(axes_y);
            debug_axes.AddRange(axes_x);

            for (int i = 0; i < vts.Length; i++)
            {
                foreach (Line axis in axes_y)
                {
                    Point3d plummet = axis.ClosestPoint(vts[i], false);
                    double param = axis.ClosestParameter(vts[i]);
                    if (vts[i].DistanceTo(plummet) < tol_d && param >= 0 && param <= 1)
                        vts[i] = plummet;
                }
                foreach (Line axis in axes_x)
                {
                    Point3d plummet = axis.ClosestPoint(vts[i], false);
                    double param = axis.ClosestParameter(vts[i]);
                    if (vts[i].DistanceTo(plummet) < tol_d && param >= 0 && param <= 1)
                        vts[i] = plummet;
                }
            }

            // remove redundant points with incident, colined edges 
            Vector3d normal = new Vector3d(0, 0, 1);
            for (int i = 0; i < vts.Length; i++)
            {
                if (vts[i].Z > 0 || vts_degree[i] != 2)
                    continue;
                int[] idx = new int[2] { -1, -1 };
                int idx_counter = 0;
                for (int j = 0; j < adjMat.GetLength(1); j++)
                {
                    if (adjMat[i, j] >= 0)
                    {
                        idx[idx_counter] = j;
                        idx_counter++;
                    }
                }
                if (idx[0] < 0 && idx[1] < 0)
                    continue;
                double angle_delta = Vector3d.VectorAngle(
                    vts[idx[0]] - vts[i], vts[idx[1]] - vts[i], normal);
                if (Math.Abs(angle_delta - Math.PI) < 0.0001)
                {
                    vts[i] = new Point3d(0, 0, 1);  // which means to skip this point
                    adjMat[idx[0], i] = -1;
                    adjMat[idx[1], i] = -1;
                    adjMat[i, idx[0]] = -1;
                    adjMat[i, idx[1]] = -1;
                    adjMat[idx[0], idx[1]] = 999;   // an impossible edge index
                    adjMat[idx[1], idx[0]] = 999;   // as a place holder
                }
            }

            /*
            // locate intersections X of axes
            // collapse nodes to X within certain range (be careful)
            // remove duplicate points with inherited adjacency information
            List<Point3d> ptx = new List<Point3d>() { };
            foreach (Line axis_x in axes_x)
            {
                foreach (Line axis_y in axes_y)
                {
                    // it will return false with 2D parallel lines
                    if (Intersection.LineLine(axis_x, axis_y, out double param_x, out double param_y,
                        RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, false))
                        ptx.Add(axis_x.PointAt(param_x));
                }
            }
            List<Point3d> checkbox_vts = new List<Point3d>()
            {
                new Point3d(tol_d / 2, tol_d / 2, 0),
                new Point3d(-tol_d / 2, tol_d / 2, 0),
                new Point3d(-tol_d / 2, -tol_d / 2, 0),
                new Point3d(tol_d / 2, -tol_d / 2, 0),
                new Point3d(tol_d / 2, tol_d / 2, 0),
            };
            
            foreach (Point3d pt in ptx)
            {
                Polyline checkbox = new Polyline(checkbox_vts);
                var xf = Transform.Translation(pt.X, pt.Y, pt.Z);
                checkbox.Transform(xf);

                List<int> pts_collapse_idx = new List<int>() { };
                for (int i = 0; i < vts.Length; i++)
                {
                    if (Basic.IsPtInPoly(vts[i], checkbox, true))
                        pts_collapse_idx.Add(i);
                }
                if (pts_collapse_idx.Count == 0)
                    continue;
                // set the first point as the collapsed one
                // copy all adjacencies to its column/row
                int idx_base = pts_collapse_idx.Min();
                for (int i = 0; i < adjMat.GetLength(0); i++)
                {
                    if (pts_collapse_idx.Contains(i))
                        for (int j = 0; j < adjMat.GetLength(1); j++)
                        {
                            if (adjMat[idx_base, j] < 0)
                                adjMat[idx_base, j] = adjMat[i,j];
                        }
                }
                for (int j = 0; j < adjMat.GetLength(1); j++)
                {
                    if (pts_collapse_idx.Contains(j))
                        for (int i = 0; i < adjMat.GetLength(0); i++)
                        {
                            if (adjMat[i, idx_base] < 0)
                                adjMat[i, idx_base] = adjMat[i, j];
                        }
                }

                // cull points from vts list
                for (int i = 0; i < vts.Length; i++)
                {
                    if (i == idx_base)
                        vts[i] = pt;
                    else if (pts_collapse_idx.Contains(i))
                        vts[i] = new Point3d(0, 0, 1);
                    // we will skip this one during regeneration
                }
            }
            */


            // ------------- updated method, simple extension along edge direction ------------
            // only edit the coordinate of endpoints, do not add new edges

            for (int i = 0; i < vts.Length; i++)
            {
                // if endpoint, track incident edge and its possible extension
                if (vts_degree[i] == 1)
                {
                    if (GetAdjacency(i, adjMat).Count == 0)
                        continue;
                    Point3d startPt = vts[GetAdjacency(i, adjMat)[0]];
                    Vector3d dir = vts[i] - startPt;
                    dir.Unitize();
                    LineCurve ray = new LineCurve(startPt, startPt + 100000 * dir);
                    List<double> parameters = new List<double>(); // param list for the nearest intersection
                    foreach (Curve edge in polylines)
                    {
                        CurveIntersections ccx = Intersection.CurveCurve(ray as Curve, edge,
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        foreach (IntersectionEvent evt in ccx)
                        {
                            if (evt.IsPoint)
                            {
                                parameters.Add(evt.ParameterA);
                            }
                        }
                    }
                    if (parameters.Count != 0)
                    {
                        parameters.Sort();
                        vts[i] = ray.PointAt(parameters[0]);
                    }
                }
            }
            // regenerate edges
            List<Line> network_rebuilt = RegenEdges(vts, adjMat);
            return network_rebuilt;
        }


        // -------------------------- UTILITIES ----------------------------- //
        // ------------------------------------------------------------------ //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// This is based on the Quality Threshold clustering.
        /// Basic idea borrowed from https://github.com/antklen/diameter-clustering
        /// we add points one by one. If there is a cluster with all points close enough to new points, 
        /// then we add new point to this cluster. If there is no such cluster, this point starts new cluster.
        /// </summary>
        /// <returns></returns>
        public static List<Line> GetAxes(List<Line> edges, Vector3d direction, double tol_d, double tol_theta, double eta, 
            out List<List<Line>> bundles)
        {
            bundles = new List<List<Line>>();
            List<List<Line>> edge_bundles = new List<List<Line>>() { };
            List<Line> _edges = new List<Line>() { };

            Vector3d unitz = new Vector3d(0, 0, 1);
            foreach (Line edge in edges)
            {
                double angle_delta = Vector3d.VectorAngle(edge.Direction, direction, unitz);
                if (Math.Abs(angle_delta) < tol_theta || Math.Abs(angle_delta - Math.PI) < tol_theta)
                    _edges.Add(edge);
            }

            // the task is to move items from _edges to edge_bundles one by one
            // put an edge to a group if the maximum distance to 
            // all other edges of that group is within the threshold
            edge_bundles.Add(new List<Line>() { _edges[0] });
            _edges.RemoveAt(0);
            while (_edges.Count > 0)
            {
                int inBundleCounter = 0;
                List<double> avg_distances = new List<double>() { };
                // if the directional distance between edge[0] and any edge within edge_bundles[i] is
                // less than the threshold (3×d for example) then it can be allowed into this bundle
                for (int i = 0; i < edge_bundles.Count; i++)
                {
                    // dNormal - the distance of lines perpendicular to its direction
                    int outRangeCounter = 0;
                    double dNormal_sum = 0;
                    for (int j = 0; j < edge_bundles[i].Count; j++)
                    {
                        double dNormal = Basic.PtDistanceToRay(
                            _edges[0].PointAt(0), edge_bundles[i][j].PointAt(0), direction, 
                            out Point3d plummet, out double stretch);
                        //double distance = edge_bundles[i][j].DistanceTo(_edges[0].PointAt(0), false);
                        dNormal_sum += dNormal;
                        // not in range
                        if (dNormal > tol_d)
                            outRangeCounter++;
                    }
                    // if the normal distance between this edge and any edge in the bundle is less than the threshold
                    // and the max directional distance between this edge and edge in the bundle is less than the threshold
                    //  && dAlongs[0] < 3 * tol_d
                    avg_distances.Add(dNormal_sum / edge_bundles[i].Count);
                    if (outRangeCounter == 0)
                        inBundleCounter++;
                    // if not, go to the next group and check
                }
               
                if (inBundleCounter == 0)
                    // if no group accepts this edge, create a new one
                    edge_bundles.Add(new List<Line>() { _edges[0] });
                else
                    // if there are several candidates, pick the nearest one
                    // if serveral candidates again, IndexOf() will return the first one
                    edge_bundles[avg_distances.IndexOf(avg_distances.Min())].Add(_edges[0]);

                _edges.RemoveAt(0);
            }

            List<Line> axes = new List<Line>() { };
            foreach (List<Line> edge_bundle in edge_bundles)
            {
                /*
                List<double> lengths = new List<double>() { };
                foreach (Line edge in edge_bundle)
                    lengths.Add(edge.Length);
                // if several candidates, pick the centeroid or their average position
                // generate long enough line segment to cover all edges (as an axis)
                Line mainTrunk = edge_bundle[lengths.IndexOf(lengths.Max())];
                axes.Add(new Line(mainTrunk.PointAt(0), mainTrunk.PointAt(1)));
                */

                if (edge_bundle.Count == 1)
                {
                    axes.Add(edge_bundle[0]);
                    continue;
                }
                    
                // how about further divide edge_bundle into several clusters by fuzzy intersection?
                List<int> cmarks = FuzzyIntersectionExpansion(edge_bundle, eta*tol_d, tol_d, 1);
                List<List<Line>> clusters = new List<List<Line>>();
                for (int i = 0; i < cmarks.Max(); i++)
                    clusters.Add(new List<Line>());
                for (int i = 0; i < cmarks.Count; i++)
                {
                    if (cmarks[i] == -1)
                        clusters.Add(new List<Line>() { edge_bundle[i] });
                    else
                        clusters[cmarks[i] - 1].Add(edge_bundle[i]);
                }

                foreach (List<Line> cluster in clusters)
                {
                    bundles.Add(cluster);

                    List<double> tracedAreas = new List<double>();
                    foreach (Line edge in cluster)
                    {
                        tracedAreas.Add(GetProjectionTraceArea(cluster, edge));
                    }
                    Line baseline = cluster[tracedAreas.IndexOf(tracedAreas.Min())];
                    List<double> parameters = new List<double>();
                    foreach (Line edge in cluster)
                    {
                        parameters.Add(baseline.ClosestParameter(edge.PointAt(0)));
                        parameters.Add(baseline.ClosestParameter(edge.PointAt(1)));
                    }
                    parameters.Sort();
                    axes.Add(new Line(baseline.PointAt(parameters[0]), baseline.PointAt(parameters.Last())));
                }
            }

            return axes;
        }

        public static int[,] GetAdjMat(List<Line> edges, out Point3d[] vts, out int[] degrees)
        {
            vts = GetNodes(edges, out List<int> degreeList).ToArray();
            degrees = degreeList.ToArray();
            int[,] adjMat = new int[vts.Length, vts.Length];

            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                for (int j = 0; j < adjMat.GetLength(1); j++)
                {
                    adjMat[i, j] = -1;
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                int id_1 = -1;
                int id_2 = -1;
                for (int j = 0; j < vts.Length; j++)
                {
                    if (edges[i].PointAt(0).DistanceTo(vts[j]) < 0.0001)
                        id_1 = j;
                    if (edges[i].PointAt(1).DistanceTo(vts[j]) < 0.0001)
                        id_2 = j;
                }
                if (id_1 >= 0 && id_2 >= 0)
                {
                    adjMat[id_1, id_2] = i;
                    adjMat[id_2, id_1] = i;
                }
            }
            return adjMat;
        }

        /// <summary>
        /// return the index adjacent to the target node
        /// </summary>
        public static List<int> GetAdjacency(int id, int[,] adjMat)
        {
            List<int> ids = new List<int>();
            for (int i = 0; i < adjMat.GetLength(1); i++)
            {
                if (adjMat[id, i] >= 0)
                {
                    ids.Add(i);
                }
            }
            return ids;
        }

        public static double GetEdgeDistanceAtDirection(Line eA, Line eB, Vector3d dir)
        {
            // project the 4 endpoints onto a given direction, then take the minimum distance among them
            Line baseline = new Line(Point3d.Origin, Point3d.Origin + dir);
            double a = baseline.ClosestParameter(eA.PointAt(0));
            double b = baseline.ClosestParameter(eA.PointAt(1));
            double c = baseline.ClosestParameter(eB.PointAt(0));
            double d = baseline.ClosestParameter(eB.PointAt(1));
            if (a > b) Util.Swap(ref a, ref b);
            if (c > d) Util.Swap(ref c, ref d);
            if (b < c)
                return baseline.PointAt(b).DistanceTo(baseline.PointAt(c));
            else if (d < a)
                return baseline.PointAt(d).DistanceTo(baseline.PointAt(a));
            else
                return 0;
        }

        public static double GetProjectionTraceArea(List<Line> edges, Line baseline)
        {
            double summation = 0;
            foreach (Line edge in edges)
            {
                Point3d projStart = baseline.ClosestPoint(edge.PointAt(0), false);
                Point3d projEnd = baseline.ClosestPoint(edge.PointAt(1), false);
                double area = 0.5 * projStart.DistanceTo(projEnd) * (
                    edge.PointAt(0).DistanceTo(projStart) + edge.PointAt(1).DistanceTo(projEnd));
                summation += area;
            }
            return summation;
        }

        /// <summary>
        /// Recreate edges based on vertice list and its adjacency matrix
        /// </summary>
        /// <returns></returns>
        public static List<Line> RegenEdges(Point3d[] vts, int[,] adjMat)
        {
            List<Line> skeletons = new List<Line>() { };
            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                for (int j = i; j < adjMat.GetLength(1); j++)
                {
                    if (adjMat[i, j] >= 0)
                        if (vts[i].Z < 1 && vts[j].Z < 1)
                        {
                            Line newEdge = new Line(vts[i], vts[j]);
                            if (newEdge.IsValid)
                                skeletons.Add(newEdge);
                        }
                            
                }
            }
            return skeletons;
        }

        /// <summary>
        /// Flatten all lines, remove the duplicate points, then return the node list
        /// </summary>
        /// <returns></returns>
        public static List<Point3d> GetNodes(List<Line> lines, out List<int> degrees)
        {
            List<Point3d> vts = new List<Point3d>() { };
            degrees = new List<int>() { };
            foreach (Line line in lines)
            {
                vts.Add(line.PointAt(0));
                vts.Add(line.PointAt(1));
                degrees.Add(1);
                degrees.Add(1);
            }

            //Rhino.Geometry has available function to do this
            //Point3d[] vts_ = Point3d.CullDuplicates(vts, 0.0001);

            for (int i = vts.Count - 1; i >= 1; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if ((vts[i] - vts[j]).Length < 0.00001)
                    {
                        vts.RemoveAt(i);
                        degrees[j] += degrees[i];
                        degrees.RemoveAt(i);
                        break;
                    }
                }
            }
            return vts;
        }

        public static List<int> FuzzyIntersectionExpansion(List<Line> edges, double ext_x, double ext_y, int MinPts)
        {
            List<int> labels = Enumerable.Repeat(0, edges.Count).ToList();
            int C = 0;
            for (int P = 0; P < edges.Count; P++)
            {
                if (labels[P] != 0)
                    continue;
                List<int> neighbors = RegionQuery(edges, P, ext_x, ext_y);
                if (neighbors.Count < MinPts)
                    labels[P] = -1;
                else
                {
                    C += 1;
                    // grow cluster
                    int i = 0;
                    while (i < neighbors.Count)
                    {
                        int Pn = neighbors[i];
                        if (labels[Pn] == -1)
                            labels[Pn] = C;
                        else if (labels[Pn] == 0)
                        {
                            labels[Pn] = C;
                            List<int> neighborsPn = RegionQuery(edges, Pn, ext_x, ext_y);
                            if (neighborsPn.Count >= MinPts)
                            {
                                neighbors.AddRange(neighborsPn);
                            }
                        }
                        i += 1;
                    }
                }
            }
            return labels;
        }

        // search datapoint within the neighborhood by edge distance
        // you can switch this distance function for DBSCAN iteration
        public static List<int> RegionQuery(List<Line> edges, int P, double ext_x, double ext_y)
        {
            List<int> neighbors = new List<int>();
            for (int i = 0; i < edges.Count; i++)
            {
                if (i == P)
                    continue;
                PolylineCurve boxA = GetExpansionBox(edges[P], ext_x, ext_y);
                PolylineCurve boxB = GetExpansionBox(edges[i], ext_x, ext_y);
                CurveIntersections ccx = Intersection.CurveCurve(boxA, boxB,
                    RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                    RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                if (ccx.Count > 0)
                    neighbors.Add(i);
            }
            return neighbors;
        }

        public static PolylineCurve GetExpansionBox(Line edge, double ext_x, double ext_y)
        {
            List<Point3d> vertices = new List<Point3d>() { };
            Vector3d direction = edge.Direction / edge.Length;
            Vector3d perpCW = new Vector3d(direction.Y, -direction.X, 0);
            Vector3d perpCCW = new Vector3d(-direction.Y, direction.X, 0);
            vertices.Add(edge.PointAt(0) - ext_x * direction + ext_y * perpCCW);
            vertices.Add(edge.PointAt(1) + ext_x * direction + ext_y * perpCCW);
            vertices.Add(edge.PointAt(1) + ext_x * direction + ext_y * perpCW);
            vertices.Add(edge.PointAt(0) - ext_x * direction + ext_y * perpCW);
            vertices.Add(vertices[0]);
            return new PolylineCurve(vertices);
        }

        public static Line ExtendLineToBoundary(List<Curve> bounds, Line line)
        {
            Vector3d dir = line.Direction / line.Length;
            Point3d mid = line.PointAt(0.5);
            LineCurve ray = new LineCurve(mid - 100000 * dir, mid + 100000 * dir);
            List<double> parameters = new List<double>();
            foreach (Curve bound in bounds)
            {
                CurveIntersections ccx = Intersection.CurveCurve(ray as Curve, bound,
                    RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                    RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                foreach (IntersectionEvent evt in ccx)
                    if (evt.IsPoint)
                        parameters.Add(evt.ParameterA);
            }
            if (parameters.Count != 0)
            {
                parameters.Sort();
                int counter = 0;
                foreach (double parameter in parameters)
                {
                    if (parameter > 100000)
                        break;
                    counter += 1;
                }
                return new Line(ray.PointAt(parameters[counter - 1]), ray.PointAt(parameters[counter]));
            }
            else
                return line;
        }
    }
}
