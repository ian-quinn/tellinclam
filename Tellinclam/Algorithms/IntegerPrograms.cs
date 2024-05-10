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
            out List<List<List<Tuple<int, int>>>> forests, out List<List<List<double>>> flowParcels, out List<double[]> optVals)
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
            GRBLinExpr LinearSumByParam(GRBVar[] vars, double[] pars)
            {
                GRBLinExpr expr = new GRBLinExpr();
                for (int i = 0; i < vars.Length; i++)
                {
                    expr.AddTerm(pars[i], vars[i]);
                }
                return expr;
            }
            GRBLinExpr LinearSumById(GRBVar[] vars, int[] ids)
            {
                GRBLinExpr expr = new GRBLinExpr();
                foreach (int id in ids)
                {
                    expr.AddTerm(1.0, vars[id]);
                }
                return expr;
            }
            GRBLinExpr LinearSumAll(GRBVar[] vars)
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
            optVals = new List<double[]>();

            // ----------------------------------- Prepare Graph Info -------------------------------------

            // by default, the source nodes are included in graph input
            // override k if source nodes are provided, in case of collision
            int cardiS = k;
            int cardiV = graph.Nodes.Count;
            if (sources.Count > 0)
                cardiS = sources.Count;
            // V array for the index of terminal/relay nodes
            int[] V = InitiateArray(cardiV);
            // S array for the index of source node
            int[] S = new int[cardiS];
            for (int i = 0; i < cardiS; i++)
                S[i] = cardiV + i;

            // create directed graph G'. dimension = |V| + |S|
            int dim = cardiV + cardiS;
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
                // double the edges, to generate two anti-parallel arcs for each
                adj[edges[i].From.Value, edges[i].To.Value] = 1;
                adj[edges[i].To.Value, edges[i].From.Value] = 1;
                wgte[edges[i].From.Value, edges[i].To.Value] = edges[i].Weight;
                wgte[edges[i].To.Value, edges[i].From.Value] = edges[i].Weight;
                eid.Add(new Tuple<int, int>(edges[i].From.Value, edges[i].To.Value), arc_counter);
                eid.Add(new Tuple<int, int>(edges[i].To.Value, edges[i].From.Value), arc_counter + 1);
                arc_counter += 2;
            }
            // additionally, pair each S with each V
            // if shaft nodes are provided, only pair each S with each V_0 ⊂ V
            if (sources.Count == 0)
            {
                for (int i = 0; i < S.Length; i++)
                {
                    for (int j = 0; j < V.Length; j++)
                    {
                        adj[S[i], V[j]] = 1;
                        wgte[S[i], V[j]] = 0; // virtual connection with 0 weight
                        eid.Add(new Tuple<int, int>(S[i], V[j]), arc_counter);
                        arc_counter += 1;
                    }
                }
            }
            else
            {
                for (int i = 0; i < S.Length; i++)
                {
                    for (int j = 0; j < sources.Count; j++)
                    {
                        adj[S[i], sources[j]] = 1;
                        wgte[S[i], sources[j]] = 0; // virtual connection with 0 weight
                        eid.Add(new Tuple<int, int>(S[i], sources[j]), arc_counter);
                        arc_counter += 1;
                    }
                }
            }

            foreach (PathFinding.Node<int> node in graph.Nodes)
            {
                wgtv[node.Value] = node.Weight;
            }

            double W = wgtv.Sum();
            double[] wgte_dict = new double[eid.Count];
            foreach (KeyValuePair<Tuple<int, int>, int> kvp in eid)
            {
                wgte_dict[kvp.Value] = wgte[kvp.Key.Item1, kvp.Key.Item2];
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

                // single-objective programming
                //mo.SetObjective(LinearSumId(f, EdgeIndexMatch(S[0], RetrieveNeighbors(adj, S[0], 0), 0, eid)), GRB.MINIMIZE);

                // options for multi-objective programming: SetObjectiveN(*)
                // arg:priority 2 > 1 > 0 solver finds the optimum in each priority tier then goes to the next level
                // arg:weight defines the parameter of linear combination in the optimization of each level
                // arg:index marks the output sequence in Model.Parameters.ObjNumber
                mo.ModelSense = GRB.MINIMIZE;
                mo.SetObjectiveN(LinearSumByParam(f, wgte_dict), 0, 2, 1.0, 1.0, 0.01, "UnitCoverage");
                mo.SetObjectiveN(LinearSumById(f, EdgeIndexMatch(S[0], RetrieveNeighbors(adj, S[0], 0), 0, eid)), 1, 1, 1.0, 1.0, 0.01, "LoadVariance");
                mo.SetObjectiveN(LinearSumAll(vari), 2, 0, 1.0, 1.0, 0.01, "FlowVariance");

                for (int i = 0; i < S.Length - 1; i++)
                {
                    var flow_s_i = LinearSumById(f, EdgeIndexMatch(S[i], RetrieveNeighbors(adj, S[i], 0), 0, eid));
                    var flow_s_j = LinearSumById(f, EdgeIndexMatch(S[i + 1], RetrieveNeighbors(adj, S[i + 1], 0), 0, eid));
                    mo.AddConstr(flow_s_i, GRB.GREATER_EQUAL, flow_s_j, $"c1_{i}");
                }

                for (int i = 0; i < V.Length; i++)
                {
                    var flow_v_in = LinearSumById(f, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 1), 1, eid));
                    var flow_v_out = LinearSumById(f, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 0), 0, eid));
                    mo.AddConstr(flow_v_in - flow_v_out, GRB.EQUAL, wgtv[i], $"c2_{i}");
                }

                for (int i = 0; i < eid.Count; i++)
                {
                    mo.AddConstr(f[i], GRB.LESS_EQUAL, W * y[i], $"c3_{i}");
                }

                // S can only deliver flow to atmost 1 node
                for (int i = 0; i < S.Length; i++)
                {
                    var pass_s_out = LinearSumById(y, EdgeIndexMatch(S[i], RetrieveNeighbors(adj, S[i], 0), 0, eid));
                    mo.AddConstr(pass_s_out, GRB.LESS_EQUAL, 1, $"c4_{i}");
                }

                // V can only receive flow from atmost 1 node
                for (int i = 0; i < V.Length; i++)
                {
                    var pass_v_in = LinearSumById(y, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 1), 1, eid));
                    mo.AddConstr(pass_v_in, GRB.LESS_EQUAL, 1, $"c5_{i}");
                }

                // additional constraints for flow balancing. define flow variance among branches
                // first, define the mean value for each outflow of node in V
                for (int i = 0; i < V.Length; i++)
                {
                    mo.AddConstr(mean[i], GRB.EQUAL, f[i] - LinearSumById(f, EdgeIndexMatch(V[i], RetrieveNeighbors(adj, V[i], 0), 0, eid)), $"c6_{i}");
                }
                // second, take the absolute value 
                for (int i = 0; i < eid.Count; i++)
                {
                    mo.AddGenConstrAbs(vari[i], mean[i], $"c7_{i}");
                }

                mo.Write("bcp_k.lp");
                mo.Optimize();

                //double[] x = mo.Get(GRB.DoubleAttr.X, f);

                int nSolutions = mo.SolCount;
                int nObjectives = mo.NumObj;
                Debug.Print($"Gurobi found {nSolutions} solutions for {nObjectives}-objective problem");
                
                for (int s = 0; s < nSolutions; s++)
                {
                    mo.Parameters.SolutionNumber = s;
                    List<double> objNVals = new List<double>();
                    for (int o = 0; o < nObjectives; o++)
                    {
                        mo.Parameters.ObjNumber = o;
                        objNVals.Add(mo.ObjNVal);
                    }
                    optVals.Add(objNVals.ToArray());
                    Debug.Print($"{{{string.Join(", ", objNVals.Select(x => x.ToString()))}}}");

                    // this list stores all branches of this solution temperally, erased with iteration
                    var connections = new List<Tuple<int, int>>();
                    // forest remembers each partition as a tree for this solution, erased with iteration
                    // forest_graft has trees with branches ordered by flow values (descending)
                    // flowParcel packs flow value of each branch of each tree for this solution, erased with iteration
                    var forest = new List<List<Tuple<int, int>>>();
                    var forest_graft = new List<List<Tuple<int, int>>>();
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
                            // switch to pipe length for testing
                            //flows.Add(wgte_summarize[eid[key]]);
                        }

                        // order the tree branches by flow values so you can easily locate the source node
                        // no plan to use a graph data model here
                        var taggedTree = flows.Zip(tree, (_f, _e) => new { FlowValue = _f, Branch = _e })
                                    .OrderByDescending(item => item.FlowValue);
                        flowParcel.Add(taggedTree.Select(item => item.FlowValue).ToList());
                        forest_graft.Add(taggedTree.Select(item => item.Branch).ToList());
                    }

                    // output results
                    flowParcels.Add(flowParcel);
                    forests.Add(forest_graft);
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
