using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    public static class PolygonDetect
    {
        public static object Rhino { get; private set; }

        public static void GetRegion(List<Line> lines,
            out List<Line> shatters, out List<Line> edges, out List<Polyline> regions)
        {
            regions = new List<Polyline>() { };
            // assuming we have perfect trimmed line segment set.

            // perform self intersection and get all shatters
            // OUTPUT List<Line> shatters

            shatters = Basic.BreakLinesAtIntersection(lines);

            // trim all orphan edges untill there is no vertice with 0 degree
            int[,] _adjMat = GetAdjMatBidirection(shatters, 0.000001, out Point3d[] _vts, out int[] _degrees);
            List<int> edge_remove = new List<int>() { };
            List<int> vt_remove = new List<int>() { };
            while (_degrees.Contains(1))
            {
                // remove vertex with 1 degree
                // deduct 1 degree from adjacent connected vertex
                // remove the edge, e.g. connection between
                for (int m = 0; m < _adjMat.GetLength(0); m++)
                {
                    if (vt_remove.Contains(m)) continue;
                    if (_degrees[m] == 1)
                    {
                        for (int n = 0; n < _adjMat.GetLength(1); n++)
                        {
                            if (vt_remove.Contains(n)) continue;
                            if (_adjMat[m, n] >= 0)
                            {
                                vt_remove.Add(m);
                                _degrees[m] = 0;
                                _degrees[n] = _degrees[n] - 1;
                                edge_remove.Add(_adjMat[m, n]);
                                _adjMat[m, n] = -1;
                                _adjMat[n, m] = -1;
                            }
                        }
                    }
                }
            }

            List<Line> trimmed = new List<Line>() { };
            for (int i = 0; i < _adjMat.GetLength(0); i++)
            {
                if (vt_remove.Contains(i)) continue;
                for (int j = i; j < _adjMat.GetLength(1); j++)
                {
                    if (vt_remove.Contains(j)) continue;
                    if (_adjMat[i, j] >= 0)
                    {
                        Line newEdge = new Line(_vts[i], _vts[j]);
                        if (newEdge.IsValid)
                            trimmed.Add(newEdge);
                    }
                }
            }

            edges = trimmed;
            
            // build a directed graph representing all edges
            // double the shatters list then make them reversed "half curve"
            int[,] adjMat = GetAdjMatBidirection(edges, 0.000001, out Point3d[] vts, out int[] degrees);
            List<Line> edge_reversed = new List<Line>() { };
            for (int i = 0; i < edges.Count; i++)
            {
                edge_reversed.Add(new Line(edges[i].PointAt(1), edges[i].PointAt(0)));
            }
            edges.AddRange(edge_reversed);

            // set a list marking those edges that have been traversed
            // traverse all edges by looking up the edge index in adjMat
            // from row index to column index to find the next vertice
            // search for the first edge rotating clockwise
            //int[] edge_traversed = new int[edges.Count];
            List<int> edge_remain = new List<int>() { };
            List<Polyline> shell = new List<Polyline>() { };
            for (int i = 0; i < edges.Count; i++)
            {
                //edge_traversed[i] = 0;
                edge_remain.Add(i);
            }

            int counter = 0;
            while (edge_remain.Count > 0)
            {
                counter++;
                
                // let's say starting from edge_remain[0]
                int edge_initiate = edge_remain[0];
                int vt_start = LookupEdgeVts(adjMat, edge_initiate)[0];
                int vt_current = vt_start;
                int vt_next = LookupEdgeVts(adjMat, edge_initiate)[1];

                // the list recording enclosed edges, which will be removed at the end of loop
                List<int> edge_polygon = new List<int>() { edge_initiate };

                // stop the traverse once the current vertex index is the starting vertex
                // or the vertex has degree 1, which means it connects to nothing, orphan point
                while (vt_next != vt_start)
                {
                    // this happens at the current vertex
                    Vector3d baseDir = vts[vt_current] - vts[vt_next];
                    Vector3d normal = new Vector3d(0, 0, 1);
                    int[] out_edge_ids = LookupEdgeOut(adjMat, vt_next, vt_current);
                    double min_angle = double.PositiveInfinity;
                    int min_edge_id = -1;
                    for (int i = 0; i < out_edge_ids.Length; i++)
                    {
                        // right-hand rule counter-clockwise from vecA to vecB
                        double delta_angle = Vector3d.VectorAngle(
                            edges[out_edge_ids[i]].Direction,
                            baseDir, normal);
                        if (delta_angle < min_angle)
                        {
                            min_angle = delta_angle;
                            min_edge_id = out_edge_ids[i];
                        }
                    }
                    if (min_edge_id >= 0)
                    {
                        edge_polygon.Add(min_edge_id);
                        vt_current = vt_next;
                        vt_next = LookupEdgeVts(adjMat, min_edge_id)[1];
                    }
                    else
                    {
                        break;
                    }
                }

                // create polyline
                List<Point3d> poly_vts = new List<Point3d>() { };
                foreach (int index in edge_polygon)
                {
                    poly_vts.Add(edges[index].PointAt(1));
                }
                poly_vts.Insert(0, edges[edge_polygon[0]].PointAt(0));
                Polyline poly = new Polyline(poly_vts);
                if (Basic.IsClockwise(poly))
                    shell.Add(poly);
                else
                    regions.Add(poly);

                // remove traversed edges
                foreach (int index in edge_polygon)
                {
                    edge_remain.Remove(index);
                }
            }

            regions.Insert(0, shell[0]);

            return;
        }

        private static int[,] GetAdjMatBidirection(List<Line> edges, double _eps, 
            out Point3d[] vts, out int[] degrees)
        {
            List<Point3d> _vts = new List<Point3d>() { };
            List<int> _degrees  = new List<int>() { };
            foreach (Line line in edges)
            {
                _vts.Add(line.PointAt(0));
                _vts.Add(line.PointAt(1));
                _degrees.Add(1);
                _degrees.Add(1);
            }

            //Rhino.Geometry has available function to do this
            //Point3d[] vts_ = Point3d.CullDuplicates(vts, 0.0001);

            for (int i = _vts.Count - 1; i >= 1; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if ((_vts[i] - _vts[j]).Length < _eps)
                    {
                        _vts.RemoveAt(i);
                        _degrees[j] += _degrees[i];
                        _degrees.RemoveAt(i);
                        break;
                    }
                }
            }

            // array is just a reminder that this data cannot be revised
            vts = _vts.ToArray();
            degrees = _degrees.ToArray();

            // rule: all edges start from row index, and end at column index
            // all duplicated edges will append to the original edge list
            int[,] adjMat = new int[vts.Length, vts.Length];

            // initiation with -1, index referring to nothing, indicating disconnected
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
                // look up for the vertices in vts array
                // this is rather time consuming, only for test
                for (int j = 0; j < vts.Length; j++)
                {
                    if (edges[i].PointAt(0).DistanceTo(vts[j]) < _eps)
                        id_1 = j;
                    if (edges[i].PointAt(1).DistanceTo(vts[j]) < _eps)
                        id_2 = j;
                }
                if (id_1 >= 0 && id_2 >= 0)
                {
                    // at this step, duplicate the edge with its reversed version
                    // the direction is always from left to right, e.g. from row to column index
                    // remember to duplicate the list edges so that the index can match automatically
                    adjMat[id_1, id_2] = i;
                    adjMat[id_2, id_1] = i + edges.Count;
                }
            }
            return adjMat;
        }

        /// <summary>
        /// Return the pair of start/end vertex id of an edge
        /// </summary>
        private static int[] LookupEdgeVts(int[,] adjMat, int edge_id)
        {
            // the edge index can only appear once in this matrix
            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                for (int j = 0; j < adjMat.GetLength(1); j++)
                {
                    if (adjMat[i, j] == edge_id)
                    {
                        return new int[2] { i, j };
                    }
                }
            }
            return new int[2] { -1, -1 };
        }

        /// <summary>
        /// Return the ids of outgoing edges from a vertex
        /// </summary>
        private static int[] LookupEdgeOut(int[,] adjMat, int vt, int vt_in)
        {
            List<int> out_edge_id = new List<int>() { };
            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                if (i == vt)
                {
                    for (int j = 0; j < adjMat.GetLength(1); j++)
                    {
                        if (adjMat[i, j] != -1 && j != vt_in)
                            out_edge_id.Add(adjMat[i, j]);
                    }
                }
            }
            return out_edge_id.ToArray();
        }

        //private static int[] LookupPairVt(int[,] adjMat, int vt_id, out int[] edge_id)
        //{
        //    List<int> out_vt_id = new List<int>() { };
        //    List<int> out_edge_id = new List<int>() { };
        //    for (int i = 0; i < adjMat.GetLength(0); i++)
        //    {
        //        if (i == vt_id)
        //        {
        //            for (int j = 0; j < adjMat.GetLength(1); j++)
        //            {
        //                if (adjMat[i, j] >= 0)
        //                {
        //                    out_vt_id.Add(j);
        //                    out_edge_id.Add(adjMat[i, j]);
        //                }
        //            }
        //        }
        //    }
        //    edge_id = out_edge_id.ToArray();
        //    return out_vt_id.ToArray();
        //}
    }
}
