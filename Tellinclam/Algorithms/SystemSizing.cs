using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using PsychroLib;
using Tellinclam.JSON;
using System.Xml.Linq;

namespace Tellinclam.Algorithms
{
    internal class SystemSizing
    {
        public static void Sizing(SystemZone jsSystem, List<string> spaceIdfNames, List<int> sensorLocs, 
            List<double> heatLoads, List<double> coolLoads, double exp, List<double> heatTemps, List<double> coolTemps)
        {
            // inject some information to the JSON file
            // this is only valid for fan-coil system, maybe
            jsSystem.chwTempSupply = 12; // chilled water 7 ~ 12
            jsSystem.chwTempDelta = 5;
            jsSystem.hwTempSupply = 50; // hot water 45 ~ 50
            jsSystem.hwTempDelta = 5;
            // following should be listed in the zone properties
            jsSystem.heatSet = heatTemps[0];
            jsSystem.coolSet = coolTemps[0];
            jsSystem.heatSupply = 35;
            jsSystem.coolSupply = 18;

            foreach (ControlZone jsZone in jsSystem.zones)
            {
                double heatLoad = 0;
                double coolLoad = 0;
                foreach (FunctionSpace jsSpace in jsZone.rooms)
                {
                    int spaceId = Convert.ToInt32(jsSpace.id.Split('_')[1]);
                    // check if the input zone names of IDF are in line with the JSON
                    if (Convert.ToInt32(spaceIdfNames[spaceId].Split('_')[1]) != spaceId)
                        Debug.Print("IDF space name not in accordance with the JSON space ID");
                    jsSpace.name = spaceIdfNames[spaceId];
                    jsSpace.heatLoad = heatLoads[spaceId];
                    jsSpace.coolLoad = coolLoads[spaceId];
                    heatLoad += jsSpace.heatLoad;
                    coolLoad += jsSpace.coolLoad;
                    // mapping the space label to the schedule/control setting, then assign it to the jsSpace
                }
                int zoneId = Convert.ToInt32(jsZone.id.Split('_')[1]);
                if (sensorLocs[zoneId] >= 0)
                    jsZone.sensor = spaceIdfNames[sensorLocs[zoneId]];
                else if (sensorLocs[zoneId] == -1)
                    jsZone.sensor = "TempReturnDuct";
                else if (sensorLocs[zoneId] == -2)
                    jsZone.sensor = "TempAverage";

                jsZone.heatLoad = heatLoad; // safe factor maybe
                jsZone.coolLoad = coolLoad; 
                // all zones share the same setpoint by system property
                //jsZone.heatSet = heatTemps[zoneId];
                //jsZone.coolSet = coolTemps[zoneId];
                // according to the system type. however, this value should be altered so that the duct system
                // suits for both the heating and cooling needs. this trial process needs come coding
                //jsZone.heatSupply = 35;
                //jsZone.coolSupply = 18;
            }

            // calculate the design flowrate by load / (temp_supply - temp_set)
            var psySI = new Psychrometrics(PsychroLib.UnitSystem.SI);
            //Dictionary<string, double> terminalZoneFlow = new Dictionary<string, double>() { };
            foreach (ControlZone jsZone in jsSystem.zones)
            {
                // get the dictionary of space and loads
                foreach (FunctionSpace jsSpace in jsZone.rooms)
                {
                    // 这里应该以冷负荷为准，冬季有内热余量的
                    // var rho = psySI.GetDryAirDensity(sizingSet, 101325);
                    var airHeaFlow = exp * jsSpace.heatLoad / Math.Abs(psySI.GetDryAirEnthalpy(jsSystem.heatSupply) - psySI.GetDryAirEnthalpy(jsSystem.heatSet));
                    var airCooFlow = exp * jsSpace.coolLoad / Math.Abs(psySI.GetDryAirEnthalpy(jsSystem.coolSet) - psySI.GetDryAirEnthalpy(jsSystem.coolSupply));
                    // var speed -> ?
                    jsSpace.airHeaFlow = airHeaFlow;
                    jsSpace.airCooFlow = airCooFlow;
                    //jsSpace.airOutFlow = jsSpace.area * 0.1 * 0.00833;
                    jsSpace.airOutFlow = jsSpace.area * 0.0003; // area weighted OA by default
                    if (jsSpace.function.Contains("oz:Office"))
                        jsSpace.airOutFlow += jsSpace.area * 0.05 * 0.0025;
                    if (jsSpace.function.Contains("oz:Lounge"))
                        jsSpace.airOutFlow += jsSpace.area * 0.3 * 0.0025;
                    if (jsSpace.function.Contains("oz:Laboratory"))
                        jsSpace.airOutFlow += jsSpace.area * 0.27 * 0.0025;
                    if (jsSpace.function.Contains("oz:Classroom"))
                        jsSpace.airOutFlow += jsSpace.area * 0.7 * 0.0025;
                    if (jsSpace.function.Contains("oz:Conference"))
                        jsSpace.airOutFlow += jsSpace.area * 0.5 * 0.0025;
                    // leakage 0.3*VRoo*1.2/3600 Outdoor air mass flow rate, assuming constant infiltration air flow rate [kg/s]
                    //jsSpace.airOutFlow = 0.3 * jsSpace.volume * 1.2 / 3600;
                    //terminalSpaceFlow.Add(jsSpace.id, aFlow);
                }
                // the flowrate can be rounded up by sizing factor upon load value, no need to do it here
                jsZone.airHeaFlow = jsZone.rooms.Sum(space => space.airHeaFlow);
                jsZone.airCooFlow = jsZone.rooms.Sum(space => space.airCooFlow);
                jsZone.airOutFlow = jsZone.rooms.Sum(space => space.airOutFlow);
                // not considering radiation system for now, network within a control zone must be air duct
                if (jsSystem.type != sysTypeEnum.IDE)
                    foreach (ConduitNode jsNode in jsZone.network.nodes)
                    {
                        if (jsNode.linkedTerminalId != null)
                        {
                            var terminalSpace = jsZone.rooms.Where(jsSpace => jsSpace.id == jsNode.linkedTerminalId).First();
                            jsNode.massFlow = terminalSpace.airHeaFlow > terminalSpace.airCooFlow ? terminalSpace.airHeaFlow : terminalSpace.airCooFlow;
                        }
                    }

                // presumtion rho = 1000 kg/m3, cp = 4186.8 J/kg, note that load value in W
                var wFlowHeat = jsZone.heatLoad / jsSystem.hwTempDelta / 4186;
                var wFlowCool = jsZone.coolLoad / jsSystem.chwTempDelta / 4186;
                jsZone.wFlow = wFlowHeat > wFlowCool ? wFlowHeat : wFlowCool;
            }

            // do water flowrate calculation, 0.09 ~ 0.18 kg/s, round up a little bit
            jsSystem.heatLoad = jsSystem.zones.Sum(zone => zone.heatLoad);
            jsSystem.coolLoad = jsSystem.zones.Sum(zone => zone.coolLoad);
            jsSystem.airHeaFlow = jsSystem.zones.Sum(zone => zone.airHeaFlow);
            jsSystem.airCooFlow = jsSystem.zones.Sum(zone => zone.airCooFlow);
            jsSystem.airOutFlow = jsSystem.zones.Sum(zone => zone.airOutFlow);
            jsSystem.chwFlow = jsSystem.coolLoad / jsSystem.chwTempDelta / 4186;
            jsSystem.hwFlow = jsSystem.heatLoad / jsSystem.hwTempDelta / 4186;
            // if zone terminals are connected by water loop, mark jsNode.massFlow with zone sizing water flow rate
            // if zone terminals are connected by air loop, mark jsNode.massFlow with zone sizing air flow rate
            if (jsSystem.type != sysTypeEnum.IDE)
                foreach (ConduitNode jsNode in jsSystem.network.nodes)
                {
                    if (jsNode.linkedTerminalId != null)
                    {
                        var terminalZone = jsSystem.zones.Where(jsZone => jsZone.id == jsNode.linkedTerminalId).First();
                        if (jsSystem.type == sysTypeEnum.FCU)
                            jsNode.massFlow = terminalZone.wFlow;
                        else if (jsSystem.type == sysTypeEnum.VAV)
                            jsNode.massFlow = terminalZone.airHeaFlow > terminalZone.airCooFlow ? terminalZone.airHeaFlow : terminalZone.airCooFlow;
                    }
                }

            // if not ideal system, compile the zone-level network, according to system-level medium, compile pipe/duct
            if (jsSystem.type != sysTypeEnum.IDE)
            {
                foreach (ControlZone jsZone in jsSystem.zones)
                    Ducting(jsZone.network);
                if (jsSystem.type == sysTypeEnum.FCU)
                    Piping(jsSystem.network);
                if (jsSystem.type == sysTypeEnum.VAV)
                    Ducting(jsSystem.network);
            }

            return;
        }
        public static void Ducting(ConduitGraph jsNetwork)
        {
            jsNetwork.type = resTypeEnum.duct;

            // Update the pressure loss of each duct/pipe by Equal Friction Method
            double epsilon = 0.0001524; // absolute roughness of Galvanized Steel air duct
            double density = 1.2;
            double viscosity = 1.81e-5;
            List<double> d_DN = new List<double>() {0.1, 0.125, 0.15, 0.175, 0.2, 0.225, 0.25, 0.3, 0.35, 0.4, 0.45,
                0.5, 0.55, 0.6, 0.65, 0.7, 0.75, 0.8, 0.85, 0.9, 0.95, 1.0, 1.1, 1.2, 1.3,
                1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 2.0, 2.2, 2.4, 2.6, 2.8, 3.0, 3.5};

            // calculate the flow rate at each network node
            var dict_jsNode = new Dictionary<string, ConduitNode>() { };
            foreach (ConduitNode jsNode in jsNetwork.nodes)
                dict_jsNode.Add(jsNode.id, jsNode);
            // rank edges by their depth. follow this sequence,
            // each edge should copy the end point value to the start point (terminal -> source)
            var edges_ranked = jsNetwork.edges.OrderBy(x => -dict_jsNode[x.endId].depth);
            foreach (ConduitEdge jsEdge in edges_ranked)
            {
                // the flowrate of this edge equals to the end point
                jsEdge.massFlow = dict_jsNode[jsEdge.endId].massFlow;
                dict_jsNode[jsEdge.startId].massFlow += dict_jsNode[jsEdge.endId].massFlow;
            }

            var mainDuct = jsNetwork.edges.OrderByDescending(edge => edge.massFlow).First();
            double v_guess = 5; // presume the maximum velocity acceptable is 5 m/s
            double d_guess = Math.Pow(4 * mainDuct.massFlow / density / Math.PI / v_guess, 0.5);
            // pick diameter from DN list that right above d_guess (minimum d - d_guess larger than 0)
            double d_main = 0.3;
            if (d_guess > d_DN[d_DN.Count - 1])
                d_main = d_DN[d_DN.Count - 1];
            else
                d_main = d_DN.Where(d => d - d_guess > 0).Min();
            double v_main = 4 * mainDuct.massFlow / density / Math.PI / Math.Pow(d_main, 2); // m/s
            double f_main = DarcyFriction(d_main, v_main, viscosity, epsilon); // kinetic viscosity of regular air (15)
            // calculate the the pressure loss per length
            double p_delta = f_main * density / 2 * Math.Pow(v_main, 2) / d_main; // Pa/m

            // record the info of main duct/pipe
            mainDuct.diameter = Convert.ToInt32(d_main * 1000); // mm
            mainDuct.velocity = v_main; // m/s
            mainDuct.friction = p_delta * mainDuct.length; // Pa

            // for rest of the duct/pipe
            foreach (ConduitEdge jsEdge in jsNetwork.edges)
            {
                // with presumed f_main of main duct, guess the diameter of sub pipes (so called Equal Friction Method)
                double d_approx = Math.Pow(f_main * density / 2 * Math.Pow(4 * jsEdge.massFlow / Math.PI, 2) / p_delta, 0.2);
                double d_sub = d_DN.OrderBy(d => Math.Abs(d - d_approx)).First();
                double v_sub = jsEdge.massFlow * 4 / Math.PI / Math.Pow(d_sub, 2);
                jsEdge.diameter = Convert.ToInt32(d_sub * 1000); // diameter in mm unit
                jsEdge.velocity = v_sub;
                jsEdge.friction = DarcyFriction(d_sub, v_sub, viscosity, epsilon) * 
                    density / 2 * Math.Pow(v_sub, 2) / d_sub * jsEdge.length; // mm meters head to pascal
            }
            // set valve pressure loss for the compensation of distribution balance
            // it is a binary tree. index the edge by its end node
            Dictionary<string, ConduitEdge> edgeDict = new Dictionary<string, ConduitEdge>() { };
            foreach (ConduitEdge edge in jsNetwork.edges)
            {
                edgeDict.Add(edge.endId, edge);
            }
            // the longest path
            List<string> nodeListFarthest = RetrieveNodeList(jsNetwork.nodes, jsNetwork.maxNode);
            double maxResistance = SumPathWeight(jsNetwork.edges, nodeListFarthest);
            foreach (ConduitNode node in jsNetwork.nodes)
            {
                if (node.degree == 0) // indicates terminals
                {
                    List<string> nodeList = RetrieveNodeList(jsNetwork.nodes, node.id);
                    double resistance = SumPathWeight(jsNetwork.edges, nodeList);
                    foreach (ConduitEdge edge in jsNetwork.edges)
                        if (edge.endId == node.id)
                            edge.friction += maxResistance - resistance; // compensate the resistance at the terminal duct
                }
            }

            jsNetwork.sumMaterial = jsNetwork.edges.Sum(x => x.length * Math.PI * Math.Pow((double)x.diameter / 1000, 2));
            jsNetwork.sumLength = jsNetwork.edges.Sum(x => x.length);

            return;
        }

        public static void Piping(ConduitGraph jsNetwork)
        {
            jsNetwork.type = resTypeEnum.pipe;

            // global settings
            double epsilon = 0.0005; // absolute roughness of close water pipe
            double density = 1000;
            double viscosity = 4.74e-7;
            List<double> d_DN = new List<double>() {0.015, 0.02, 0.025, 0.032, 0.04, 0.05,
                0.065, 0.08, 0.1, 0.125, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4};
            // open function
            Tuple<double, double, double> FrictionFilter(IEnumerable<double> ds, double volumeFlow)
            {
                // pick the diameter with the smallest pressure drop
                List<Tuple<double, double, double>> sizings = new List<Tuple<double, double, double>>();
                foreach (double d in ds)
                {
                    var sizingZip = PressureDropSizing(d, volumeFlow, density, viscosity, epsilon);
                    if (sizingZip.Item3 > 100 && sizingZip.Item3 < 300)
                        sizings.Add(sizingZip);
                }
                if (sizings.Count > 0)
                    return sizings.OrderBy(z => z.Item3).First();
                else
                    return null;
            }

            // calculate the flow rate at each network node
            Dictionary<string, ConduitNode> dict_jsNode = new Dictionary<string, ConduitNode>() { };
            foreach (ConduitNode jsNode in jsNetwork.nodes)
                dict_jsNode.Add(jsNode.id, jsNode);
            // rank edges by their depth
            // and follow this sequence, each edge should copy the end point value to the start point (terminal -> source)
            var edges_ranked = jsNetwork.edges.OrderBy(x => -dict_jsNode[x.endId].depth);
            foreach (ConduitEdge jsEdge in edges_ranked)
            {
                // the flowrate of this edge equals to the end point
                jsEdge.massFlow = dict_jsNode[jsEdge.endId].massFlow;
                dict_jsNode[jsEdge.startId].massFlow += dict_jsNode[jsEdge.endId].massFlow;
            }
            foreach (ConduitEdge jsEdge in jsNetwork.edges)
            {
                List<double> velocities = new List<double>();
                Tuple<double, double, double> sizing = null;
                foreach (double diameter in d_DN)
                {
                    velocities.Add(4 * jsEdge.massFlow / density / Math.PI / Math.Pow(diameter, 2));
                }
                // check if v(32mm) < 1.5, v(65) < 2
                if (velocities[3] < 1.5)
                    sizing = FrictionFilter(d_DN.Take(4), jsEdge.massFlow / density);
                // if previous loop does not find the suitable p_delta
                if (sizing is null && velocities[6] < 2)
                    sizing = FrictionFilter(d_DN.Skip(4).Take(3), jsEdge.massFlow / density);
                if (sizing is null && velocities[15] < 3)
                    sizing = FrictionFilter(d_DN.Skip(7), jsEdge.massFlow / density);
                if (sizing is null)
                    Util.LogPrint("ERROR - No suitable diameter found");
                else
                {
                    // record the info of main duct/pipe
                    jsEdge.diameter = Convert.ToInt32(sizing.Item1 * 1000); // mm only for record
                    jsEdge.velocity = sizing.Item2;
                    jsEdge.friction = sizing.Item3 * jsEdge.length; // Pa
                }
            }
            // 0.0003 presumed thickness
            jsNetwork.sumMaterial = jsNetwork.edges.Sum(x => x.length * Math.PI * Math.Pow((double)x.diameter / 1000, 2) * 0.0003);
            jsNetwork.sumLength = jsNetwork.edges.Sum(x => x.length);

            return;
        }

        protected static double DarcyFriction(double diameter, double velocity, double viscosity, double epsilon)
        {
            // darcy friction is dimensionless, no worry about the parameter unit
            // reference 'McGill airflow's duct system design guide'
            double reynolds = diameter * velocity / viscosity;
            double friction = 0.11 * Math.Pow(epsilon / diameter + 68 / reynolds, 0.25);
            if (friction >= 0.018)
                return friction;
            else
                return friction * 0.85 + 0.0028;
        }

        protected static Tuple<double, double, double> PressureDropSizing(double diameter, double flow, 
            double density, double viscosity, double epsilon)
        {
            double velocity = 4 * flow / Math.PI / Math.Pow(diameter, 2);
            // calculate the the pressure loss per length
            double friction = DarcyFriction(diameter, velocity, viscosity, epsilon);
            double pressureLossPerLength = friction * density / 2 * Math.Pow(velocity, 2) / diameter; // Pa/m
            return new Tuple<double, double, double>(diameter, velocity, pressureLossPerLength);
        }

        protected static List<string> RetrieveNodeList(List<ConduitNode> nodes, string terminalId)
        {
            List<string> nodeIds = new List<string>() { terminalId };
            Dictionary<string, ConduitNode> nodeDict = new Dictionary<string, ConduitNode>() { };
            foreach (ConduitNode node in nodes)
            {
                nodeDict.Add(node.id, node);
            }
            ConduitNode currentNode = nodeDict[terminalId];
            while (currentNode.parent != null)
            {
                nodeIds.Add(currentNode.parent);
                currentNode = nodeDict[currentNode.parent];
            }
            nodeIds.Reverse();
            return nodeIds;
        }

        protected static double SumPathWeight(List<ConduitEdge> edges, List<string> endIds)
        {
            double sumRes = 0;
            foreach (string endId in endIds)
            {
                foreach (ConduitEdge edge in edges)
                {
                    if (edge.endId == endId)
                        sumRes += edge.friction;
                }
            }
            return sumRes;
        }
    }
}
