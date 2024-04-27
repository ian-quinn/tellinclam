using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gurobi;

namespace Tellinclam.Algorithms
{
    public class IntegerPrograms
    {
        /// <summary>
        /// Handles the mixed integer linear program on balanced k-partition on graph, outputs connected sub-graphs with
        /// pre-defined source nodes assigned for each
        /// </summary>
        public static List<List<Tuple<int, int>>> BalancedConnectedPartition(PathFinding.Graph<int> graph, int k, List<int> sources)
        {
            bool isJoined(Tuple<int, int> e1, Tuple<int, int> e2)
            {
                List<int> ids = new List<int> { e1.Item1, e1.Item2, e2.Item1, e2.Item2 };
                HashSet<int> unique_ids = new HashSet<int>();
                foreach (int id in ids)
                    if (!unique_ids.Add(id))
                        return true;
                return false;
            }

            // -----------------------------------------

            int[] InitiateArray(int n)
            {
                int[] res = new int[n];
                for (int i = 0; i < n; i++)
                    res[i] = i;
                return res;
            }
            int[] RetrieveNeighbors(int[,] adjacency, int id, int direction)
            {
                int n = adjacency.GetLength(0);
                List<int> neighbors = new List<int>() { };
                for (int i = 0; i < n; i++)
                {
                    if (direction == 0 && adjacency[id, i] > 0)
                    {
                        neighbors.Add(i);
                    }
                    if (direction == 1 && adjacency[i, id] > 0)
                    {
                        neighbors.Add(i);
                    }
                }
                return neighbors.ToArray();
            }
            int[] EdgeIndexMatch(int id, int[] neighbors, int direction, Dictionary<Tuple<int, int>, int> dict)
            {
                List<int> edge_ids = new List<int>() { };
                for (int i = 0; i < neighbors.Length; i++)
                {
                    if (direction == 0)
                        edge_ids.Add(dict[new Tuple<int, int>(id, neighbors[i])]);
                    else
                        edge_ids.Add(dict[new Tuple<int, int>(neighbors[i], id)]);
                }
                return edge_ids.ToArray();
            }
            GRBLinExpr LinearSum(GRBVar[] vars, int[] ids)
            {
                GRBLinExpr expr = new GRBLinExpr();
                foreach (int id in ids)
                {
                    expr.AddTerm(1.0, vars[id]);
                }
                return expr;
            }

            // ----------------------------------- Prepare Graph Info -------------------------------------

            // by default, the source nodes are included in graph input
            // override k if source nodes are provided, in case of collision
            if (sources.Count > 0)
                k = sources.Count;
            // figure out the dimension of adjacency matrix
            int dim = graph.Nodes.Count;
            if (sources.Count == 0)
                dim = graph.Nodes.Count + k;
            // S array for the index of source node
            int[] S = new int[k];
            for (int i = 0; i < k; i++)
            {
                if (sources.Count == 0)
                    S[i] = graph.Nodes.Count + i;
                else
                    S[i] = sources[i];
            }
            // V array for the index of terminal/relay nodes
            int[] V = InitiateArray(dim - k);

            // for an undirected graph the adj matrix must be symmetric
            int[,] adj = new int[dim, dim];
            double[,] wgte = new double[dim, dim];
            double[] wgtv = new double[dim];
            // usage eid[(i, j)]
            Dictionary<Tuple<int, int>, int> eid = new Dictionary<Tuple<int, int>, int>() { };

            List<PathFinding.Edge<int>> edges = graph.GetEdges();
            int arc_counter = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                // for edges closed by V, generate two anti-parallel arcs
                if (!S.Contains(edges[i].From.Value) && !S.Contains(edges[i].To.Value))
                {
                    adj[edges[i].From.Value, edges[i].To.Value] = 1;
                    adj[edges[i].To.Value, edges[i].From.Value] = 1;
                    eid.Add(new Tuple<int, int> ( edges[i].From.Value, edges[i].To.Value ), arc_counter);
                    eid.Add(new Tuple<int, int> ( edges[i].To.Value, edges[i].From.Value ), arc_counter + 1);
                    arc_counter += 2;
                }
                // for edges induced by S, generate arc from S to V
                else if (S.Contains(edges[i].From.Value))
                {
                    adj[edges[i].From.Value, edges[i].To.Value] = 1;
                    eid.Add(new Tuple<int, int>(edges[i].From.Value, edges[i].To.Value ), arc_counter);
                    arc_counter += 1;
                }
                else if (S.Contains(edges[i].To.Value))
                {
                    adj[edges[i].To.Value, edges[i].From.Value] = 1;
                    eid.Add(new Tuple<int, int>(edges[i].To.Value, edges[i].From.Value ), arc_counter);
                    arc_counter += 1;
                }
                Debug.Print("Edge prev: " + edges[i].To.Value.ToString() + " , " + edges[i].From.Value.ToString());
            }
            // if no source node is provided, generate phantom source nodes pointing to all V
            if (sources.Count == 0)
            {
                for (int i = 0; i < S.Length; i++)
                {
                    for (int j = 0; j < V.Length; j++)
                    {
                        adj[S[i], V[j]] = 1;
                        eid.Add(new Tuple<int, int>(S[i], V[j]), arc_counter);
                        arc_counter += 1;
                    }
                }
            }

            foreach (PathFinding.Node<int> node in graph.Nodes)
            {
                wgtv[node.Value] = node.Weight;
            }
            double W = wgtv.Sum();

            Debug.Print("var numbers: " + eid.Count.ToString());

            List<Tuple<int, int>> connections = new List<Tuple<int, int>>() { };
            List<List<Tuple<int, int>>> clusters = new List<List<Tuple<int, int>>>() { };

            // ----------------------------------- Gurobi MIP Solver Configuration --------------------------------
            try
            {
                GRBEnv env = new GRBEnv(true);
                env.Set("LogFile", "bcp_k.log");
                env.Set(GRB.DoubleParam.TimeLimit, 600);
                env.Start();

                GRBModel mo = new GRBModel(env);

                GRBVar[] y = mo.AddVars(eid.Count, GRB.BINARY);
                GRBVar[] f = mo.AddVars(eid.Count, GRB.CONTINUOUS);
                
                mo.SetObjective(LinearSum(f, EdgeIndexMatch(S[0], RetrieveNeighbors(adj, S[0], 0), 0, eid)), GRB.MAXIMIZE);

                for (int i = 0; i < S.Length - 1; i++)
                {
                    var flow_s_i = LinearSum(f, EdgeIndexMatch(S[i], RetrieveNeighbors(adj, S[i], 0), 0, eid));
                    var flow_s_j = LinearSum(f, EdgeIndexMatch(S[i + 1], RetrieveNeighbors(adj, S[i + 1], 0), 0, eid));
                    mo.AddConstr(flow_s_i, GRB.LESS_EQUAL, flow_s_j, $"c1_{i}");
                }

                for (int i = 0; i < V.Length; i++)
                {
                    var flow_v_in = LinearSum(f, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 1), 1, eid));
                    var flow_v_out = LinearSum(f, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 0), 0, eid));
                    mo.AddConstr(flow_v_in - flow_v_out, GRB.EQUAL, wgtv[i], $"c2_{i}");
                }

                for (int i = 0; i < eid.Count; i++)
                {
                    mo.AddConstr(f[i], GRB.LESS_EQUAL, W * y[i], $"c3_{i}");
                }

                // S can only deliver flow to atmost 1 node
                for (int i = 0; i < S.Length; i++)
                {
                    var pass_s_out = LinearSum(y, EdgeIndexMatch(S[i], RetrieveNeighbors(adj, S[i], 0), 0, eid));
                    mo.AddConstr(pass_s_out, GRB.LESS_EQUAL, 1, $"c4_{i}");
                }

                // V can only receive flow from atmost 1 node
                for (int i = 0; i < V.Length; i++)
                {
                    var pass_v_in = LinearSum(y, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 1), 1, eid));
                    mo.AddConstr(pass_v_in, GRB.LESS_EQUAL, 1, $"c5_{i}");
                }

                mo.Write("bcp_k.lp");
                mo.Optimize();
                Debug.Print("Obj: " + mo.ObjVal);

                double[] x = mo.Get(GRB.DoubleAttr.X, y);
                foreach (KeyValuePair<Tuple<int, int>, int> kvp in eid)
                {
                    if (y[kvp.Value].X > 0)
                    {
                        // if no source node is provided before hand, remove the edges incident to S
                        if (sources.Count == 0)
                            // eid only contains arc starting from S
                            if (S.Contains(kvp.Key.Item1))
                                continue;
                        connections.Add(kvp.Key);
                    }
                }

                while (connections.Count > 0)
                {
                    bool flag = true;
                    while (flag)
                    {
                        flag = false;
                        for (int i = 0; i < clusters.Count; i++)
                            for (int j = 0; j < clusters[i].Count; j++)
                                for (int h = connections.Count - 1; h >= 0; h--)
                                    if (isJoined(connections[h], clusters[i][j]))
                                    {
                                        clusters[i].Add(connections[h]);
                                        connections.RemoveAt(h);
                                        flag = true;
                                        break;
                                    }
                    }
                    if (connections.Count > 0)
                    {
                        clusters.Add(new List<Tuple<int, int>>() { connections[connections.Count - 1] });
                        connections.RemoveAt(connections.Count - 1);
                    }
                }

                mo.Dispose();
                env.Dispose();

                Debug.Print("Cluster number: " + clusters.Count.ToString());
                return clusters;
            }
            catch (GRBException e)
            {
                Debug.Print("Error code: " + e.ErrorCode + ". " + e.Message);
                return clusters;
            }
        }
    }
}
