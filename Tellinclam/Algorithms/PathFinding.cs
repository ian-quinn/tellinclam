using CGAL.Wrapper;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Priority_Queue;


using Tellinclam.Serialization;
using static Tellinclam.Serialization.SchemaJSON;
using System.CodeDom;
using static Tellinclam.Algorithms.PathFinding;
using Rhino.FileIO;

namespace Tellinclam.Algorithms
{
    public class PathFinding
    {
        public enum algoEnum
        {
            MST,        // Minimal Spanning Tree by Kruskal
            SPT         // Shortest Path Tree by Dijkstra
        }

        public class Node<T>
        {
            public int Index { get; set; }
            public T Value { get; set; }
            public Point3d Coords { get; set; }
            public string label { get; set; }
            public bool isRoot { get; set; } = false;
            public int depth { get; set; } = -1; // invalid value by default
            public double Weight { get; set; } = 0;
            public List<Node<T>> Neighbors { get; set; } = new List<Node<T>>();
            public List<Node<T>> Successors { get; set; } = new List<Node<T>>();
            public List<Node<T>> Predecessors { get; set; } = new List<Node<T>>();
            public List<double> Weights { get; set; } = new List<double>();


            public bool AddNeighbors(Node<T> neighbor)
            {
                if (Neighbors.Contains(neighbor))
                {
                    return false;
                }
                else
                {
                    Neighbors.Add(neighbor);
                    return true;
                }
            }

            public bool AddSuccessor(Node<T> successor)
            {
                if (Successors.Contains(successor))
                {
                    return false;
                }
                else
                {
                    Successors.Add(successor);
                    return true;
                }
            }

            public bool AddPredecessor(Node<T> predecessor)
            {
                if (Predecessors.Contains(predecessor))
                {
                    return false;
                }
                else
                {
                    Predecessors.Add(predecessor);
                    return true;
                }
            }

            public bool RemoveNeighbors(Node<T> neighbor)
            {
                // 20240425 remove neighbor's weight as well
                // current graph data structure is not that ideal, use 3rd Pkg
                int id_neighbor = Neighbors.IndexOf(neighbor);
                Weights.RemoveAt(id_neighbor);
                Neighbors.RemoveAt(id_neighbor);
                return true;
            }

            public bool RemoveAllNeighbors()
            {
                for (int i = Neighbors.Count; i >= 0; i--)
                {
                    Neighbors.RemoveAt(i);
                }
                return true;
            }

            public override string ToString()
            {
                StringBuilder nodeString = new StringBuilder();
                nodeString.Append($"[ ID-{Index} Value-{Value} Neighbors- ");
                for (int i = 0; i < Neighbors.Count; i++)
                {
                    nodeString.Append(Neighbors[i].Value + $"-{Weights[i]} "); // + edge.ToString()+ " ");                
                }
                nodeString.Append("]");
                return nodeString.ToString();
            }
        }

        public class Edge<T>
        {
            public Node<T> From { get; set; }
            public Node<T> To { get; set; }
            public double Weight { get; set; }
            public bool isMarked { get; set; }

            public override string ToString()
            {
                return $"WeightedEdge: {From.Value} -> {To.Value}, weight: {Weight}";
            }
        }

        public class PathNodeInfo<T>
        {
            //Graph: internal previous node variable
            Node<T> previous;
            //Graph: constructor to initialize the previous node
            public PathNodeInfo(Node<T> previous)
            {
                this.previous = previous;
            }
            //Graph: Readonly return previous node prop
            public Node<T> Previous
            {
                get
                {
                    return previous;
                }
            }
        }

        public class Graph<T>
        {
            List<Node<T>> nodes = new List<Node<T>>();
            private bool _isDirected = false;

            public Graph(bool isDirected)
            {
                _isDirected = isDirected;
            }

            public int Count { get { return nodes.Count; } }
            public List<Node<T>> Nodes { get { return nodes; } }

            //Increase the Index Value
            public void UpdateIndices()
            {
                int i = 0;
                Nodes.ForEach(n => n.Index = i++);
            }

            // only for trees!
            public void updateDepth(Node<T> node, int depth)
            {
                if (node != null)
                {
                    node.depth = depth;
                    foreach (Node<T> _node in node.Neighbors)
                    {
                        updateDepth(_node, depth + 1);
                    }
                }
            }

            // this .Equals() method limits the usage of graph model
            // two points are not equal with tiny little bit coordinate difference
            // so the comparision between points must have some tolerance, like .DistanceTo() < tol
            // the safe choice is to use integer to construct the graph, then append point to the node
            // or, you need to override Point3d.Equals() to make it useful
            public Node<T> Find(Node<T> graphNode)
            {
                foreach (Node<T> node in nodes)
                {
                    if (node.Value.Equals(graphNode.Value))
                    {
                        return node;
                    }
                }
                return null;
            }

            public Node<T> FindByValue(T value)
            {
                foreach (Node<T> node in nodes)
                {
                    if (node.Value.Equals(value))
                    {
                        return node;
                    }
                }
                return null;
            }

            public Node<T> AddNode(T value, double weight)
            {
                Node<T> node = new Node<T>() { Value = value, Weight = weight};
                if (Find(node) != null)
                {
                    return null;
                }
                else
                {
                    Nodes.Add(node);
                    UpdateIndices();
                    return node;
                }
            }

            public bool AddEdge(Node<T> from, Node<T> to, double weight)
            {
                Node<T> source = Find(from);
                Node<T> destin = Find(to);
                if (source == null || destin == null)
                {
                    return false;
                }
                else if (source.Neighbors.Contains(destin))
                {
                    return false;
                }
                else
                {
                    //for direted graph only below 1st line is required  node1->node2
                    source.AddNeighbors(destin);
                    source.Weights.Add(weight);
                    //for undireted graph the neighbors are both successors and predecessors
                    if (!_isDirected)
                    {
                        source.AddSuccessor(destin);
                        destin.AddPredecessor(source);
                        destin.AddNeighbors(source);
                        destin.Weights.Add(weight);
                        // for junction you need to add different ratios to 
                        // different weight values
                    }
                    return true;
                }
            }

            public bool RemoveNode(Node<T> value)
            {
                Node<T> removeNode = Find(value);
                if (removeNode == null)
                {
                    return false;
                }
                else
                {
                    nodes.Remove(removeNode);
                    foreach (Node<T> node in nodes)
                    {
                        node.RemoveNeighbors(removeNode);
                        RemoveEdge(node, removeNode);
                    }
                    return true;
                }
            }

            public bool RemoveEdge(Node<T> from, Node<T> to)
            {
                Node<T> node1 = Find(from);
                Node<T> node2 = Find(to);
                if (node1 == null || node2 == null)
                {
                    return false;
                }
                else if (!node1.Neighbors.Contains(node2))
                {
                    return false;
                }
                else
                {
                    //for direted graph only below 1st line is required  node1->node2
                    int index = from.Neighbors.FindIndex(n => n == to);
                    if (index >= 0)
                    {
                        from.Neighbors.RemoveAt(index);
                        from.Weights.RemoveAt(index);
                    }
                    //for undireted graph need below line as well
                    index = to.Neighbors.FindIndex(n => n == from);
                    if (index >= 0)
                    {
                        to.Neighbors.RemoveAt(index);
                        to.Weights.RemoveAt(index);
                    }
                    return true;
                }
            }

            // retrieve certain edge from the graph by its start and end node
            public Edge<T> this[int from, int to]
            {
                get
                {
                    Node<T> nodeFrom = Nodes[from];
                    Node<T> nodeTo = Nodes[to];
                    int i = nodeFrom.Neighbors.IndexOf(nodeTo);
                    if (i >= 0)
                    {
                        Edge<T> edge = new Edge<T>()
                        {
                            From = nodeFrom,
                            To = nodeTo,
                            Weight = i < nodeFrom.Weights.Count ? nodeFrom.Weights[i] : 0
                        };
                        return edge;
                    }

                    return null;
                }
            }

            public List<Edge<T>> GetEdges()
            {
                // 20240425 directed/undirected graph applies different formulation
                // current structure saves (u, v) edge as v is one neighbor of u and vice versa
                // thus returning two arcs for one edge in undirected graph
                // if the graph is marked as undirected, then restrict that edge always targets node with larger index
                // (1,4) is okay but (4,1) is illegal

                List<Edge<T>> edges = new List<Edge<T>>();
                foreach (Node<T> from in Nodes)
                {
                    for (int i = 0; i < from.Neighbors.Count; i++)
                    {
                        // new conditions here // beware that it uses .Index for comparing
                        if (!_isDirected && from.Index > from.Neighbors[i].Index)
                            continue;

                        Edge<T> edge = new Edge<T>()
                        {
                            From = from,
                            To = from.Neighbors[i],
                            Weight = i < from.Weights.Count ? from.Weights[i] : 0
                        };
                        edges.Add(edge);
                    }
                }
                return edges;
            }

            private void Fill<Q>(Q[] array, Q value)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = value;
                }
            }

            public override string ToString()
            {
                StringBuilder nodeString = new StringBuilder();
                for (int i = 0; i < Count; i++)
                {
                    nodeString.Append(nodes[i].ToString());
                    if (i < Count - 1)
                    {
                        nodeString.Append("\n");
                    }
                }
                return nodeString.ToString();
            }

            // iterate all nodes in Dijkstra way, get the longest path
            public List<Edge<T>> GetFurthestPathDijkstra(Node<T> source, out int remoteIdx)
            {
                void Fill<Q>(Q[] array, Q value)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = value;
                    }
                }

                int[] previous = new int[Nodes.Count];
                //Set Every Previous node with initial value -1
                Fill(previous, -1);

                float[] distances = new float[Nodes.Count];
                //Set Every Previous node with initial value -1
                Fill(distances, float.PositiveInfinity);
                //Initially distance will be 0 on starting node
                distances[source.Index] = 0;

                //Create SimplePriorityQueue for dynamicall update the priority of each node on the basis of distance and process accordingly
                SimplePriorityQueue<Node<T>> nodes = new SimplePriorityQueue<Node<T>>();
                for (int i = 0; i < Nodes.Count; i++)
                {
                    nodes.Enqueue(Nodes[i], distances[i]);
                }

                while (nodes.Count != 0)
                {
                    Node<T> node = nodes.Dequeue();
                    for (int i = 0; i < node.Neighbors.Count; i++)
                    {
                        Node<T> neighbor = node.Neighbors[i];
                        double weight = i < node.Weights.Count ? node.Weights[i] : 0;
                        double weightTotal = distances[node.Index] + weight;

                        if (distances[neighbor.Index] > weightTotal)
                        {
                            distances[neighbor.Index] = (float)weightTotal;
                            previous[neighbor.Index] = node.Index;
                            nodes.UpdatePriority(neighbor, distances[neighbor.Index]);
                        }
                    }
                }

                List<float> distPile = distances.ToList();
                foreach (double num in distPile)
                {
                    Console.Write($"{num} ");
                }
                remoteIdx = distPile.IndexOf(distPile.Max());

                //Getting all the index
                List<int> indices = new List<int>();
                int index = remoteIdx;
                while (index >= 0)
                {
                    indices.Add(index);
                    index = previous[index];
                }

                //Reverse all the index to get the correct order
                indices.Reverse();
                List<Edge<T>> result = new List<Edge<T>>();
                for (int i = 0; i < indices.Count - 1; i++)
                {
                    Edge<T> edge = this[indices[i], indices[i + 1]];
                    result.Add(edge);
                }
                //return list of WeightedEdge
                return result;
            }

            public List<Edge<T>> GetShortestPathDijkstra(Node<T> source, Node<T> target, out double distance)
            {
                int[] previous = new int[Nodes.Count];
                //Set Every Previous node with initial value -1
                Fill(previous, -1);

                double[] distances = new double[Nodes.Count];
                //Set Every Previous node with initial value -1
                Fill(distances, int.MaxValue);
                //Initially distance will be 0 on starting node
                distances[source.Index] = 0;

                //Create SimplePriorityQueue for dynamicall update the priority of each node on the basis of distance and process accordingly
                SimplePriorityQueue<Node<T>> nodes = new SimplePriorityQueue<Node<T>>();
                for (int i = 0; i < Nodes.Count; i++)
                {
                    nodes.Enqueue(Nodes[i], (float)distances[i]);
                }

                while (nodes.Count != 0)
                {
                    Node<T> node = nodes.Dequeue();
                    for (int i = 0; i < node.Neighbors.Count; i++)
                    {
                        Node<T> neighbor = node.Neighbors[i];
                        double weight = i < node.Weights.Count ? node.Weights[i] : 0;
                        double weightTotal = distances[node.Index] + weight;

                        if (distances[neighbor.Index] > weightTotal)
                        {
                            distances[neighbor.Index] = weightTotal;
                            previous[neighbor.Index] = node.Index;
                            nodes.UpdatePriority(neighbor, (float)distances[neighbor.Index]);
                        }
                    }
                }

                //Getting all the index
                List<int> indices = new List<int>();
                int index = target.Index;
                while (index >= 0)
                {
                    indices.Add(index);
                    index = previous[index];
                }

                //Reverse all the index to get the correct order
                indices.Reverse();
                List<Edge<T>> result = new List<Edge<T>>();
                distance = 0;
                for (int i = 0; i < indices.Count - 1; i++)
                {
                    Edge<T> edge = this[indices[i], indices[i + 1]];
                    result.Add(edge);
                    distance += edge.Weight;
                }
                //return list of WeightedEdge
                return result;
            }

            // avoid to use this on loop graph. only for trees.
            public Graph<T> Graft()
            {
                if (nodes.Count == 0)
                    return this;

                // change the edge direction to make the graph directed
                // iterate all neighbors from the root node
                int rootId = 0;
                bool[] isTraversed = new bool[nodes.Count];
                List<int> nodeIds = new List<int>() { };
                foreach (Node<T> node in nodes)
                {
                    nodeIds.Add(node.Index);
                    if (node.isRoot)
                        rootId = node.Index;
                }

                isTraversed[rootId] = true;
                nodeIds.Remove(rootId);

                int safeCounter = 0;
                while (nodeIds.Count > 0 || safeCounter < 1000)
                {
                    safeCounter++;
                    foreach (Node<T> node in nodes)
                    {
                        safeCounter++;
                        if (!isTraversed[node.Index])
                        {
                            int boolCounter = 0;
                            foreach (Node<T> neighbor in node.Neighbors)
                            {
                                if (isTraversed[neighbor.Index])
                                {
                                    boolCounter++;
                                }
                            }
                            if (boolCounter > 0)
                            {
                                isTraversed[node.Index] = true;
                                nodeIds.Remove(node.Index);
                                for (int i = node.Neighbors.Count - 1; i >= 0; i--)
                                {
                                    int index = node.Neighbors[i].Index;
                                    if (isTraversed[index])
                                    {
                                        node.Neighbors.RemoveAt(i);
                                        node.Weights.RemoveAt(i);
                                    }
                                }
                            }
                        }
                    }
                }

                // update the depth of nodes
                updateDepth(nodes[rootId], 0);

                _isDirected = true;
                return this;
            }

        }

        // ################################# END OF GRAPH MODEL ###################################

        /// <summary>
        /// Connect all terminals to the main pipe guidelines as sub-guidelines
        /// then return shattered line segments based on the union of main and sub guidelines
        /// Elevation: use Manhatten distance, punish any connection across the wall
        /// </summary>
        public static List<Line> GetTerminalConnection(List<Line> edges, List<Point3d> terminals, out List<Line> connections)
        {
            connections = new List<Line>() { };

            // change this to Manhattan distance 
            foreach (Point3d pt in terminals)
            {
                // this absurd default value is for debugging convenience
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
            List<Line> shatters = Basic.BreakLinesAtIntersection(allEdges);

            return shatters;
        }
        
        /// <summary>
        /// Get the sub-graph given a subset of the original graph nodes, 
        /// which may contain Steiner points (also a subset of the original graph nodes)
        /// This may contain loops (often the case)
        /// </summary>
        public static List<Line> GetSubGraph(List<Line> edges, List<Point3d> terminals, out double sum_length)
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

            // get candidate points (either
            // or Steiner pts as relays)
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

            List<Line> branches = Basic.RemoveDupLines(edges_rebuilt, 
                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, out _);
            sum_length = 0;
            foreach (Line branch in branches)
                sum_length += branch.Length;

            return branches;
        }

        /// <summary>
        /// Get the minimum spanning tree from existing graph. However, not all nodes need to be connected.
        /// This is the Steiner tree problem with some supporting Steiner points that are not necessarily included.
        /// </summary>
        public static List<Line> GetSteinerTree(List<Line> edges, List<Point3d> terminals, List<Point3d> sources, algoEnum algorithm)
        {
            // based on the relative sub-graph, generate the minimum spanning tree
            // then remove irrelevant vertices (with degree 1 and not terminal)

            int[,] adjMat = GetAdjMat(edges, out Point3d[] vts, out int[] degrees);

            // retrieve the index of terminals
            List<int> terminalIdx = new List<int>() { };
            List<int> sourceIdx = new List<int>() { };
            for (int i = 0; i < vts.Length; i++)
            {
                for (int j = 0; j < terminals.Count; j++)
                {
                    if (terminals[j].DistanceTo(vts[i]) < 0.0001)
                        if (!terminalIdx.Contains(i))
                            terminalIdx.Add(i);
                }
                for (int j = 0; j < sources.Count; j++)
                {
                    if (sources[j].DistanceTo(vts[i]) < 0.0001)
                        if (!sourceIdx.Contains(i))
                            sourceIdx.Add(i);
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


            // solver
            List<Tuple<int, int>> branches = new List<Tuple<int, int>>() { };
            int branch_counter = 0;

            if (algorithm == algoEnum.MST)
            {
                // calculate the MST
                branches = MinSpanningTree.GetKruskalMST(subEdges, subWeights, out int mstEdgeCount);
                // I am not using dynamic memory allocation in C++ code 
                // so the array returned may have invalid Tuple<int, int> appended
                // int edge_count indicates the actual number of edges in the MST
                branch_counter = mstEdgeCount;
            }
            else if (algorithm == algoEnum.SPT)
            {
                Graph<int> graph = new Graph<int>(false);
                List<int> nodelist = new List<int>() { };
                foreach (Tuple<int, int> edge in subEdges)
                {
                    if (!nodelist.Contains(edge.Item1))
                    {
                        nodelist.Add(edge.Item1);
                        Node<int> newNode = graph.AddNode(edge.Item1, 0);
                        if (edge.Item1 == sourceIdx[0])
                            graph.Nodes.Last().isRoot = true;
                    }
                    if (!nodelist.Contains(edge.Item2))
                    {
                        nodelist.Add(edge.Item2);
                        Node<int> newNode = graph.AddNode(edge.Item2, 0);
                        if (edge.Item2 == sourceIdx[0])
                            graph.Nodes.Last().isRoot = true;
                    }
                    Node<int> nodeFrom = null;
                    Node<int> nodeTo = null;
                    foreach (Node<int> node in graph.Nodes)
                    {
                        if (node.Value == edge.Item1)
                            nodeFrom = node;
                        if (node.Value == edge.Item2)
                            nodeTo = node;
                    }
                    graph.AddEdge(nodeFrom, nodeTo, subWeights[subEdges.IndexOf(edge)]);
                }

                // calculate the SPT
                Graph<int> treeSPT = DijkstraSPT(graph);
                // rebuild all edges
                foreach (Edge<int> edge in treeSPT.GetEdges())
                {
                    branches.Add(new Tuple<int, int>(edge.From.Value, edge.To.Value));
                    // 20240425 the consequnce of removing half edges from GetEdges() results
                    branches.Add(new Tuple<int, int>(edge.To.Value, edge.From.Value));
                }
                branch_counter = branches.Count;
            }


            // trim unnecessary branches. Cull edges with 1 degree vertex that is not a terminal
            // some vertices are removed, so mst_degree may have 0 degree points, idle
            int[] mstDegrees = new int[vts.Length];
            List<int> vts_removed = new List<int>() { };
            bool flag_iteration = true;
            while (flag_iteration)
            {
                // cache degree list
                for (int i = 0; i < branch_counter; i++)
                {
                    if (vts_removed.Contains(branches[i].Item1) ||
                        vts_removed.Contains(branches[i].Item2))
                        continue;
                    mstDegrees[branches[i].Item1]++;
                    mstDegrees[branches[i].Item2]++;
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


            // rebuild the steiner tree edges by mapping back the graph edge to actual line segments
            // mst (i, j) -> adjMat[i, j] -> edges_proxy[k] -> edge[l~m]
            List<Line> st_edges = new List<Line>() { };

            for (int i = 0; i < branch_counter; i++)
            {
                // if any mst edge falls on the vertex within vts_removed, skip it
                if (vts_removed.Contains(branches[i].Item1) ||
                    vts_removed.Contains(branches[i].Item2))
                    continue;

                foreach (int id in edges_proxy[adjMat[branches[i].Item1, branches[i].Item2]])
                    st_edges.Add(edges[id]);
            }

            return st_edges;
        }


        // ######################## Auxiliary Functions ########################


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


        // ######################## Implementation with the Graph model ########################


        public static Graph<int> RebuildGraph(List<Line> edges)
        {
            // compare the edges and trunks then mark the duplicated ones
            // get the center point of the tree as the pre-selected point for AHU
            // reconstruct the tree to make it grows based on the center point
            // you may need the adjacency matrix describing a directional graph

            bool VtsContain(List<Point3d> nodes, Point3d pt, out int dupIdx)
            {
                dupIdx = -1;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].DistanceTo(pt) < 0.000001)
                    {
                        dupIdx = i;
                        return true;
                    }
                }
                return false;
            }

            List<Point3d> vts = new List<Point3d>() { };
            List<Line> droppedEdges = new List<Line>() { }; // for debug
            Graph<int> graph = new Graph<int>(false);

            // iterate all edges, piling up vertices, then rebuild the graph
            foreach (Line edge in edges)
            {
                int startIdx = -1;
                int endIdx = -1;
                if (!VtsContain(vts, edge.PointAt(0), out startIdx))
                {
                    vts.Add(edge.PointAt(0));
                    Node<int> newNode = graph.AddNode(graph.Count, 0);
                    newNode.Coords = edge.PointAt(0);
                    startIdx = graph.Count - 1;
                }
                if (!VtsContain(vts, edge.PointAt(1), out endIdx))
                {
                    vts.Add(edge.PointAt(1));
                    Node<int> newNode = graph.AddNode(graph.Count, 0);
                    newNode.Coords = edge.PointAt(1);
                    endIdx = graph.Count - 1;
                }
                if (graph.AddEdge(graph.Nodes[startIdx], graph.Nodes[endIdx], edge.Length))
                    droppedEdges.Add(edge);
            }

            return graph;
        }

        /// <summary>
        /// Create shortest path tree by Dijkstra Algorithm
        /// Rebuilt from https://github.com/RodrigoMendoza2000/Lab-6-Dijkstra-s-Shortest-Path-Tree
        /// </summary>
        public static Graph<int> DijkstraSPT(Graph<int> graph)
        {
            var _tree = new Dictionary<int, List<Tuple<int, double>>>() { };
            int source = -1;

            // rebuild the graph to dictionary
            var _graph = new Dictionary<int, List<Tuple<int, double>>>() { };
            foreach (Node<int> node in graph.Nodes)
            {
                _graph.Add(node.Value, new List<Tuple<int, double>>() { });
                for (int i = 0; i < node.Neighbors.Count; i++)
                {
                    _graph[node.Value].Add(
                        new Tuple<int, double>(node.Neighbors[i].Value, node.Weights[i]));
                }
                if (node.isRoot)
                {
                    source = node.Value;
                }
            }

            // get visited and unvisited nodes
            List<int> node_unvisited = new List<int>() { };
            List<int> node_visited = new List<int>() { };
            foreach (int key in _graph.Keys)
            {
                node_unvisited.Add(key);
            }
            node_visited.Add(source);

            // get all nodes then assign them with infinite distance
            var cost_prev = new Dictionary<int, Tuple<double, int, double>>() { };
            foreach (int key in _graph.Keys)
            {
                cost_prev.Add(key, new Tuple<double, int, double>(double.PositiveInfinity, -1, 0));
            }
            cost_prev[source] = new Tuple<double, int, double>(0, -1, 0);

            // new graph for cache
            //Graph<int> tree = new Graph<int>(false, true);

            // while unvisited nodes exist, put the lowest cost nodes in cost_prev
            while (node_unvisited.Count > 0)
            {
                int current = -1;
                double min_cost = double.PositiveInfinity;
                foreach (int nodeIdx in node_unvisited)
                {
                    if (cost_prev[nodeIdx].Item1 < min_cost)
                    {
                        min_cost = cost_prev[nodeIdx].Item1;
                        current = nodeIdx;
                    }
                }
                node_unvisited.Remove(current);
                node_visited.Add(current);

                for (int i = 0; i < _graph[current].Count; i++)
                {
                    double new_cost = 0;
                    int neighbor = _graph[current][i].Item1;
                    double cost = _graph[current][i].Item2;
                    if (node_unvisited.Contains(neighbor))
                    {
                        new_cost = cost_prev[current].Item1 + cost;
                        if (new_cost < cost_prev[neighbor].Item1)
                        {
                            cost_prev[neighbor] = new Tuple<double, int, double>(new_cost, current, cost);
                        }
                    }
                }
            }

            // iterate through cost_prev dict and create the return graph
            foreach (KeyValuePair<int, Tuple<double, int, double>> pair in cost_prev)
            {
                if (pair.Value.Item2 != -1)
                {
                    // check if current tree graph includes this value
                    if (_tree.Keys.Contains(pair.Key))
                    {
                        _tree[pair.Key].Add(
                            new Tuple<int, double>(pair.Value.Item2, pair.Value.Item3));
                    }
                    else
                    {
                        _tree[pair.Key] = new List<Tuple<int, double>>()
                        {
                            new Tuple<int, double>(pair.Value.Item2, pair.Value.Item3)
                        };
                    }
                }
            }

            // 
            foreach (KeyValuePair<int, Tuple<double, int, double>> pair in cost_prev)
            {
                if (pair.Value.Item2 != -1)
                {
                    // check if current tree graph includes this value
                    if (_tree.Keys.Contains(pair.Value.Item2))
                    {
                        _tree[pair.Value.Item2].Add(
                            new Tuple<int, double>(pair.Key, pair.Value.Item3));
                    }
                    else
                    {
                        _tree[pair.Value.Item2] = new List<Tuple<int, double>>()
                        {
                            new Tuple<int, double>(pair.Key, pair.Value.Item3)
                        };
                    }
                }
            }

            Graph<int> tree = new Graph<int>(false);
            foreach (KeyValuePair<int, List<Tuple<int, double>>> pair in _tree)
            {
                Node<int> newNode = tree.AddNode(pair.Key, 0);
            }
            foreach (KeyValuePair<int, List<Tuple<int, double>>> pair in _tree)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    tree.FindByValue(pair.Key).Neighbors.Add(tree.FindByValue(pair.Value[i].Item1));
                    tree.FindByValue(pair.Key).Weights.Add(pair.Value[i].Item2);
                }
            }

            return tree;
        }

        /// <summary>
        /// Get the center point of the tree by Dijkstra furtherst path search
        /// Only applied to graph with positive edge weight
        /// </summary>
        public static Point3d GetPseudoRootOfGraph(Graph<int> graph)
        {
            Point3d loc_equip = new Point3d(); // for debug
            if (graph.Count <= 1)
                return loc_equip;

            List<Edge<int>> path_1 = graph.GetFurthestPathDijkstra(graph.Nodes[0], out int remoteIdx_1);
            List<Edge<int>> path_2 = graph.GetFurthestPathDijkstra(graph.Nodes[remoteIdx_1], out int remoteIdx_2);

            double distance = 0;
            foreach (Edge<int> edge in path_2)
            {
                distance += edge.Weight;
            }
            double midDist = distance / 2;
            distance = 0;
            int edgeRemoveIdx = -1;
            int prevNodeIdx = remoteIdx_1;
            
            foreach (Edge<int> edge in path_2)
            {
                // prevNodeIdx records the node sequence in this retrieving path
                // the next edge must start from this node, if not, 
                // the edge may be flipped so the prevNodeIdx for next edge should be edge.From
                edgeRemoveIdx++;
                distance += edge.Weight;
                if (distance == midDist)
                {
                    Point3d midPt = edge.To.Coords;
                    Node<int> mid = edge.To;
                    if (edge.To.Index == prevNodeIdx)
                    {
                        midPt = edge.From.Coords;
                        mid = edge.From;
                    }

                    Line targetEdge = new Line(edge.From.Coords, edge.To.Coords);
                    // offset a little bit as the equiptment position
                    Vector3d dir_offset = Basic.GetPendicularUnitVec(targetEdge.Direction, true);
                    loc_equip = midPt + 0.5 * dir_offset;
                    Node<int> root = graph.AddNode(graph.Count, 0);
                    graph.Nodes.Last().Coords = loc_equip;
                    graph.Nodes.Last().isRoot = true;
                    graph.AddEdge(root, mid, 0.5);

                    break;
                }
                if (distance > midDist)
                {
                    Line targetEdge = new Line(edge.From.Coords, edge.To.Coords);
                    Point3d midPt = targetEdge.PointAt((midDist - distance + edge.Weight) / edge.Weight);
                    graph.RemoveEdge(edge.From, edge.To);

                    Node<int> mid = graph.AddNode(graph.Count, 0);
                    graph.Nodes.Last().Coords = midPt;
                    //graph.Nodes.Last().isRoot = true;
                    graph.AddEdge(edge.From, mid, midDist - distance + edge.Weight);
                    graph.AddEdge(mid, edge.To, distance - midDist);

                    // offset a little bit as the equiptment position
                    Vector3d dir_offset = Basic.GetPendicularUnitVec(targetEdge.Direction, true);
                    loc_equip = midPt + 0.5 * dir_offset;
                    Node<int> root = graph.AddNode(graph.Count, 0);
                    graph.Nodes.Last().Coords = loc_equip;
                    graph.Nodes.Last().isRoot = true;
                    graph.AddEdge(root, mid, 0.5);

                    break;
                }

                if (edge.From.Index != prevNodeIdx)
                    prevNodeIdx = edge.From.Index;
            }

            return loc_equip;
        }

    }

}
