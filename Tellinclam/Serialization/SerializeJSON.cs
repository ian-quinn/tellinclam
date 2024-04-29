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
using static Tellinclam.Serialization.SchemaJSON;


namespace Tellinclam.Serialization
{
    public class SerializeJSON
    {
        //public static string JsonPrettify(this string json)
        //{
        //    var jDoc = JsonDocument.Parse(json);
        //    return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
        //}


        public static SchemaJSON.ConduitGraph PackSubGraph(Graph<int> graph)
        {
            List<SchemaJSON.ConduitNode> jsNodes = new List<SchemaJSON.ConduitNode>() { };
            List<SchemaJSON.ConduitEdge> jsEdges = new List<SchemaJSON.ConduitEdge>() { };
            double sum_length = 0;
            double max_res = 0;
            int max_node = -1;
            bool[] isTraversed = new bool[graph.Count];
            for (int i = 0; i < graph.Count; i++)
            {
                isTraversed[i] = false;
            }

            // generate jsNodes with different IDs, then by the same sequence, refered by the jsEdge
            // the node index must be 0, 1, 2, 3...
            //int max_depth = 0;
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
                jsNodes.Add(new SchemaJSON.ConduitNode
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

            foreach (Edge<int> edge in graph.GetEdges())
            {
                jsEdges.Add(new SchemaJSON.ConduitEdge
                {
                    startId = jsNodes[edge.From.Index].id,
                    endId = jsNodes[edge.To.Index].id,
                    length = edge.Weight, 
                    isTrunk = false, // really?
                    resType = resTypeEnum.duct
                });
            }
            SchemaJSON.ConduitGraph jsGraph = new SchemaJSON.ConduitGraph
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
        public static string InitiateSystem(List<Graph<int>> trunks, List<Graph<int>> graphs,
            List<List<int>> nestedZones, List<List<Point3d>> nested_entry_pts, List<Point3d> ahu_pts, List<double> areas, bool isReadable)
        {
            // generate spaces and networks of all thermal zones
            List<SchemaJSON.ControlZone> jsZones = new List<SchemaJSON.ControlZone>() { };
            string[] space_ids = new string[nested_entry_pts.Count];
            string[] zone_ids = new string[graphs.Count];
            for (int i = 0; i < graphs.Count; i++)
            {
                // initiate the spaces information but leave them blank for now
                List<SchemaJSON.FunctionSpace> jsSpaces = new List<SchemaJSON.FunctionSpace>() { };
                foreach (int spaceId in nestedZones[i])
                {
                    string space_id = Guid.NewGuid().ToString("N").Substring(0, 8);
                    space_ids[spaceId] = space_id;
                    jsSpaces.Add(new SchemaJSON.FunctionSpace
                    {
                        id = space_id, 
                        // pending for replacement
                        // the name should be the same as that the Honeybee generated
                        // to be directly used in Modelica connections.
                        name = $"SPACE_{spaceId}",
                        function = "",
                        area = areas[spaceId],
                        volume = areas[spaceId] * 3.0,
                        maxLoad = 0.0,
                        avgLoad = 0.0
                    });
                }

                ConduitGraph network_zone = PackSubGraph(graphs[i]);
                foreach (ConduitNode node in network_zone.nodes)
                {
                    foreach (List<Point3d> entry_pts in nested_entry_pts)
                    {
                        foreach (Point3d entry_pt in entry_pts)
                        {
                            if (Math.Abs(node.coordU - entry_pt.X) < 0.0001 &&
                                Math.Abs(node.coordV - entry_pt.Y) < 0.0001)
                            {
                                node.linkedTerminalId = space_ids[nested_entry_pts.IndexOf(entry_pts)];
                                // 
                            }
                        }
                    }
                }

                string zone_id = Guid.NewGuid().ToString("N").Substring(0, 8);
                zone_ids[i] = zone_id;
                SchemaJSON.ControlZone jsZone = new SchemaJSON.ControlZone
                {
                    id = zone_id, // leave blank before you figure out how to use it
                    name = $"ZONE_{i}",
                    rooms = jsSpaces,
                    thermostat = "", // will be added in Templating component
                    network = network_zone
                };
                jsZones.Add(jsZone);
            }

            List<SchemaJSON.SystemZone> jsSystems = new List<SchemaJSON.SystemZone>() { };
            // select specific zones then add them to the system zone
            for (int i = 0; i < trunks.Count; i++)
            {
                ConduitGraph network_system = PackSubGraph(trunks[i]);
                List<SchemaJSON.ControlZone> sub_jsZones = new List<SchemaJSON.ControlZone>() { };
                foreach (ConduitNode node in network_system.nodes)
                {
                    foreach (Point3d ahu_pt in ahu_pts)
                    {
                        if (Math.Abs(node.coordU - ahu_pt.X) < 0.0001 &&
                            Math.Abs(node.coordV - ahu_pt.Y) < 0.0001)
                        {
                            node.linkedTerminalId = zone_ids[ahu_pts.IndexOf(ahu_pt)];
                            sub_jsZones.Add(jsZones[ahu_pts.IndexOf(ahu_pt)]);
                        }
                    }
                }

                SchemaJSON.SystemZone jsSystem = new SchemaJSON.SystemZone
                {
                    id = i.ToString(), // leave blank before you figure out how to use it
                    name = $"SYS_{i}",
                    type = "",
                    zones = sub_jsZones,
                    network = network_system
                };
                jsSystems.Add(jsSystem);
            }
            

            // we don't want more layers for now
            // just assume that we have one single system per floorplan
            SchemaJSON.Floorplan jsFloorplan = new SchemaJSON.Floorplan
            {
                id = "",
                systems = jsSystems,
            };

            return JsonSerializer.Serialize(jsFloorplan, new JsonSerializerOptions { WriteIndented = isReadable });
        }
    }
}
