using System;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Tellinclam.JSON
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum nodeTypeEnum
    {
        tjoint,      // representing T-junctions
        relay,      // intermedian points representing relay that can be erased in estimating resistance
        terminal,   // terminal points representing vents/AHUs/VAV boxes
        source      // the entry point of the current distribution network
    }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum resTypeEnum
    {
        pipe,
        duct
    }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum sysTypeEnum
    {
        IDE,
        FCU,
        VAV,
        RAD,
        VRF
    }

    // JSON for general graph representation, D3.js and NetworkX compatitable
    public class Node
    {
        public int id { get; set; }
        public double weight { get; set; }
    }
    public class Link
    {
        public int source { get; set; }
        public int target { get; set; }
        public double weight { get; set; }
    }
    public class Graph
    {
        public List<Node> nodes { get; set; }
        public List<Link> links { get; set; }
    }

    // actual functional unit. architectural level
    public class FunctionSpace
    {
        public string id { get; set; }
        public string name { get; set; }
        public string function { get; set; }
        public double area { get; set; }
        public double volume { get; set; }
        public double heatLoad { get; set; }
        public double coolLoad { get; set; }
        public double airHeaFlow { get; set; }
        public double airCooFlow { get; set; }
        public double airOutFlow { get; set; }
    }
    // control level, maintaning the same environment with one thermostat, or compound 
    public class ControlZone
    {
        public string id { get; set; }
        public string name { get; set; }
        // the anchor of control thermostat is some kind of an attribute of zone
        // not the space. This can avoid misleading when all classes are flatten
        // if multiple thermostats exist, turn to special logic (by checking termostat.Count)
        public string sensor { get; set; } 
        public double heatLoad { get; set; }
        public double coolLoad { get; set; }
        public double airHeaFlow { get; set; }
        public double airCooFlow { get; set; }
        public double airOutFlow { get; set; }
        public double wFlow { get; set; }
        public string id_root { get; set; }
        public List<FunctionSpace> rooms { get; set; }
        public ConduitGraph network { get; set; }
        // the terminal/control unit location of this zone, id targets to the root point of the zone's network
        // necessary?
    }

    // distribution level, for better organization and balance
    public class SystemZone
    {
        public string id { get; set; }
        public string name { get; set; }
        public sysTypeEnum type { get; set; }
        public double[] schedule { get; set; }
        public double heatLoad { get; set; }
        public double coolLoad { get; set; }
        public double heatSet { get; set; }
        public double heatSupply { get; set; }
        public double coolSet { get; set; }
        public double coolSupply { get; set; }
        public double chwTempSupply { get; set; }
        public double chwTempDelta { get; set; }
        public double hwTempSupply { get; set; }
        public double hwTempDelta { get; set; }
        public double chwFlow { get; set; }
        public double hwFlow { get; set; }
        public double airHeaFlow { get; set; }
        public double airCooFlow { get; set; }
        public double airOutFlow { get; set; }
        public List<ControlZone> zones { get; set; }
        public ConduitGraph network { get; set; }
    }
    public class Floorplan
    {
        public string id { get; set; }
        public double level { get; set; }
        public List<SystemZone> systems { get; set; }
    }

    // ---------------------------------------------------------------------

    public class ConduitGraph
    {
        public double maxLength { get; set; }
        public string maxNode { get; set; }
        public double sumLength { get; set; }
        public double sumMaterial { get; set; }
        public int numJunction { get; set; }
        public int numBend { get; set; }
        public resTypeEnum type { get; set; } // by default
        public List<ConduitNode> nodes { get; set; }
        public List<ConduitEdge> edges { get; set; }
    }
    public class ConduitNode
    {
        public string id { get; set; }
        public string parent { get; set; }
        public double coordU { get; set; }
        public double coordV { get; set; }
        public nodeTypeEnum type { get; set; }
        public int degree { get; set; }
        // based on the presumption that all network used in the distribution system
        // is some kind of a tree, a binary tree (no 4-way joint allowed)
        public int depth { get; set; }
        public string linkedTerminalId { get; set; }
        // air/water mass flowrate kg/s
        public double massFlow { get; set; } = 0.0;
    }
    public class ConduitEdge
    {
        public string startId { get; set; }
        public string endId { get; set; }
        public double length { get; set; }
        public bool isTrunk { get; set; } = false;
        public double massFlow { get; set; } = 0.0;
        public int diameter { get; set; }
        public double velocity { get; set; }
        public double friction { get; set; }

        //public ConduitEdge(string startId, string endId, double length)
        //{
        //    this.startId = startId;
        //    this.endId = endId;
        //    this.length = length;
        //}
    }

    public class SimulationSettings
    {
        public string info { get; set; }
        public int startTime { get; set; }
        public int stopTime { get; set; }
        public double interval { get; set; }
        public double tolerance { get; set; }
        public string algorithm { get; set; }
    }
}
