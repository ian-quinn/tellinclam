using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Geometry.Delaunay;
using Grasshopper.Kernel.Utility;
using Microsoft.SqlServer.Server;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.UI.Controls;
using static Rhino.Render.TextureGraphInfo;
using System.CodeDom.Compiler;
using System.Runtime.InteropServices;

namespace Tellinclam.Algorithms
{
    internal class EdgeAlign
    {
        public static List<Line> Align(List<Line> edges, double tol_d, double tol_c, double tol_theta,
            out List<Vector3d> debug_directions, out List<Line> debug_axes, out List<List<Line>> debug_bundles,
            out List<Point3d> debug_x, out List<Point3d> debug_vt1, out List<Point3d> debug_vt2)
        {
            // CAUTION
            // there are four level of tolerances, derived from tol_d
            // 1 - for axis spanning larger gaps, use 4 * tol_d
            // 2 - for axis collapsing together, use tol_d
            // 3 - for edge moving to axis, use 0.5 * tol_d
            // 4 - for axis intersection absorbing nearby vertices, use 0.5 * tol_d


            // DEBUG prepare output for each line cluster
            debug_bundles = new List<List<Line>>() { };


            // update the node list and their degrees
            int[,] adjMat = GetAdjMat(edges, out Point3d[] vts, out int[] vts_degree);

            List<Vector3d> directions = GetDirections(edges, tol_theta);


            // DEBUG prepare output for all directions
            debug_directions = directions;


            // align prevalent edges to form axes (d) then move relevant points to them
            // note that the vector should be replaced by prevalent ones
            List<Line> axes = new List<Line>() { };

            // DEBUG prepare output for all axes
            debug_axes = new List<Line>() { };
            foreach (Vector3d direction in directions)
            {
                // for DEBUG purpose this list includes axes with 0 length
                // remove those invalid axes in 
                var _axes = GetAxes(edges, direction, tol_d, tol_theta, out List<List<Line>> sub_groups);
                debug_axes.AddRange(_axes);
                foreach (Line ax in _axes)
                    if (ax.Length != 0)
                        axes.Add(ax);
                debug_bundles.AddRange(sub_groups);
            }

            for (int i = 0; i < edges.Count; i++)
            {
                int[] ids = GetEndpointIdFromAdjMat(i, adjMat);
                if (ids[0] == -1 || ids[1] == -1) continue;

                // if there exist both offset and extension, do extension then offset
                // record offset in moving vectors and extension intersection points

                List<Point3d> pointA = new List<Point3d>() { };
                List<Point3d> pointB = new List<Point3d>() { };
                //List<Line> edges_moved = new List<Line>() { };
                List<double> candidate_axis_length = new List<double>() { };

                foreach (Line axis in axes)
                {
                    double angle_delta = Basic.VectorAnglePI_2(edges[i].Direction, axis.Direction);

                    // regard them as parallel, do offset
                    if (Math.Abs(angle_delta) < tol_theta)
                    {
                        // when align an edge to an axis, you must ensure the aligned edge and the axis are overlapping

                        //double dist = Basic.SegProjectToSeg(edges[i], axis, tol_theta, out double overlap, out Line proj);
                        //if (dist < tol_d && dist > 0 && overlap > 0)
                        //{
                        //    edges_moved.Add(proj);
                        //    candidate_axis_length.Add(axis.Length);
                        //}
                        double dist1 = Basic.PtDistanceToSeg(vts[ids[0]], axis, out Point3d p1, out double s1);
                        double dist2 = Basic.PtDistanceToSeg(vts[ids[1]], axis, out Point3d p2, out double s2);

                        if ((dist1 + dist2) / 2 < 0.5 * tol_d)
                        {
                            pointA.Add(p1);
                            pointB.Add(p2);
                            candidate_axis_length.Add(axis.Length);
                        }
                    }
                }
                if (candidate_axis_length.Count == 0) continue;
                int move_id = candidate_axis_length.IndexOf(candidate_axis_length.Max());
                //vts[ids[0]] = edges_moved[move_id].PointAt(0);
                //vts[ids[1]] = edges_moved[move_id].PointAt(1);
                vts[ids[0]] = pointA[move_id];
                vts[ids[1]] = pointB[move_id];
            }

            
            for (int i = 0; i < edges.Count; i++)
            {
                // two scenarios, extension along the line, or offset with changed line direction
                // retrieve current line direction and check the angle in between is within tol_theta
                // within tol_theta, do offset; out of tol_theta, do extension
                int[] ids = GetEndpointIdFromAdjMat(i, adjMat);
                if (ids[0] == -1 || ids[1] == -1) continue;
                Line edge_update = new Line(vts[ids[0]], vts[ids[1]]);

                // if there exist both offset and extension, do extension then offset
                // record offset in moving vectors and extension intersection points
                List<Point3d> sections = new List<Point3d>() { };
                List<int> pt_replace = new List<int>() { };
                List<double> candidate_extension = new List<double>() { };

                foreach (Line axis in axes)
                {
                    double angle_delta = Basic.VectorAnglePI_2(vts[ids[1]] - vts[ids[0]], axis.Direction);

                    // regard them intersected, do extension
                    // carefully check if the extension is too large (< 2 * tol_d by default)
                    // record pairs of extension and intersection, then pick the farthest extension
                    if (Math.Abs(angle_delta) > tol_theta)
                    {
                        var llx = Basic.SegIntersection(edge_update, axis, 
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 
                            RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                            out Point3d intersection, out double t1, out double t2);
                        double dist1 = Basic.PtDistanceToSeg(vts[ids[0]], axis, out Point3d p1, out double s1);
                        double dist2 = Basic.PtDistanceToSeg(vts[ids[1]], axis, out Point3d p2, out double s2);

                        double extension = intersection.DistanceTo(vts[ids[0]]);
                        double _extension = intersection.DistanceTo(vts[ids[1]]);
                        int pt_move = ids[0];
                        if (extension > _extension)
                        {
                            extension = _extension;
                            pt_move = ids[1];
                        }
                        // check if the extended endpoint is orphan (degree == 1)
                        if (vts_degree[pt_move] == 1)
                            // check if the edge falls within the collapse range (< tol_d)
                            if (dist1 < 2 * tol_d || dist2 < 2 * tol_d)
                                if (extension < 4 * tol_d && t2 >= 0 && t2 <= 1)
                                {
                                    sections.Add(intersection);
                                    candidate_extension.Add(extension);
                                    pt_replace.Add(pt_move);
                                }
                    }
                }
                if (candidate_extension.Count == 0) continue;
                int move_id = candidate_extension.IndexOf(candidate_extension.Max());
                vts[pt_replace[move_id]] = sections[move_id];
            }
            


            // remove redundant point with coline vectors stretching out
            /*
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
                if (idx[0] < 0 || idx[1] < 0)
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
            */

            debug_vt1 = vts.ToList();

            // locate intersections X of axes
            // collapse nodes to X within certain range (be careful)
            // remove duplicate points with inherited adjacency information
            List<Point3d> ptx = new List<Point3d>() { };
            for (int i = 0; i < axes.Count - 1; i++)
            {
                for (int j = i + 1; j < axes.Count; j++)
                {
                    // the intersection does not perform on infinite lines
                    // or else there will be to many candidates
                    // Intersection.LineLine() returns the closest point
                    // NOT the intersection point!
                    var ccx = Intersection.CurveCurve(
                        GetExtensionCurve(axes[i], 2 * tol_d),
                        GetExtensionCurve(axes[j], 2 * tol_d),
                        RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                        RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    if (ccx.Count > 0)
                        ptx.Add(ccx[0].PointA);
                }
            }

            // DEBUG output all intersection points of axes
            debug_x = ptx;
            
            // collapse ?
            // DEBUG when collapsing endpoint of stray edges, try to move the 
            // entire segment with the same moving vector
            foreach (Point3d pt in ptx)
            {
                List<int> pts_collapse_idx = new List<int>() { };
                for (int i = 0; i < vts.Length; i++)
                {
                    if (Basic.IsPtInPoly(vts[i], GetExpansionBox(pt, tol_c), true))
                    {
                        pts_collapse_idx.Add(i);

                        // if this point is the endpoint of an orphan edge
                        // add its paired point as well, by retrieving it from adjMat
                        
                        List<int> pairs = GetOrphanEdgePairedEndpointId(i, adjMat);
                        Vector3d move = pt - vts[i];
                        if (move.Length < 0.00001) continue;

                        // however, move the point at here may cause other problems
                        // we will see to it when DEBUG
                        for (int j = 0; j < pairs.Count; j++)
                        {
                            if (vts[pairs[j]].Z != 1)
                                vts[pairs[j]] = vts[pairs[j]] + move;
                        }
                    }

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
                                adjMat[idx_base, j] = adjMat[i, j];
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
            

            // regenerate edges
            List<Line> results = RegenEdges(vts, adjMat);
            debug_vt2 = vts.ToList();

            return results;
            // return new List<Line>() { };
        }


        // -------------------------- UTILITIES ----------------------------- //
        // ------------------------------------------------------------------ //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// to get every possible directions
        /// </summary>
        /// <param name="edges"></param>
        /// <param name="tol_theta"></param>
        /// <returns></returns>
        public static List<Vector3d> GetDirections(List<Line> edges, double tol_theta)
        {
            List<List<Line>> edge_bundles = new List<List<Line>>() { };
            List<Line> _edges = new List<Line>() { };
            Vector3d xray = new Vector3d(1, 0, 0);
            foreach (Line edge in edges)
            {
                _edges.Add(edge);
            }

            edge_bundles.Add(new List<Line>() { _edges[0] });
            _edges.RemoveAt(0);

            while (_edges.Count > 0)
            {
                int inBundleCounter = 0;
                List<double> avg_distances = new List<double>() { };
                for (int i = 0; i < edge_bundles.Count; i++)
                {
                    int inRangeCounter = 0;
                    double distance_sum = 0;
                    for (int j = 0; j < edge_bundles[i].Count; j++)
                    {
                        // this function returns rotating angle from A to B
                        // by the right-hand law, thumb up to normal direction
                        double angle_delta = Basic.VectorAnglePI_2(_edges[0].Direction,
                            edge_bundles[i][j].Direction);

                        distance_sum += angle_delta;
                        // not in range
                        if (angle_delta > tol_theta)
                            inRangeCounter++;
                    }
                    // if the edge could be included in this group
                    if (inRangeCounter == 0)
                    {
                        avg_distances.Add(distance_sum / edge_bundles[i].Count);
                        inBundleCounter++;
                    }
                    else
                    {
                        avg_distances.Add(double.PositiveInfinity);
                    }
                    // if could not, go to next group and check again
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

            List<Vector3d> directions = new List<Vector3d>() { };
            List<double> fromUnitX_angles = new List<double>() { };
            foreach (List<Line> bundle in edge_bundles)
            {
                List<int> indexs = new List<int>() { };
                List<double> angles = new List<double>() { };
                List<double> lengths = new List<double>() { };
                foreach (Line edge in bundle)
                {
                    // DEBUG here
                    //double angle_delta = Vector3d.VectorAngle(edge.Direction, xray, normal);
                    //if (angle_delta > Math.PI) angle_delta = angle_delta - Math.PI;
                    double angle_delta = Basic.VectorAnglePI_2(edge.Direction, xray);
                    if (angles.Contains(angle_delta))
                        lengths[angles.IndexOf(angle_delta)] += edge.Length;
                    else
                    {
                        indexs.Add(bundle.IndexOf(edge));
                        angles.Add(angle_delta);
                        lengths.Add(edge.Length);
                    }
                }

                Vector3d direction = bundle[lengths.IndexOf(lengths.Max())].Direction;
                if (!directions.Contains(direction))
                {
                    directions.Add(direction);
                }
            }

            return directions;
        }

        /// <summary>
        /// This is based on the Quality Threshold clustering.
        /// Basic idea borrowed from https://github.com/antklen/diameter-clustering
        /// we add points one by one. If there is a cluster with all points close enough to new points, 
        /// then we add new point to this cluster. If there is no such cluster, this point starts new cluster.
        /// </summary>
        /// <returns></returns>
        public static List<Line> GetAxes(List<Line> edges, Vector3d direction, double tol_d, double tol_theta,
            out List<List<Line>> sub_groups)
        {
            sub_groups = new List<List<Line>>() { };

            List<List<Line>> edge_bundles = new List<List<Line>>() { };
            List<Line> _edges = new List<Line>() { };
            foreach (Line edge in edges)
            {
                double angle_delta = Basic.VectorAnglePI_2(edge.Direction, direction);
                if (Math.Abs(angle_delta) < tol_theta)
                    _edges.Add(edge);
            }

            // the task is to empty the _edges list
            // put an edge to a group if the maximum distance to 
            // all edges in it is within the threshold
            edge_bundles.Add(new List<Line>() { _edges[0] });
            _edges.RemoveAt(0);

            while (_edges.Count > 0)
            {
                int inBundleCounter = 0;
                // record the average distances from target line to each bundle
                List<double> avg_distances = new List<double>() { };
                for (int i = 0; i < edge_bundles.Count; i++)
                {
                    int outRangeCounter = 0;
                    double distance_sum = 0;
                    for (int j = 0; j < edge_bundles[i].Count; j++)
                    {
                        double distance = Basic.PtDistanceToRay(
                            _edges[0].PointAt(0), edge_bundles[i][j].PointAt(0),
                            direction, out Point3d plummet, out double stretch);

                        //double distance = edge_bundles[i][j].DistanceTo(_edges[0].PointAt(0), false);

                        distance_sum += distance;
                        // not in range
                        if (distance > tol_d)
                            outRangeCounter++;
                    }
                    // if the edge could be included in this group
                    if (outRangeCounter == 0)
                    {
                        avg_distances.Add(distance_sum / edge_bundles[i].Count);
                        inBundleCounter++;
                    }
                    else
                    {
                        avg_distances.Add(double.PositiveInfinity);
                    }
                    // if could not, go to next group and check again
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
                // within each bundle there can be multiple clusters of edges, which are far enough 
                // to have their own main axes representing themselves.
                // cluster by their expansion box
                List<List<Line>> clusters = SegClusterByFuzzyIntersection(edge_bundle, tol_d, tol_d);
                sub_groups.AddRange(clusters);

                // sub_groups.AddRange(clusters);
                // List<Line> debug_axis = new List<Line>() { };

                foreach (List<Line> cluster in clusters)
                {
                    if (cluster.Count == 1)
                    {
                        if (cluster[0].Length > 2 * tol_d)
                        {
                            axes.Add(Basic.SegProjDirection(cluster[0], direction));
                            //axes.Add(cluster[0]);
                        }
                        else
                        {
                            axes.Add(new Line(Point3d.Origin, Point3d.Origin));
                        }
                    }
                    else
                    {
                        // first, get the line that has the longest walk throught all edges
                        // by picking an edge as A, sum up the sweeping area by projecting other edges onto A
                        // if A has the minimum sweeping area in total, choose it as the "longest walk", e.g. the axis
                        List<double> projection_sums = new List<double>() { };
                        List<Tuple<double, double>> endpoint_evas = new List<Tuple<double, double>>() { };
                        for (int i = 0; i < cluster.Count; i++)
                        {
                            double projection_sum = 0;
                            // the axis spans over all projections of the lines within the cluster
                            // including the baseline A itself
                            // adding evaluation parameter 0 and 1 indicating the baseline A
                            // then other evluation parameters of projections are added to the list
                            List<double> evaluations = new List<double>() { 0, 1 };
                            for (int j = 0; j < cluster.Count; j++)
                            {
                                if (i != j)
                                {
                                    Point3d plummet1 = cluster[i].ClosestPoint(cluster[j].PointAt(0), false);
                                    Point3d plummet2 = cluster[i].ClosestPoint(cluster[j].PointAt(1), false);
                                    double dist1 = plummet1.DistanceTo(cluster[j].PointAt(0));
                                    double dist2 = plummet2.DistanceTo(cluster[j].PointAt(1));
                                    evaluations.Add((plummet1 - cluster[i].PointAt(0)) * cluster[i].Direction / cluster[i].Length / cluster[i].Length);
                                    evaluations.Add((plummet2 - cluster[i].PointAt(0)) * cluster[i].Direction / cluster[i].Length / cluster[i].Length);
                                    projection_sum += plummet1.DistanceTo(plummet2) * (dist1 + dist2) / 2;
                                }
                            }
                            projection_sums.Add(projection_sum);
                            evaluations.Sort();
                            endpoint_evas.Add(new Tuple<double, double>(evaluations[0], evaluations.Last()));
                        }
                        // find the maximum length, whose index is the main edge
                        int main_id = projection_sums.IndexOf(projection_sums.Min());
                        Point3d axis_start = cluster[main_id].PointAt(endpoint_evas[main_id].Item1);
                        Point3d axis_end = cluster[main_id].PointAt(endpoint_evas[main_id].Item2);

                        Line axis = new Line(axis_start, axis_end);
                        if (axis.Length > 2 * tol_d)
                            axes.Add(Basic.SegProjDirection(axis, direction));
                        else
                            axes.Add(new Line(Point3d.Origin, Point3d.Origin));
                    }
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

        public static int[] GetEndpointIdFromAdjMat(int edgeId, int[,] adjMat)
        {
            int[] ids = new int[2] { -1, -1 };
            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                for (int j = 0; j < adjMat.GetLength(1); j++)
                {
                    if (adjMat[i, j] == edgeId)
                    {
                        ids[0] = i;
                        ids[1] = j;
                        return ids;
                    }
                }
            }
            return ids;
        }

        /// <summary>
        /// DEBUG. This is very time consuming. Must get improved.
        /// consider a NextEdge list or something...
        /// </summary>
        public static List<int> GetOrphanEdgePairedEndpointId(int ptId, int[,] adjMat)
        {
            List<int> orphanPts = new List<int>() { };
            List<int> possiblePts = new List<int>() { };
            for (int j = 0; j < adjMat.GetLength(1); j++)
            {
                if (adjMat[ptId, j] >= 0)
                {
                    possiblePts.Add(j);
                }
            }
            foreach (int pt in possiblePts)
            {
                int connection = 0;
                for (int j = 0; j < adjMat.GetLength(1); j++)
                {
                    if (adjMat[pt, j] >= 0)
                    {
                        connection++;
                    }
                }
                if (connection == 1)
                    orphanPts.Add(pt);
            }
            return orphanPts;
        }

        /// <summary>
        /// Recreate edges based on vertice list and its adjacency matrix
        /// remove the orphan segments at the same time
        /// </summary>
        public static List<Line> RegenEdges(Point3d[] vts, int[,] adjMat)
        {
            List<Line> skeletons = new List<Line>() { };
            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                for (int j = i + 1; j < adjMat.GetLength(1); j++)
                {
                    if (adjMat[i, j] >= 0)
                    {
                        if (vts[i].Z == 0 && vts[j].Z == 0)
                        {
                            //if (!(degrees[i] == 1 && degrees[j] == 1))
                            //{
                                Line newEdge = new Line(vts[i], vts[j]);
                                skeletons.Add(newEdge);
                            //}
                        }
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

        public static Polyline GetExpansionBox(Point3d pt, double d_ext)
        {
            // indicating a closed 2D box
            List<Point3d> vertices = new List<Point3d>()
            {
                pt + new Point3d(d_ext / 2, d_ext / 2, 0),
                pt + new Point3d(-d_ext / 2, d_ext / 2, 0),
                pt + new Point3d(-d_ext / 2, -d_ext / 2, 0),
                pt + new Point3d(d_ext / 2, -d_ext / 2, 0),
                pt + new Point3d(d_ext / 2, d_ext / 2, 0),
            };
            return new Polyline(vertices);
        }

        public static Curve GetExtensionCurve(Line edge, double d_ext)
        {
            Point3d start = edge.PointAt(0) - edge.Direction / edge.Length * d_ext;
            Point3d end = edge.PointAt(1) + edge.Direction / edge.Length * d_ext;
            return new LineCurve(start, end);
        }

        public static List<List<Line>> SegClusterByFuzzyIntersection(List<Line> lines, double tol_gap, double tol_off)
        {
            List<Line> linePool = new List<Line>();
            foreach (Line line in lines)
            {
                linePool.Add(line);
            }
            List<List<Line>> lineGroups = new List<List<Line>>();
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
                        if (lineGroup[i] != linePool[j])
                        {
                            // inpropotional expansion box
                            PolylineCurve box1 = GetExpansionBox(lineGroup[i], tol_gap, tol_off);
                            PolylineCurve box2 = GetExpansionBox(linePool[j], tol_gap, tol_off);
                            var ccx = Intersection.CurveCurve(box1, box2, 0.00001, 0.00001);
                            if (ccx.Count > 0)
                            {
                                lineGroup.Add(linePool[j]);
                                linePool.RemoveAt(j);
                            }
                        }
                    }
                }
                lineGroups.Add(lineGroup);
            }
            return lineGroups;
        }

    }
}
