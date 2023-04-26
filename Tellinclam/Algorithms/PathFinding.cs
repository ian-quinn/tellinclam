using CGAL.Wrapper;
using Rhino;
using Rhino.ApplicationSettings;
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
using CGAL.Wrapper;
using Grasshopper.Kernel.Geometry.Delaunay;

namespace Tellinclam.Algorithms
{
    internal class PathFinding
    {
        public static List<Line> GetTerminalConnection(List<Line> edges, List<Point3d> terminals)
        {
            List<Line> connections = new List<Line>() { };

            // change this to Manhattan distance 
            foreach (Point3d pt in terminals)
            {
                Point3d closestPt = new Point3d(0, 0, 0);
                double minDist = double.MaxValue;
                foreach (Line edge in edges)
                {
                    Point3d plummet = edge.ClosestPoint(pt, true);
                    double distance = pt.DistanceTo(plummet);
                    if (distance < minDist)
                    {
                        closestPt = plummet;
                        minDist = distance;
                    }
                }
                connections.Add(new Line(pt, closestPt));
            }

            // recreate the graph
            List<Line> allEdges = new List<Line>() { };
            allEdges.AddRange(edges);
            allEdges.AddRange(connections);
            List<Line> shatters = Basic.ShatterLines(allEdges);

            return shatters;
        }
        /// <summary>
        /// Get the sub-graph given a subset of the original graph nodes, 
        /// which may contain Steiner points (also a subset of the original graph nodes)
        /// This may contain loops (often the case)
        /// </summary>
        public static List<Line> GetSubGraph(List<Line> edges, List<Point3d> terminals)
        {
            // adjacency matrix mapping edge index to vertices incident
            int[,] adjMat = SkeletonPrune.GetAdjMat(edges, out Point3d[] vts, out int[] degrees);
            // distance matrix recording the shortest path distance between any two vertices
            double[,] distMat = new double[vts.Length, vts.Length];
            // previous vertex matrix recording the start point of the shortest path
            int[,] prevMat = new int[vts.Length, vts.Length];

            // initiate the matrix with positive infinite value
            for (int i = 0; i < vts.Length; i++)
            {
                for (int j = 0; j < vts.Length; j++)
                {
                    distMat[i, j] = double.MaxValue;
                    prevMat[i, j] = -1;
                }
            }
            for (int i = 0; i < vts.Length; i++)
            {
                for (int j = 0; j < vts.Length; j++)
                {
                    if (i == j)
                    {
                        distMat[i, j] = 0;
                        prevMat[i, j] = j;
                    }
                    if (adjMat[i, j] >= 0)
                    {
                        distMat[i, j] = edges[adjMat[i, j]].Length;
                        prevMat[i, j] = i;
                    }
                }
            }
            for (int k = 0; k < vts.Length; k++)
                for (int i = 0; i < vts.Length; i++)
                    for (int j = 0; j < vts.Length; j++)
                        if (distMat[i, j] > distMat[i, k] + distMat[k, j])
                        {
                            distMat[i, j] = distMat[i, k] + distMat[k, j];
                            prevMat[i, j] = prevMat[k, j];
                        }

            // get terminal index
            List<int> terminalIds = new List<int>() { };

            for (int i = terminals.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < vts.Length; j++)
                {
                    if (terminals[i].DistanceTo(vts[j]) < 0.0001)
                        if (!terminalIds.Contains(j))
                            terminalIds.Add(j);
                }
            }

            // get candidate points (either terminals or Steiner pts as relays)
            List<int> vtx_included = new List<int>() { };
            List<int> vtx_skipped = new List<int>() { };
            for (int i = 0; i < terminalIds.Count; i++)
            {
                if (vtx_skipped.Contains(terminalIds[i]))
                    continue;
                for (int j = 0; j < terminalIds.Count; j++)
                {
                    // any vertices on this shortest path
                    var vtx_traversed = GetVtxChain(terminalIds[i], terminalIds[j]);
                    vtx_included = MergeTwoList(vtx_included, vtx_traversed);
                    foreach (int idx in vtx_traversed)
                    {
                        if (terminalIds.Contains(idx))
                            vtx_skipped.Append(idx);
                    }
                }
            }

            // regenerate edges
            List<Line> edges_rebuilt = new List<Line>() { };
            for (int i = 0; i < vts.Length; i++)
            {
                for (int j = 0; j < vts.Length; j++)
                {
                    if (i != j)
                        // if both vertices i and j are selected for the subgraph
                        // try to add the edge connecting them into the final subgraph
                        if (vtx_included.Contains(i) && vtx_included.Contains(j))
                            if (adjMat[i, j] != -1)
                                edges_rebuilt.Add(edges[adjMat[i, j]]);
                }
            }

            // utilities

            List<int> GetVtxChain(int start, int end)
            {
                List<int> chain = new List<int>() { };
                if (prevMat[start, end] == -1)
                    return chain;
                chain.Add(end);
                while (start != end)
                {
                    end = prevMat[start, end];
                    chain.Insert(0, end);
                }
                return chain;
            }

            List<int> MergeTwoList(List<int> origin, List<int> appendix)
            {
                List<int> merged = new List<int>() { };
                merged.AddRange(origin);
                foreach (int item in appendix)
                    if (!merged.Contains(item))
                        merged.Add(item);
                return merged;
            }
            return edges_rebuilt;
        }

        public static List<Line> GetSteinerTree(List<Line> edges, List<Point3d> terminals)
        {
            // based on the relative sub-graph, generate the minimum spanning tree
            // then remove irrelevant vertices (with degree 1 and not terminal)

            int[,] adjMat = GetAdjMat(edges, out Point3d[] vts, out int[] degrees);

            // retrieve the index of terminals
            List<int> terminalIdx = new List<int>() { };
            for (int i = 0; i < vts.Length; i++)
            {
                for (int j = 0; j < terminals.Count; j++)
                {
                    if (terminals[j].DistanceTo(vts[i]) < 0.0001)
                        if (!terminalIdx.Contains(i))
                            terminalIdx.Add(i);
                }
            }

            // remove the relay vertices
            // the graph from Floyd-Warshall are only the overlay of the shortest paths
            // especially when it contains a loop, some "shortest paths" may not be included
            // in the minimum spanning tree, for example
            //    ┌─────────────────┐  
            //    A                 │  <- this edge is redundant, though it is the shortest
            //    │                 B     between A-B
            //    │                 │
            //    └──────── C ──────┘
            // here I need an edge_proxy to represent the two line segments, then make it 
            // one edge in the graph model (e.g. remove the redundant Steiner points)

            List<List<int>> edges_proxy = new List<List<int>>() { };
            for (int i = 0; i < edges.Count; i++)
                edges_proxy.Add(new List<int>() { i });

            //                   E
            //               (2)───(5)
            //       A        C     │ <-D
            //  (0)────(3)─────────(4)
            //          │ <-B
            //         (1)    
            // take this graph for example, to erase redundant relay (4) and (5)
            // here is how adjMat updates during each iteration, removed line segments will be added as 
            // a new edge appended to the edges_proxy. MAP: adjMat[i, j] -> edges_proxy[k] -> edge[l~m]

            //   │   │ 0 │ 1 │ 2 │ 3 │ 4 │ 5 │    │   │ 0 │ 1 │ 2 │ 3 │ 4 │ 5 │    │   │ 0 │ 1 │ 2 │ 3 │ 4 │ 5 │
            //   │ 0 │ - │ - │ - │ A │ - │ - │    │ 0 │ - │ - │ - │ A │ - │ - │    │ 0 │ - │ - │ - │ A │ - │ - │
            //   │ 1 │ - │ - │ - │ B │ - │ - │    │ 1 │ - │ - │ - │ B │ - │ - │    │ 1 │ - │ - │ - │ B │ - │ - │
            //   │ 2 │ - │ - │ - │ - │ - │ E │ -> │ 2 │ - │ - │ - │ - │ - │ E │ -> │ 2 │ - │ - │ - │CDE│ - │ * │
            //   │ 3 │ a │ b │ - │ - │ C │ - │    │ 3 │ a │ b │ - │ - │ + │ CD│    │ 3 │ a │ b │cde│ - │ + │ * │
            //   │ 4 │ - │ - │ - │ c │ - │ D │    │ 4 │ - │ - │ - │ + │ - │ + │    │ 4 │ - │ - │ - │ + │ - │ + │
            //   │ 5 │ - │ - │ e │ - │ d │ - │    │ 5 │ - │ - │ e │ cd│ + │ - │    │ 5 │ - │ - │ * │ * │ + │ - │

            //
            List<int> idx_skipped = new List<int>() { };
            for (int i = 0; i < vts.Length; i++)
            {
                if (degrees[i] == 2)
                {
                    idx_skipped.Add(i);
                    List<int> idx_edge = new List<int>() { };
                    List<int> idx_endpoint = new List<int>() { };
                    for (int j = 0; j < vts.Length; j++)
                    {
                        if (i == j) continue;
                        if (adjMat[i, j] >= 0)
                        {
                            idx_edge.AddRange(edges_proxy[adjMat[i, j]]);
                            idx_endpoint.Add(j);
                            adjMat[i, j] = -1;
                            adjMat[j, i] = -1;
                        }
                    }
                    // what if this place has an edge already?
                    adjMat[idx_endpoint[0], idx_endpoint[1]] = edges_proxy.Count;
                    adjMat[idx_endpoint[1], idx_endpoint[0]] = edges_proxy.Count;
                    edges_proxy.Add(idx_edge);
                }
            }

            // retrieve the whole edge connections
            List<Tuple<int, int>> subEdges = new List<Tuple<int, int>>() { };
            List<double> subWeights = new List<double>() { };
            for (int i = 0; i < vts.Length; i++)
            {
                if (idx_skipped.Contains(i)) continue;
                for (int j = i; j < vts.Length; j++)
                {
                    if (idx_skipped.Contains(j) || i == j) continue;
                    if (adjMat[i, j] >= 0)
                    {
                        subEdges.Add(new Tuple<int, int>(i, j));
                        double length_sum = 0;
                        foreach (int idx in edges_proxy[adjMat[i, j]])
                            length_sum += edges[idx].Length;
                        subWeights.Add(length_sum);
                    }
                }
            }

            // calculate the MST
            List<Tuple<int, int>> mst = MinSpanningTree.GetKruskalMST(subEdges, subWeights, out int mstEdgeCount);
            // I am not using dynamic memory allocation in C++ code 
            // so the array returned may have invalid Tuple<int, int> appended
            // int edge_count indicates the actual number of edges in the MST

            // trim unnecessary branches. Cull edges with 1 degree vertex that is not a terminal
            // some vertices are removed, so mst_degree may have 0 degree points, idle
            int[] mstDegrees = new int[vts.Length];
            List<int> vts_removed = new List<int>() { };
            bool flag_iteration = true;
            while (flag_iteration)
            {
                // cache degree list
                for (int i = 0; i < mstEdgeCount; i++)
                {
                    if (vts_removed.Contains(mst[i].Item1) ||
                        vts_removed.Contains(mst[i].Item2))
                        continue;
                    mstDegrees[mst[i].Item1]++;
                    mstDegrees[mst[i].Item2]++;
                }

                // if there is not increment in vts_removed, cancel this iteration
                flag_iteration = false;

                for (int i = 0; i < vts.Length; i++)
                {
                    if (!terminalIdx.Contains(i) && mstDegrees[i] == 1)
                    {
                        vts_removed.Add(i);
                        flag_iteration = true;
                    }
                }

                mstDegrees = new int[vts.Length];
            }


            // rebuild the MST edges by mapping back the graph edge to actual line segments
            // mst (i, j) -> adjMat[i, j] -> edges_proxy[k] -> edge[l~m]
            List<Line> mst_edges = new List<Line>() { };

            for (int i = 0; i < mstEdgeCount; i++)
            {
                // if any mst edge falls on the vertex within vts_removed, skip it
                if (vts_removed.Contains(mst[i].Item1) ||
                    vts_removed.Contains(mst[i].Item2))
                    continue;

                foreach (int id in edges_proxy[adjMat[mst[i].Item1, mst[i].Item2]])
                    mst_edges.Add(edges[id]);
            }

            return mst_edges;
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
                    if (edges[i].PointAt(0).DistanceTo(vts[j]) < 0.00001)
                        id_1 = j;
                    if (edges[i].PointAt(1).DistanceTo(vts[j]) < 0.00001)
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
