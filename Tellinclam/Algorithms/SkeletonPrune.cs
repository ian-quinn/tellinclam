using Grasshopper.Kernel.Geometry.Delaunay;
using Grasshopper.Kernel.Utility;
using Microsoft.SqlServer.Server;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.Input.Custom;
using Rhino.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tellinclam.Algorithms
{
    internal class SkeletonPrune
    {
        public static List<Line> Prune(List<Line> skeletons, List<Line> bisectors, 
            double tol_h, double tol_d, double tol_theta, 
            out List<Point3d> debug_vts, out List<double> debug_vts_height, out List<int> debug_vts_degree)
        {
            // do not edit the original skeletons or bisectors

            List<Point3d> vertice = GetNodes(skeletons, out List<int> _ds);

            // retreive node incident to edge
            // then extend remaining nodes to the corresponding contour
            // (by connecting it to the midpoint)

            List<Line> extended_edges = new List<Line>() { };
            int init_num = vertice.Count;
            for (int i = 0; i < init_num; i++)
            {
                Point3d contour_incident = new Point3d(0, 0, 0);
                for (int j = 0; j < bisectors.Count; j++)
                {
                    if (bisectors[j].PointAt(0).DistanceTo(vertice[i]) < 0.0001)
                        contour_incident = contour_incident + bisectors[j].PointAt(1);
                    if (bisectors[j].PointAt(1).DistanceTo(vertice[i]) < 0.0001)
                        contour_incident = contour_incident + bisectors[j].PointAt(0);
                }
                if (_ds[i] == 1)
                {
                    // extend the stray to contour by connecting incident point
                    // with the corresponding mid-point of the contour edge it collapses from
                    extended_edges.Add(new Line(vertice[i], contour_incident / 2));
                    vertice.Add(contour_incident / 2);
                }
            }

            List<Line> edges = new List<Line>() { };
            edges.AddRange(skeletons);
            edges.AddRange(extended_edges);

            // regenerate the network, recording the vertices, adjacency matrix
            // height and degree of each vertex
            int[,] adjMat = GetAdjMat(edges, out Point3d[] vts, out int[] vts_degree);
            double[] vts_height = new double[vts.Length];       // records height of each vertex

            for (int i = 0; i < vts.Length; i++)
            {
                for (int j = 0; j < bisectors.Count; j++)
                {
                    if (bisectors[j].PointAt(0).DistanceTo(vts[i]) < 0.0001 ||
                        bisectors[j].PointAt(1).DistanceTo(vts[i]) < 0.0001)
                    {
                        vts_height[i] = bisectors[j].Length / Math.Sqrt(2);
                    }
                }
            }

            // fullfil the vts_height list
            // every node with degree <=2 must have bisectors connected
            // which indicates the 'height' it rises from the base boundary contour
            // those nodes with no bisectors can 'borrow' height value
            // from other nodes connected to them, by choosing the maximum
            // this only applies to general situation that the skeleton contains
            // no 'tower tip', only the 'roof ridges'
            // 'tower tip' usually collapses from regular polygons
            // in which case the height of 'tip' node equals adjacent node's height
            // plus the projection of the edge in between
            for (int flag = 0; flag < 2; flag++)
            {
                for (int i = 0; i < vts.Length; i++)
                    if (vts_height[i] == 0)
                    {
                        double max_height = 0;
                        for (int j = 0; j < adjMat.GetLength(1); j++)
                        {
                            if (adjMat[i, j] > 0)
                                if (vts_height[j] > max_height)
                                    max_height = vts_height[j];
                        }
                        vts_height[i] = max_height;
                    }
            }

            // info from original network
            //debug_vts = vts.ToList();
            //debug_vts_height = vts_height.ToList();
            //debug_vts_degree = vts_degree.ToList();

            // loop to remove all strays under certain height
            // the height indicates the offset distance from the contour
            // usually for indoor circulation area, this value should not 
            // be smaller than 0.75, or else the corridor width is too narrow
            List<int> removable_edge_idx = new List<int>() { };
            bool flag_rerun = true;
            while (flag_rerun)
            {
                int trimmable_counter = 0;
                for (int i = 0; i < vts.Count(); i++)
                {
                    if (vts_height[i] < tol_h && vts_degree[i] == 1)
                    {
                        trimmable_counter++;
                        for (int j = 0; j < adjMat.GetLength(1); j++)
                        {
                            if (adjMat[i, j] > 0)
                            {
                                removable_edge_idx.Add(adjMat[i, j]);
                                vts_degree[i] -= 1;
                                vts_degree[j] -= 1;
                            }
                        }
                    }
                }
                if (trimmable_counter == 0)
                    flag_rerun = false;
            }

            // remove inqualified edges
            for (int i = edges.Count - 1; i >= 0; i--)
            {
                if (removable_edge_idx.Contains(i))
                    edges.RemoveAt(i);
            }

            // flatten all edges then solve the adjacency
            // get graph in form of vertex list and adjacency matrix
            // the matrix records the id of the edge connecting these nodes

            // update the node list and their degrees
            adjMat = GetAdjMat(edges, out vts, out vts_degree);              // update the adjacency matrix

            // align prevalent edges to form axes (d) then move relevant points to them
            // note that the vector should be replaced by prevalent ones
            List<Line> axes_y = GetAxes(edges, new Vector3d(0, 1, 0), tol_d, tol_theta);
            List<Line> axes_x = GetAxes(edges, new Vector3d(1, 0, 0), tol_d, tol_theta);
            for (int i = 0; i < vts.Length; i++)
            {
                foreach (Line axis in axes_y)
                {
                    Point3d plummet = axis.ClosestPoint(vts[i], false);
                    if (vts[i].DistanceTo(plummet) < tol_d)
                        vts[i] = plummet;
                }
                foreach (Line axis in axes_x)
                {
                    Point3d plummet = axis.ClosestPoint(vts[i], false);
                    if (vts[i].DistanceTo(plummet) < tol_d)
                        vts[i] = plummet;
                }
            }

            debug_vts = vts.ToList();
            debug_vts_height = vts_height.ToList();
            debug_vts_degree = vts_degree.ToList();

            // remove redundant point with coline vectors stretching out
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
        public static List<Line> GetAxes(List<Line> edges, Vector3d direction, double tol_d, double tol_theta)
        {
            List<List<Line>> edge_bundles = new List<List<Line>>() { };
            List<Line> _edges = new List<Line>() { };
            Vector3d normal = new Vector3d(0, 0, 1);
            foreach (Line edge in edges)
            {
                double angle_delta = Vector3d.VectorAngle(edge.Direction, direction, normal);
                if (Math.Abs(angle_delta) < tol_theta || Math.Abs(angle_delta - Math.PI) < tol_theta)
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
                List<double> avg_distances = new List<double>() { };
                for (int i = 0; i < edge_bundles.Count; i++)
                {
                    int inRangeCounter = 0;
                    double distance_sum = 0;
                    for (int j = 0; j < edge_bundles[i].Count; j++)
                    {
                        double distance = Basic.PtDistanceToRay(
                            _edges[0].PointAt(0),
                            edge_bundles[i][j].PointAt(0),
                            direction, out double stretch);
                        //double distance = edge_bundles[i][j].DistanceTo(_edges[0].PointAt(0), false);
                        distance_sum += distance;
                        // not in range
                        if (distance > tol_d)
                            inRangeCounter++;
                    }
                    // if the edge could be included in this group
                    if (inRangeCounter == 0)
                    {
                        avg_distances.Add(distance_sum / edge_bundles[i].Count);
                        inBundleCounter++;
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
                List<double> lengths = new List<double>() { };
                foreach (Line edge in edge_bundle)
                    lengths.Add(edge.Length);
                // if several candidates, pick the centeroid or their average position
                // generate long enough line segment to cover all edges (as an axis)
                Line mainTrunk = edge_bundle[lengths.IndexOf(lengths.Max())];
                axes.Add(new Line(mainTrunk.PointAt(0), mainTrunk.PointAt(1)));
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
                            skeletons.Add(new Line(vts[i], vts[j]));
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
    }
}
