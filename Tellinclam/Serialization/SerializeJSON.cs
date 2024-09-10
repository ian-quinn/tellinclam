using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static Tellinclam.Algorithms.PathFinding;
using Tellinclam.JSON;


namespace Tellinclam
{
    public class SerializeJSON
    {
        //public static string JsonPrettify(this string json)
        //{
        //    var jDoc = JsonDocument.Parse(json);
        //    return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
        //}

        static double _tol = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

        public static ConduitGraph PackSubGraph(Graph<int> graph)
        {
            List<ConduitNode> jsNodes = new List<ConduitNode>();
            List<ConduitEdge> jsEdges = new List<ConduitEdge>();
            double max_res = 0;
            int max_node = -1;
            bool[] isTraversed = new bool[graph.Count];
            for (int i = 0; i < graph.Count; i++)
            {
                isTraversed[i] = false;
            }

            // generate jsNodes with different IDs, then by the same sequence, refered by the jsEdge
            // the node index must be 0, 1, 2, 3...
            // ieration through all nodes
            int numJunction = 0;
            int numBend = 0;
            foreach (Node<int> node in graph.Nodes)
            {
                // the last step is to grow a tree from the graph, 
                // after which the Neighbors only include the next node
                // if Neighbors.Count == 0, that means it is a terminal node
                int degree = node.Neighbors.Count;
                nodeTypeEnum type = nodeTypeEnum.terminal;
                if (node.isRoot)
                    type = nodeTypeEnum.source;
                else if (degree == 1)
                {
                    type = nodeTypeEnum.relay;
                    numBend += 1;
                }
                else if (degree == 2)
                {
                    type = nodeTypeEnum.tjoint;
                    numJunction += 1;
                }

                string node_id = Guid.NewGuid().ToString("N").Substring(0, 8);
                jsNodes.Add(new ConduitNode
                {
                    id = node_id,
                    coordU = node.Coords.X,
                    coordV = node.Coords.Y,
                    degree = degree,
                    type = type,
                    depth = node.depth
                });

                // no need for the height attribute?
                //if (node.depth > max_depth)
                //    max_depth = node.depth;

                //isTraversed[node.Index] = true;
                //for (int i = 0; i < node.Neighbors.Count; i++)
                //{
                //    if (isTraversed[node.Neighbors[i].Index])
                //    {
                //        sum_length += node.Weights[i];
                //    }
                //}

                // find the furthest path. Normally the one with max pressure drop
                if (node.isRoot)
                {
                    List<Edge<int>> path = graph.GetFurthestPathDijkstra(node, out int remoteIdx);
                    for (int i = 0; i < path.Count; i++)
                    {
                        max_res += path[i].Weight;
                        if (i == path.Count - 1)
                            max_node = path[i].To.Index;
                    }
                }
            }
            // iterate once again to assign parent to each node
            // DANGEROUS!
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                foreach (Node<int> node in graph.Nodes[i].Neighbors)
                {
                    jsNodes[node.Index].parent = jsNodes[i].id;
                }
            }

            // iterate through all edges
            foreach (Edge<int> edge in graph.GetEdges())
            {
                jsEdges.Add(new ConduitEdge
                {
                    startId = jsNodes[edge.From.Index].id,
                    endId = jsNodes[edge.To.Index].id,
                    length = edge.Weight, 
                    isTrunk = false // really?
                });
            }
            // leave the length and material calculation to the next step?

            ConduitGraph jsGraph = new ConduitGraph
            {
                maxLength = max_res,
                maxNode = max_node == -1? null : jsNodes[max_node].id,
                sumLength = 0,
                sumMaterial = 0,
                numJunction = numJunction,
                numBend = numBend,
                nodes = jsNodes,
                edges = jsEdges,
            };
            return jsGraph;
        }

        /// <summary>
        /// Take tags as input. Each tag is serialized by space function label and it floor area. Not sure if I should use a 'room' class here.
        /// The best time to implement such 'room' class is at the very beginning.
        /// </summary>
        // tag -> [id, name, function].Serialze();
        public static string InitiateSystem(List<Graph<int>> sysGraphs, List<Graph<int>> zoneGraphs, 
            List<string> spaceFuncs, List<List<int>> nested_spaceIds,  List<List<Point3d>> nested_entryPts, 
            List<Point3d> ahuPts, List<double> spaceAreas, bool isReadable)
        {
            // generate spaces and networks of all thermal zones
            List<ControlZone> jsZones = new List<ControlZone>();
            // batch generation of space and zone GUIDs
            string[] guids_space = new string[nested_entryPts.Count];
            for (int i = 0; i < nested_entryPts.Count; i++)
                guids_space[i] = $"SPACE_{i}_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string[] guids_zone = new string[zoneGraphs.Count];
            for (int i = 0; i < nested_spaceIds.Count; i++)
                guids_zone[i] = $"ZONE_{i}_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // initiate zone models, containing the spaces and the network within
            for (int i = 0; i < zoneGraphs.Count; i++)
            {
                // initiate the spaces information (some fields are blank)
                List<FunctionSpace> jsSpaces = new List<FunctionSpace>();
                foreach (int spaceId in nested_spaceIds[i])
                {
                    jsSpaces.Add(new FunctionSpace
                    {
                        id = guids_space[spaceId], 
                        // the name should be in line with the zone name of IDF
                        // thus Modelica can be linked to EnergyPlus for co-simulation
                        // name = ?
                        function = spaceFuncs[spaceId],
                        area = spaceAreas[spaceId],
                        volume = spaceAreas[spaceId] * 4.5, // default floor height
                        // heatLoad = ?
                        // coolLoad = ?
                    });
                }
                // has some issues... the space pairing should be done when creating the graph
                ConduitGraph jsZoneNetwork = PackSubGraph(zoneGraphs[i]);
                foreach (ConduitNode node in jsZoneNetwork.nodes)
                {
                    // the pairing process should be here, when deciding the node type
                    // like, when you know its a terminal, you should assign the space timely
                    if (node.type == nodeTypeEnum.terminal)
                    {
                        // j stands for the index of space
                        for (int j = 0; j < nested_entryPts.Count; j++)
                        {
                            foreach (Point3d pt in nested_entryPts[j])
                            {
                                if (Math.Abs(node.coordU - pt.X) < _tol &&
                                Math.Abs(node.coordV - pt.Y) < _tol)
                                    node.linkedTerminalId = guids_space[j];
                            }
                        }
                    }
                }

                ControlZone jsZone = new ControlZone
                {
                    id = guids_zone[i], // leave blank before you figure out how to use it
                    rooms = jsSpaces,
                    network = jsZoneNetwork
                };
                jsZones.Add(jsZone);
            }

            List<SystemZone> jsSystems = new List<SystemZone>();
            // select specific zones then add them to the system zone
            for (int i = 0; i < sysGraphs.Count; i++)
            {
                ConduitGraph jsSysNetwork = PackSubGraph(sysGraphs[i]);
                List<ControlZone> jsZones_insys = new List<ControlZone>();
                foreach (ConduitNode node in jsSysNetwork.nodes)
                {
                    foreach (Point3d ahu in ahuPts)
                    {
                        if (Math.Abs(node.coordU - ahu.X) < _tol &&
                            Math.Abs(node.coordV - ahu.Y) < _tol)
                        {
                            node.linkedTerminalId = guids_zone[ahuPts.IndexOf(ahu)];
                            jsZones_insys.Add(jsZones[ahuPts.IndexOf(ahu)]);
                        }
                    }
                }
                // note here all zones are ordered by the network-A connection,
                // may not be the same order as nested_entryPts, now sort it
                // PENDING this can be resolved by matching zone spaces and network at the generation
                jsZones_insys = jsZones_insys.OrderBy(zone => Int32.Parse(zone.id.Split('_')[1])).ToList();

                SystemZone jsSystem = new SystemZone
                {
                    id = $"SYSTEM_{i}_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    // leave name blank before you figure out how to use it
                    zones = jsZones_insys,
                    network = jsSysNetwork
                };
                jsSystems.Add(jsSystem);
            }

            // we don't want more layers for now
            // just assume that we have one single system per floorplan
            Floorplan jsFloorplan = new Floorplan
            {
                id = "",
                systems = jsSystems,
            };

            return JsonSerializer.Serialize(jsFloorplan, new JsonSerializerOptions { WriteIndented = isReadable });
        }
    }
}
