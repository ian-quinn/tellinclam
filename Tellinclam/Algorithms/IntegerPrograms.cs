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
        public static bool BalancedConnectedPartition(PathFinding.Graph<int> graph, int k, List<int> sources, 
            out List<List<List<Tuple<int, int>>>> forests, out List<List<List<double>>> flowParcels)
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
            GRBLinExpr LinearSumPar(GRBVar[] vars, double[] pars)
            {
                GRBLinExpr expr = new GRBLinExpr();
                for (int i = 0; i < vars.Length; i++)
                {
                    expr.AddTerm(pars[i], vars[i]);
                }
                return expr;
            }
            GRBLinExpr LinearSumId(GRBVar[] vars, int[] ids)
            {
                GRBLinExpr expr = new GRBLinExpr();
                foreach (int id in ids)
                {
                    expr.AddTerm(1.0, vars[id]);
                }
                return expr;
            }
            GRBLinExpr LinearSum(GRBVar[] vars)
            {
                GRBLinExpr expr = new GRBLinExpr();
                foreach (GRBVar var in vars)
                {
                    expr.AddTerm(1.0, var);
                }
                return expr;
            }

            // ----------------------------------- Initiate outputs ---------------------------------------
            forests = new List<List<List<Tuple<int, int>>>>();
            flowParcels = new List<List<List<double>>>();

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
                    wgte[edges[i].From.Value, edges[i].To.Value] = edges[i].Weight;
                    wgte[edges[i].To.Value, edges[i].From.Value] = edges[i].Weight;
                    eid.Add(new Tuple<int, int> ( edges[i].From.Value, edges[i].To.Value ), arc_counter);
                    eid.Add(new Tuple<int, int> ( edges[i].To.Value, edges[i].From.Value ), arc_counter + 1);
                    arc_counter += 2;
                }
                // for edges induced by S, generate arc from S to V
                else if (S.Contains(edges[i].From.Value))
                {
                    adj[edges[i].From.Value, edges[i].To.Value] = 1;
                    // presume a 0.1m length to shaft connection
                    wgte[edges[i].From.Value, edges[i].To.Value] = 0.1;
                    eid.Add(new Tuple<int, int>(edges[i].From.Value, edges[i].To.Value ), arc_counter);
                    arc_counter += 1;
                }
                else if (S.Contains(edges[i].To.Value))
                {
                    adj[edges[i].To.Value, edges[i].From.Value] = 1;
                    // presume a 0.1m length to shaft connection
                    wgte[edges[i].From.Value, edges[i].To.Value] = 0.1;
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
                        wgte[S[i], V[j]] = 0.1;
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
            double[] wgte_summarize = new double[eid.Count];
            foreach (KeyValuePair<Tuple<int, int>, int> kvp in eid)
            {
                wgte_summarize[kvp.Value] = wgte[kvp.Key.Item1, kvp.Key.Item2];
            }

            Debug.Print("var numbers: " + eid.Count.ToString());

            // ----------------------------------- Gurobi MIP Solver Configuration --------------------------------
            try
            {
                GRBEnv env = new GRBEnv(true);
                env.Set("LogFile", "bcp_k.log");
                env.Set(GRB.DoubleParam.TimeLimit, 300);
                env.Start();

                GRBModel mo = new GRBModel(env);

                GRBVar[] y = mo.AddVars(eid.Count, GRB.BINARY);
                GRBVar[] f = mo.AddVars(eid.Count, GRB.CONTINUOUS);
                // additional for balancing control
                double[] lb = Enumerable.Repeat(-W, eid.Count).ToArray();
                char[] type = Enumerable.Repeat(GRB.CONTINUOUS, eid.Count).ToArray();
                // mean value can be nagetive in which case the lower bound has to be declared
                GRBVar[] mean = mo.AddVars(lb, null, null, type, null);
                GRBVar[] vari = mo.AddVars(eid.Count, GRB.CONTINUOUS);

                mo.SetObjective(LinearSumId(f, EdgeIndexMatch(S[0], RetrieveNeighbors(adj, S[0], 0), 0, eid)), GRB.MINIMIZE);
                // additional for multi-objective programming
                mo.ModelSense = GRB.MINIMIZE;
                mo.SetObjectiveN(LinearSumPar(f, wgte_summarize), 0, 2, 1.0, 1.0, 0.01, "UnitCoverage");
                mo.SetObjectiveN(LinearSum(vari), 1, 1, 1.0, 1.0, 0.01, "FlowVariance");
                mo.SetObjectiveN(LinearSumId(f, EdgeIndexMatch(S[0], RetrieveNeighbors(adj, S[0], 0), 0, eid)), 2, 0, 1.0, 1.0, 0.01, "LoadVariance");

                for (int i = 0; i < S.Length - 1; i++)
                {
                    var flow_s_i = LinearSumId(f, EdgeIndexMatch(S[i], RetrieveNeighbors(adj, S[i], 0), 0, eid));
                    var flow_s_j = LinearSumId(f, EdgeIndexMatch(S[i + 1], RetrieveNeighbors(adj, S[i + 1], 0), 0, eid));
                    mo.AddConstr(flow_s_i, GRB.GREATER_EQUAL, flow_s_j, $"c1_{i}");
                }

                for (int i = 0; i < V.Length; i++)
                {
                    var flow_v_in = LinearSumId(f, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 1), 1, eid));
                    var flow_v_out = LinearSumId(f, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 0), 0, eid));
                    mo.AddConstr(flow_v_in - flow_v_out, GRB.EQUAL, wgtv[i], $"c2_{i}");
                }

                for (int i = 0; i < eid.Count; i++)
                {
                    mo.AddConstr(f[i], GRB.LESS_EQUAL, W * y[i], $"c3_{i}");
                }

                // S can only deliver flow to atmost 1 node
                for (int i = 0; i < S.Length; i++)
                {
                    var pass_s_out = LinearSumId(y, EdgeIndexMatch(S[i], RetrieveNeighbors(adj, S[i], 0), 0, eid));
                    mo.AddConstr(pass_s_out, GRB.LESS_EQUAL, 1, $"c4_{i}");
                }

                // V can only receive flow from atmost 1 node
                for (int i = 0; i < V.Length; i++)
                {
                    var pass_v_in = LinearSumId(y, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 1), 1, eid));
                    mo.AddConstr(pass_v_in, GRB.LESS_EQUAL, 1, $"c5_{i}");
                }

                // additional constraints for flow balancing. define flow variance among branches
                // first, define the mean value for each outflow of node in V
                for (int i = 0; i < V.Length; i++)
                {
                    mo.AddConstr(mean[i], GRB.EQUAL, f[i] - LinearSumId(f, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 0), 0, eid)), $"c6_{i}");
                }
                // second, take the absolute value 
                for (int i = 0; i < eid.Count; i++)
                {
                    mo.AddGenConstrAbs(vari[i], mean[i], $"c7_{i}");
                }

                mo.Write("bcp_k.lp");
                mo.Optimize();
                Debug.Print("Obj: " + mo.ObjVal);

                //double[] x = mo.Get(GRB.DoubleAttr.X, f);

                int nSolutions = mo.SolCount;
                int nObjectives = mo.NumObj;
                Debug.Print($"Problem has {nObjectives} objectives");
                Debug.Print($"Gurobi found {nSolutions} solutions");

                
                for (int s = 0; s < nSolutions; s++)
                {
                    mo.Parameters.SolutionNumber = s;
                    Debug.Print($"Solution {s}: ");
                    for (int o = 0; o < nObjectives; o++)
                    {
                        mo.Parameters.ObjNumber = o;
                        Debug.Print($" {mo.ObjNVal} ");
                    }

                    // this list stores all branches of this solution temperally, erased with iteration
                    var connections = new List<Tuple<int, int>>();
                    // forest remembers each partition as a tree for this solution, erased with iteration
                    // flowParcel packs flow value of each branch of each tree for this solution, erased with iteration
                    var forest = new List<List<Tuple<int, int>>>();
                    var flowParcel = new List<List<double>>();

                    foreach (KeyValuePair<Tuple<int, int>, int> kvp in eid)
                    {
                        if (f[kvp.Value].Xn > 0.00001)
                        {
                            // if no source node is provided before hand, skip edges incident to S
                            //if (sources.Count == 0)
                            //    // eid only contains arc starting from S
                            //    if (S.Contains(kvp.Key.Item1))
                            //    {
                            //        continue;
                            //    }

                            // THIS MAY INCLUDE PHANTOM SOURCE POINTS
                            // DO HANDLE THEM IN TCGenNetwork.cs LATER! 
                            connections.Add(kvp.Key);
                        }
                    }

                    while (connections.Count > 0)
                    {
                        bool flag = true;
                        while (flag)
                        {
                            flag = false;
                            for (int i = 0; i < forest.Count; i++) // tree level
                                for (int j = 0; j < forest[i].Count; j++) // branch level
                                    for (int h = connections.Count - 1; h >= 0; h--) // pending branches
                                        if (isJoined(connections[h], forest[i][j]))
                                        {
                                            forest[i].Add(connections[h]);
                                            connections.RemoveAt(h);
                                            flag = true;
                                            break;
                                        }
                        }
                        if (connections.Count > 0)
                        {
                            forest.Add(new List<Tuple<int, int>>() { connections[connections.Count - 1] });
                            connections.RemoveAt(connections.Count - 1);
                        }
                    }

                    // retreive f[] for each cluster
                    foreach (List<Tuple<int, int>> tree in forest)
                    {
                        List<double> flows = new List<double>();
                        foreach (Tuple<int, int> key in tree)
                        {
                            // round the number to prevent tiny little bit thing
                            flows.Add(Math.Round(f[eid[key]].Xn));
                        }
                        flowParcel.Add(flows);
                    }

                    // output results
                    flowParcels.Add(flowParcel);
                    forests.Add(forest);
                }

                mo.Dispose();
                env.Dispose();

                return true;
            }
            catch (GRBException e)
            {
                Debug.Print("Error code: " + e.ErrorCode + ". " + e.Message);
                return false;
            }
        }
    }
}
