using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tellinclam.Serialization
{
    public class SchemaJSON
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
        // actual functional unit. architectural level
        public class FunctionSpace
        {
            public string id { get; set; }
            public string name { get; set; }
            public string function { get; set; }
            public double area { get; set; }
            public double volume { get; set; }
            public double load { get; set; }
            public double flowrate { get; set; }
            public double heating_set { get; set; }
            public double heating_vent { get; set; }
            public double cooling_set { get; set; }
            public double cooling_vent { get; set; }
        }
        // control level, maintaning the same environment with one thermostat, or compound 
        public class ControlZone
        {
            public string id { get; set; }
            public string name { get; set; }
            public string type { get; set; }
            // the anchor of control thermostat is some kind of an attribute of zone
            // no the space. This can avoid misleading when all classes are flatten
            // if multiple thermostats exist, turn to special logic (by checking termostat.Count)
            public string thermostat { get; set; } 
            public List<FunctionSpace> rooms { get; set; }
            public ConduitGraph network { get; set; }
            // the terminal/control unit location of this zone, id targets to the root point of the zone's network
            // necessary?
            public string id_root { get; set; }
        }
        // distribution level, for better organization and balance
        public class SystemZone
        {
            public string id { get; set; }
            public string name { get; set; }
            public string type { get; set; }
            public List<ControlZone> zones { get; set; }
            public ConduitGraph network { get; set; }
        }
        public class Floorplan
        {
            public string id { get; set; }
            public List<SystemZone> systems { get; set; }
        }

        // ---------------------------------------------------------------------

        public class ConduitGraph
        {
            public List<ConduitNode> nodes { get; set; }
            public List<ConduitEdge> edges { get; set; }
            public double maxRes { get; set; }
            public double sumLength { get; set; }
        }
        public class ConduitNode
        {
            public string id { get; set; }
            public double coordU { get; set; }
            public double coordV { get; set; }
            public nodeTypeEnum type { get; set; }
            public int degree { get; set; }
            // based on the presumption that all network used in the distribution system
            // is some kind of a tree, a binary tree (no 4-way joint allowed)
            public int depth { get; set; }
            public string linkedTerminalId { get; set; }
            public double flowrate { get; set; } = 0.0;
        }
        public class ConduitEdge
        {
            public string startId { get; set; }
            public string endId { get; set; }
            public double length { get; set; }
            public bool isTrunk { get; set; } = false;
            public resTypeEnum resType { get; set; } = resTypeEnum.duct; // by default
            public double flowrate { get; set; } = 0.0;

            //public ConduitEdge(string startId, string endId, double length)
            //{
            //    this.startId = startId;
            //    this.endId = endId;
            //    this.length = length;
            //}
        }
    }
}
