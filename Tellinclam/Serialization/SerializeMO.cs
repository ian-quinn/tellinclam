using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

using Rhino.Geometry;
using Tellinclam;
using Tellinclam.MO;
using Rhino.DocObjects;
using System.Diagnostics;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;
using GH_IO.Serialization;
using static Tellinclam.Serialization.SchemaJSON;
using static Tellinclam.Algorithms.PathFinding;
using System.Drawing.Printing;
using System.Security.Policy;
using System.Runtime.Remoting.Messaging;
using System.Text.Json.Nodes;

namespace Tellinclam
{
    class ControlPreset
    {
        public static Tuple<string, string> FanHeating(string id, Port thermostat, Port fan, Port heater, 
             double rec_flow, double minTemp, double gain, double amp, double offset)
        {
            string model = "";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Sources.Pulse THig{id}(amplitude = 0, offset = {offset + 273.15}, period = 86400, \r\n shift = 21600, width = 0.5, y(displayUnit = \"degC\", unit = \"K\"));\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Sources.Pulse TLow{id}(amplitude = {amp - 0}, offset = 0, period = 86400, \r\n shift = 32400, width = 0.375, y(displayUnit = \"degC\", unit = \"K\"));\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Add TSet{id};\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.PID conPID{id}(Ti(displayUnit = \"min\") = 1800, controllerType = Buildings.Controls.OBC.CDL.Types.SimpleController.PI, \r\n    k = 1, u_m(displayUnit = \"degC\", unit = \"K\"), u_s(displayUnit = \"degC\", unit = \"K\"), yMax = 1, yMin = 0);\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Hysteresis staA{id}(uLow = 0.05, uHigh = 0.5);\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Hysteresis staB{id}(uLow = 0.5, uHigh = 0.75);\n";
            // two level of speed control
            model += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanA{id}_flow(realTrue = {rec_flow / 2});\n";
            model += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanB{id}_flow(realTrue = {rec_flow / 2});\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Add m_fan_set{id};\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Add TAirLvgSet{id};\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.MultiplyByParameter gai{id}(final k = {gain});\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.AddParameter TSupMin{id}(p = {minTemp});\n";

            string equation = "";
            // PID controller setting
            equation += $"connect(THig{id}.y, TSet{id}.u1);\n";
            equation += $"connect(TLow{id}.y, TSet{id}.u2);\n";
            equation += $"connect(TSet{id}.y, conPID{id}.u_s);\n";
            equation += $"connect(conPID{id}.u_m, {thermostat.Name});\n";
            equation += $"connect(conPID{id}.y, staA{id}.u);\n";
            equation += $"connect(staA{id}.y, mSetFanA{id}_flow.u);\n";
            equation += $"connect(conPID{id}.y, staB{id}.u);\n";
            equation += $"connect(staB{id}.y, mSetFanB{id}_flow.u);\n";
            equation += $"connect(conPID{id}.y, gai{id}.u);\n";
            // fan controller
            equation += $"connect(mSetFanB{id}_flow.y, m_fan_set{id}.u1);\n";
            equation += $"connect(mSetFanA{id}_flow.y, m_fan_set{id}.u2);\n";
            equation += $"connect(m_fan_set{id}.y, {fan.Name});\n";
            // heater controller
            equation += $"connect(gai{id}.y, TAirLvgSet{id}.u1);\n";
            equation += $"connect(TSupMin{id}.y, TAirLvgSet{id}.u2);\n";
            equation += $"connect(TAirLvgSet{id}.y, {heater.Name});\n";
            equation += $"connect({thermostat.Name}, TSupMin{id}.u);\n";

            return new Tuple<string, string>(model, equation);
        }

        /// <summary>
        /// pending for detail settings
        /// </summary>
        /// <returns></returns>
        public static Tuple<string, string> FanHeaterCooler(string id, Port thermostat, Port fan, Port coil,
             double rec_flow, double ramp0, double ramp1, double ramp2,  Tuple<double, double> span1, Tuple<double, double> span2)
        {
            string model = "";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Sources.Pulse THig{id}(amplitude = {ramp2 - ramp1}, offset = 0, period = 86400, \r\n shift = {3600 * span2.Item1}, width = {(span2.Item2 - span2.Item1) / 24}, y(displayUnit = \"degC\", unit = \"K\"));\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Sources.Pulse TLow{id}(amplitude = {ramp1 - ramp0}, offset = {ramp0 + 273.15}, period = 86400, \r\n shift = {3600 * span1.Item1}, width = {(span1.Item2 - span1.Item1) / 24}, y(displayUnit = \"degC\", unit = \"K\"));\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Add TSet{id};\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.PID conPID{id}(Ti(displayUnit = \"min\") = 1800, controllerType = Buildings.Controls.OBC.CDL.Types.SimpleController.PI, \r\n    k = 1, u_m(displayUnit = \"degC\", unit = \"K\"), u_s(displayUnit = \"degC\", unit = \"K\"), yMax = 1, yMin = 0);\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Hysteresis staA{id}(uLow = 0.05, uHigh = 0.5);\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Hysteresis staB{id}(uLow = 0.5, uHigh = 0.75);\n";
            // two level of speed control
            model += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanA{id}_flow(realTrue = {rec_flow * 0.8 / 2});\n";
            model += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal mSetFanB{id}_flow(realTrue = {rec_flow * 0.8 / 2});\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Add m_fan_set{id};\n";
            // control lock-up to prevent the overheat of coil
            model += $"Buildings.Controls.OBC.CDL.Conversions.BooleanToReal boo{id}(realTrue = 1);\n";
            model += $"Buildings.Controls.OBC.CDL.Continuous.Multiply mul{id}();\n";

            string equation = "";
            // PID controller setting
            equation += $"connect(THig{id}.y, TSet{id}.u1);\n";
            equation += $"connect(TLow{id}.y, TSet{id}.u2);\n";
            equation += $"connect(TSet{id}.y, conPID{id}.u_s);\n";
            equation += $"connect(conPID{id}.u_m, {thermostat.Name});\n";
            equation += $"connect(conPID{id}.y, staA{id}.u);\n";
            equation += $"connect(staA{id}.y, mSetFanA{id}_flow.u);\n";
            equation += $"connect(conPID{id}.y, staB{id}.u);\n";
            equation += $"connect(staB{id}.y, mSetFanB{id}_flow.u);\n";
            
            // fan controller
            equation += $"connect(mSetFanB{id}_flow.y, m_fan_set{id}.u1);\n";
            equation += $"connect(mSetFanA{id}_flow.y, m_fan_set{id}.u2);\n";
            equation += $"connect(m_fan_set{id}.y, {fan.Name});\n";

            // make the heater respond with delays
            equation += $"connect(staA{id}.y, boo{id}.u);\n";
            equation += $"connect(boo{id}.y, mul{id}.u1);\n";
            equation += $"connect(conPID{id}.y, mul{id}.u2);\n";
            equation += $"connect(mul{id}.y, {coil.Name});\n";

            return new Tuple<string, string>(model, equation);
        }
    }
    class SerializeMO
    {
        public static string BlockMassAvg()
        {
            string scripts = "";
            scripts += "block MassAverage\n" +
                "  parameter Integer n;\n" + 
                "  parameter Real w[n];\n" + 
                "  Buildings.Controls.OBC.CDL.Interfaces.RealInput u[n];\n" +
                "  Buildings.Controls.OBC.CDL.Interfaces.RealOutput y;\n" +
                "protected\n" + 
                "  Real mass;\n" + 
                "algorithm\n" +
                "  mass := 0;\n" + 
                "  for i in 1:n loop\n" + 
                "    mass := mass + w[i]*u[i];\n" + 
                "  end for;\n" + 
                "  y := mass / sum(w);\n" +
                "end MassAverage;\n";
            return scripts;
        }

        /// <summary>
        /// Serialize 1 system zone to Modelica model for test purpose.
        /// 20231013 add a toggle to switch the mode how pipe/duct are modeled: whether to use pressure drops or
        /// just consider the friction by tee-joint (at each port)
        /// </summary>
        public static string RecFancoil(Floorplan jsFloorplan, SimulationSettings simSets,
            string modelName, string idf, string epw, bool isIdealFlow)
        {
            string scripts = "";
            string idfPath = idf.Replace(@"\", @"\\");
            string epwPath = epw.Replace(@"\", @"\\");
            string mosPath = Path.ChangeExtension(epwPath, "mos"); ;

            if (epw != "") // for test only
            {
                epwPath = "modelica://Buildings/Resources/weatherdata/USA_IL_Chicago-OHare.Intl.AP.725300_TMY3.epw";
                mosPath = "modelica://Buildings/Resources/weatherdata/USA_IL_Chicago-OHare.Intl.AP.725300_TMY3.mos";
            }

            // prepare dictionaries for Zone, Node, Edge in json
            //Dictionary<string, ControlZone> jsZoneDict = new Dictionary<string, ControlZone>();
            //Dictionary<string, FunctionSpace> jsRoomDict = new Dictionary<string, FunctionSpace>();
            Dictionary<string, ConduitNode> jsNodeDict = new Dictionary<string, ConduitNode>();
            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    //jsZoneDict.Add(jsZone.id, jsZone);
                    //foreach (FunctionSpace jsRoom in jsZone.rooms)
                    //    jsRoomDict.Add(jsRoom.id, jsRoom);
                    if (jsZone.network != null)
                        foreach (ConduitNode node in jsZone.network.nodes)
                            jsNodeDict.Add(node.id, node);
                }
                if (jsSystem.network != null)
                    foreach (ConduitNode node in jsSystem.network.nodes)
                        jsNodeDict.Add(node.id, node);
            }

            // the global module
            // the weather bus in building module has multiple connections directly to each thermal zone model
            // the pAtm has extensible ports that can accommodate all pressure balance port of each thermal zone model
            MO.Building building = new MO.Building(idfPath, epwPath, mosPath);
            Boundary pAtm = new Boundary("pAtm", "Medium");

            // for demonstration, we create a Modelica script for all zones (flatten)
            // generate MO class in a hierarchy way while make the serialization flat

            // zone level attributes
            List<Exchanger> exchangers = new List<Exchanger>() { };
            List<ControlledMassFlow> fans = new List<ControlledMassFlow>() { };
            List<SensorT> sensors = new List<SensorT>() { };
            // additional blocks
            List<MassAverage> avgs = new List<MassAverage>() { };
            // tuple.item1 -> serialized components / tuple.item2 -> serialized connections
            List<Tuple<string, string>> controls = new List<Tuple<string, string>>() { };

            List<List<ThermalZone>> nested_zones = new List<List<ThermalZone>>() { };
            // each control zone has its own sub-network
            List<List<PressureDrop>> nested_zon_pDrops = new List<List<PressureDrop>>() { };
            List<List<Junction>> nested_zon_tJoints = new List<List<Junction>>() { };
            List<List<PressureDrop>> nested_sys_pDrops = new List<List<PressureDrop>>() { };
            List<List<Junction>> nested_sys_tJoints = new List<List<Junction>>() { };

            int z = 0; // zone counter
            int s = 0; // system counter

            Dictionary<string, ThermalZone> zoneDict = new Dictionary<string, ThermalZone>() { };

            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                List<ControlZone> jsZones = jsSystem.zones;
                foreach (ControlZone jsZone in jsZones)
                {
                    List<FunctionSpace> jsSpaces = jsZone.rooms;

                    // room level attributes
                    List<ThermalZone> zones = new List<ThermalZone>() { };
                    List<MassFlowSource> flows = new List<MassFlowSource>() { };

                    // mark the Modelica room that is treated as thermostat loc
                    ThermalZone delegateZone = null;
                    List<double> rec_flow_each = new List<double>() { };
                    
                    for (int j = 0; j < jsSpaces.Count; j++)
                    {
                        string zoneName = $"zon_{z}_{j}";

                        ThermalZone newZone = new ThermalZone(zoneName, jsSpaces[j].name, jsSpaces[j].volume, 0);
                        // internal gain should be a value defined by space function
                        // or you can leave it to EnergyPlus to solve
                        // array q_gain has 3 numbers as inputs

                        // only apply one-direction connetion between constant/bus with component?
                        newZone.qGai_flow.to = newZone.qIntGai.y;
                        newZone.ports.Add(new Port($"ports[{newZone.ports.Count + 1}]", zoneName)); // for mass flow
                        newZone.ports.Add(new Port($"ports[{newZone.ports.Count + 1}]", zoneName)); // for p drop
                        newZone.ports.Add(new Port($"ports[{newZone.ports.Count + 1}]", zoneName)); // for air loop inlet
                        newZone.ports.Add(new Port($"ports[{newZone.ports.Count + 1}]", zoneName)); // for air loop outlet

                        // not so good to represent the room control in this way
                        // better to change the jsZone.thermostat to the id of a specific room
                        if (newZone.zoneName == jsZone.thermostat)
                            delegateZone = newZone;

                        // recirculation air flow rate of each room
                        // this can be the inherit attribute of a space, the flow rate should be designated while system sizing
                        //MassFlowRate recFlow = new MassFlowRate($"mRec_flow_nominal_{i}_{j}", jsSpaces[j].flowrate);
                        //MassFlowRate outFlow = new MassFlowRate($"mOut_flow_nominal_{i}_{j}", 0.3 * jsSpaces[j].volume * 1.2 / 3600);
                        rec_flow_each.Add(jsSpaces[j].flowrate);
                        newZone.recFlow = jsSpaces[j].flowrate;
                        newZone.outFlow = 0.3 * jsSpaces[j].volume * 1.2 / 3600;
                        // only cache the data. it will not be used

                        // paired with a mass flow port
                        string flowName = $"freshAir_{z}_{j}";
                        MassFlowSource newFlow = new MassFlowSource(flowName, "Medium", 0.3 * jsSpaces[j].volume * 1.2 / 3600);
                        newFlow.ports.Add(new Port($"ports[{newFlow.ports.Count + 1}]", flowName));
                        newFlow.weaBus.to = building.weaBus; // is it bi-direction?
                        newZone.newFlow = newFlow;

                        // paired with a pressure drop with outdoor air
                        string ductName = $"outDuc_{z}_{j}";
                        PressureDrop crack = new PressureDrop(ductName, "Medium", false, true, true,
                            20, 0.3 * jsSpaces[j].volume * 1.2 / 3600); /////////////////////////////////////////////////////////////////////-CHECK-///////
                        // paired with airloop
                        // add another port to the global pAtm component
                        pAtm.ports.Add(new Port($"ports[{pAtm.ports.Count + 1}]", pAtm.Name));
                        // this mutual assignment indicates a bad data structure, we'll fix it later
                        Port.Connect(pAtm.ports.Last(), crack.port_b);

                        Port.Connect(newZone.ports[0], newFlow.ports[0]);
                        newZone.ports[1].to = crack.port_a;

                        newZone.crack = crack;

                        // network connection
                        // leave this part to zone level connection

                        flows.Add(newFlow);
                        zones.Add(newZone);
                        zoneDict.Add(jsSpaces[j].id, newZone);
                    }
                    nested_zones.Add(zones);

                    // paired with a heater set
                    // name the heater one by one, start from 0
                    // calculate and input the a cummulative value based on rooms within the zone
                    double mass_flow_nominal = Math.Ceiling(rec_flow_each.Sum() * 20) / 20;
                    int exTypeId = 0;
                    if (jsSystem.type == "RecHeaterElectric") exTypeId = 0;
                    if (jsSystem.type == "RecHeaterCoolerIdeal") exTypeId = 1;
                    Port monitor = new Port("null", "null");
                    // note thte pressure drop, replace it when the equipment list is ready
                    Exchanger exchanger = new Exchanger($"exc_{z}", exTypeId, mass_flow_nominal, 200, jsZone.sizingLoad);
                    ControlledMassFlow fan = new ControlledMassFlow($"fan_{z}", mass_flow_nominal);
                    SensorT sensor = new SensorT($"sen_{z}", mass_flow_nominal);

                    fans.Add(fan);
                    sensors.Add(sensor);
                    exchangers.Add(exchanger);

                    if (jsZone.thermostat == "TempAverage")
                    {
                        // generate an array of room volume
                        double[] volumes = new double[jsSpaces.Count];
                        for (int k = 0; k < jsSpaces.Count; k++)
                        {
                            volumes[k] = jsSpaces[k].volume;
                        }
                        MassAverage avg = new MassAverage($"AvgTemp_{z}", zones.Count, volumes);
                        for (int k = 0; k < zones.Count; k++)
                        {
                            Port.Connect(avg.u[k], zones[k].TAir);
                        }
                        monitor = avg.y;
                        avgs.Add(avg);
                    }
                    else if (jsZone.thermostat == "TempReturnDuct")
                        monitor = sensor.T;
                    else
                        monitor = delegateZone.TAir;

                    // compile the network in this zone
                    // in this process, the relay points are removed
                    // be careful that the flowrate attribute is assigned when creating a bridging edge
                    // this part should be isolated as another function()
                    List<ConduitNode> jsZonNodes = jsZone.network.nodes;
                    List<ConduitEdge> jsZonEdges = jsZone.network.edges;

                    // this is a lenient version function
                    // I will make a more adaptive one and put it in the PathFinding.cs
                    SimplifyTree(jsZonNodes, jsZonEdges);

                    // now the jsEdges represent all pipes/ducts
                    // however, the jsEdge is connected to the terminal node, not the room port
                    // according to the terminalId attribute of each node, connect each jsEdge to room id
                    //foreach (ConduitEdge jsEdge in jsEdges)
                    //{
                    //    if (jsNodeDict[jsEdge.endId].linkedTerminalId != null)
                    //    {
                    //        jsEdge.endId = jsNodeDict[jsEdge.endId].linkedTerminalId;
                    //    }
                    //}

                    // iterate all terminal node to find the target zone's ports
                    Dictionary<string, Port> zonInlets = new Dictionary<string, Port>() { };
                    Dictionary<string, Port> zonOutlets = new Dictionary<string, Port>() { };
                    foreach (ConduitNode jsNode in jsZonNodes)
                    {
                        if (jsNode.linkedTerminalId != null)
                        {
                            zonInlets.Add(jsNode.id, zoneDict[jsNode.linkedTerminalId].ports[2]);
                            zonOutlets.Add(jsNode.id, zoneDict[jsNode.linkedTerminalId].ports[3]);
                        }
                    }

                    SerializeNetwork(jsZonNodes, jsZonEdges, fan.port_a, exchanger.port_b, zonInlets, zonOutlets,
                        $"netZon{z}", isIdealFlow, out List<PressureDrop> zonPDrops, out List<Junction> zonTJoints);

                    nested_zon_pDrops.Add(zonPDrops);
                    nested_zon_tJoints.Add(zonTJoints);

                    // retrieve control logic
                    // which monitor point to use? thermostat.TAir / sensor.T

                    Tuple<string, string> ctrlSchema = new Tuple<string, string>($"_{z}", "--ERROR-- No control template assigned\n");
                    if (jsSystem.type == "RecHeaterElectric")
                    {
                        ctrlSchema = ControlPreset.FanHeating($"_zon{z}", monitor, fan.m_flow_in, exchanger.set,
                            exchanger.flow_nominal, 2, 8, jsZone.heating_set, 0);
                        // two level temp control from example file, 2, 8, 6, 16, fluctuates between 10~22
                    }
                    else if (jsSystem.type == "RecHeaterCoolerIdeal")
                    {
                        ctrlSchema = ControlPreset.FanHeaterCooler($"_zon{z}", monitor, fan.m_flow_in, exchanger.set,
                            exchanger.flow_nominal, 0, 14, jsZone.heating_set, new Tuple<double, double> (6, 18), new Tuple<double, double>(9, 18));
                    }
                    controls.Add(ctrlSchema);

                    // move to next step
                    z++;
                }

                // continue with the system network part, network-A
                /*
                List<ConduitNode> jsSysNodes = jsSystem.network.nodes;
                List<ConduitEdge> jsSysEdges = jsSystem.network.edges;
                SimplifyTree(jsSysNodes, jsSysEdges);

                Dictionary<string, Port> ahuInlets = new Dictionary<string, Port>() { };
                Dictionary<string, Port> ahuipOutlets = new Dictionary<string, Port>() { };
                foreach (ConduitNode jsNode in jsSysNodes)
                {
                    if (jsNode.linkedTerminalId != null)
                    {
                        // add the equipment inlet and outlet to the dictionary
                    }
                }

                // pending to update
                SerializeNetwork(jsSysNodes, jsSysEdges, pump.port, chiller/boiler.port, ahuInlets, ahuipOutlets,
                        $"netSys{s}", isIdealFlow, out List<PressureDrop> sysPDrops, out List<Junction> sysTJoints);

                nested_sys_pDrops.Add(sysPDrops);
                nested_sys_tJoints.Add(sysTJoints);

                */

                // pump serialization part


                s++;
            }


            // ----------------------------------------------------------- //

            // serialize header
            scripts += "within ;\n\n";
            // model declearation
            scripts += $"model {modelName}\n";
            scripts += $"extends Modelica.Icons.Example;\n";

            // load custom blocks
            scripts += BlockMassAvg();

            scripts += $"package Medium = Buildings.Media.Air;\n";

            // serialize Building
            scripts += building.Serialize();
            scripts += pAtm.Serialize();

            // serialize ThermalZone (e.g. the functional space unit)
            for (int i = 0; i < nested_zones.Count; i++)
            {
                foreach (ThermalZone zone in nested_zones[i])
                {
                    scripts += zone.Serialize();
                    //scripts += $"constant Modelica.Units.SI.Area AFlo{i + 1} = {zones[i].area};\n";
                    //scripts += zone.recFlow.Serialize();
                    //scripts += zone.outFlow.Serialize();
                    // each zone is paired with a mass flow rate port, which names after the zone
                    // for example: zone1 -> freshAir1
                    scripts += zone.newFlow.Serialize(); // flow source
                    scripts += zone.qIntGai.Serialize();
                    scripts += zone.crack.Serialize();
                }

                scripts += exchangers[i].Serialize();
                scripts += fans[i].Serialize();
                scripts += sensors[i].Serialize();
                if (i < avgs.Count)
                    scripts += avgs[i].Serialize();
                scripts += controls[i].Item1;

                foreach (Junction tJoint in nested_zon_tJoints[i])
                {
                    scripts += tJoint.Serialize();
                }

                foreach (PressureDrop pDrop in nested_zon_pDrops[i])
                {
                    scripts += pDrop.Serialize();
                }
            }
            // set fan and heater
            //scripts += heater.Serialize();
            //scripts += fan.Serialize();
            // set control
            //scripts += fanHeating.Item1;

            // set fluid network


            // initial equation
            scripts += "initial equation\n";

            // -----------------------------------------------------------------------------------
            // equation part
            scripts += "equation\n";
            // you may use a list recording all ports connected
            // thermal zone session
            List<string> portNames = new List<string>() { };
            for (int i = 0; i < nested_zones.Count; i++)
            {
                foreach (ThermalZone zone in nested_zones[i])
                {
                    scripts += $"connect({zone.qGai_flow.Name}, {zone.qGai_flow.to.Name});\n";
                    foreach (Port prt in zone.ports)
                    {
                        scripts += $"connect({prt.Name}, {prt.to.Name});\n";
                    }
                    scripts += $"connect({zone.newFlow.weaBus.Name}, {zone.newFlow.weaBus.to.Name});\n";
                    scripts += $"connect({zone.crack.port_b.Name}, {zone.crack.port_b.to.Name});\n";
                }

                // set fluid network connection

                foreach (PressureDrop pDrop in nested_zon_pDrops[i])
                {
                    scripts += $"connect({pDrop.port_a.Name}, {pDrop.port_a.to.Name});\n";
                    scripts += $"connect({pDrop.port_b.Name}, {pDrop.port_b.to.Name});\n";
                }

                // if not using pDrop for connection, iterate port_2 and port_3 of T-joint
                foreach (Junction tJoint in nested_zon_tJoints[i])
                {
                    scripts += $"connect({tJoint.port_2.Name}, {tJoint.port_2.to.Name});\n";
                    scripts += $"connect({tJoint.port_3.Name}, {tJoint.port_3.to.Name});\n";
                }
                // then add the source
                scripts += $"connect({fans[i].port_a.Name}, {fans[i].port_a.to.Name});\n";
                scripts += $"connect({exchangers[i].port_b.Name}, {exchangers[i].port_b.to.Name});\n";

                // use average temperature to control?
                if (i < avgs.Count)
                {
                    foreach (Port input in avgs[i].u)
                    {
                        scripts += $"connect({input.Name}, {input.to.Name});\n";
                    }
                }
                    

                // source side connection
                // it is not right to put the sensor here, but, for convenience...
                scripts += $"connect({fans[i].port_b.Name}, {sensors[i].port_a.Name});\n";
                scripts += $"connect({sensors[i].port_b.Name}, {exchangers[i].port_a.Name});\n";
                scripts += controls[i].Item2;
            }
            
            // set fan and heater
            //scripts += $"connect({fan.port_b.Name}, {heater.port_a.Name});\n";
            // set control logic
            //scripts += fanHeating.Item2;

            // simulation configurations
            // wire this with another component for general settings
            Documentation newSim = new Documentation();
            newSim.Info = simSets.info;
            newSim.StartTime = simSets.startTime;
            newSim.StopTime = simSets.stopTime;
            newSim.Interval = simSets.interval;
            newSim.Tolerance = simSets.tolerance;
            newSim.Algorithm = simSets.algorithm;
            scripts += newSim.Serialize();

            // enclosed
            scripts += $"end {modelName};";

            return scripts;
        }


        // UTILITY ----------------------------------------------------------------------------------------
        // ------------------------------------------------------------------------------------------------
        // ------------------------------------------------------------------------------------------------
        public static void SimplifyTree(List<ConduitNode> jsNodes, List<ConduitEdge> jsEdges)
        {
            Dictionary<string, ConduitNode> jsNodeDict = new Dictionary<string, ConduitNode>() { };
            foreach (ConduitNode jsNode in jsNodes)
            {
                jsNodeDict.Add(jsNode.id, jsNode);
            }

            bool flag = true;
            int inf_counter = 0;
            while (flag || inf_counter > 30)
            {
                inf_counter += 1;
                int node_counter = 0;
                // do not remove node with degree = 2
                // just add new edges that bridge them over
                foreach (ConduitNode jsNode in jsNodes)
                {
                    if (jsNode.degree == 1) // in a tree, the source node and relay node all have degree 1
                    {
                        if (jsNode.type == nodeTypeEnum.source)
                            continue;
                        node_counter += 1;
                        string start_id = "";
                        string end_id = "";
                        double weight = 0;
                        double friction = 0;
                        for (int j = jsEdges.Count - 1; j >= 0; j--)
                        {
                            if (jsEdges[j].startId == jsNode.id)
                            {
                                end_id = jsEdges[j].endId;
                                weight += jsEdges[j].length;
                                friction += jsEdges[j].friction;
                                jsEdges.RemoveAt(j);
                                jsNode.degree -= 1;
                            }
                            else if (jsEdges[j].endId == jsNode.id)
                            {
                                start_id = jsEdges[j].startId;
                                weight += jsEdges[j].length;
                                friction += jsEdges[j].friction;
                                jsEdges.RemoveAt(j);
                                jsNode.degree -= 1;
                            }
                        }
                        weight += 0.023;
                        if (start_id != "" && end_id != "")
                            jsEdges.Add(new ConduitEdge
                            {
                                startId = start_id,
                                endId = end_id,
                                length = weight,
                                flowrate = jsNodeDict[end_id].flowrate,
                                friction = friction
                            });
                        break;
                    }
                }
                if (node_counter == 0)
                    flag = false;
            }
        }

        /// <summary>
        /// //
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="nodes"></param>
        /// <param name="edges"></param>
        /// <param name="inlet"></param>
        /// <param name="outlet"></param>
        /// <param name="terminalOutletDict"></param>
        /// <param name="terminalInletDict"></param>
        /// <param name="prefix"></param>
        /// <param name="isIdealFlow"></param>
        /// <param name="pDrops"></param>
        /// <param name="tJoints"></param>
        public static void SerializeNetwork(List<ConduitNode> nodes, List<ConduitEdge> edges, 
            Port inlet, Port outlet, Dictionary<string, Port> terminalOutletDict, Dictionary<string, Port> terminalInletDict, 
            string prefix, bool isIdealFlow, 
            out List<PressureDrop> pDrops, out List<Junction> tJoints)
        {
            Dictionary<string, ConduitNode> jsNodeDict = new Dictionary<string, ConduitNode>();
            foreach (ConduitNode node in nodes)
            {
                jsNodeDict.Add(node.id, node);
            }

            // do we have to recreate another graph, or we can just create T-joint and pressure drops directly?
            Dictionary<string, Junction> jointDict = new Dictionary<string, Junction>() { };
            Dictionary<string, Junction> jointDict_rev = new Dictionary<string, Junction>() { };
            pDrops = new List<PressureDrop>() { };
            tJoints = new List<Junction>() { };
            foreach (ConduitNode jsNode in nodes)
            {
                if (jsNode.degree == 2)
                {
                    // predefined pressure drop, nominal flow and res array, can be fixed later
                    // note that "Medium" is a default setting. You need to replace it when water/air are introduced
                    jointDict.Add(jsNode.id, new Junction(
                        $"{prefix}_TI{jointDict.Count}", "Medium", true, true,
                        10, 0, new double[] { 0, 1, 1 }, new double[] { 0, 0, 0 }));
                    tJoints.Add(jointDict[jsNode.id]);
                    jointDict_rev.Add(jsNode.id, new Junction(
                        $"{prefix}_TO{jointDict_rev.Count}", "Medium", true, true,
                        10, 0, new double[] { 0, 1, 1 }, new double[] { 0, 0, 0 }));
                    tJoints.Add(jointDict_rev[jsNode.id]);
                }
            }
            // it is a tree, you cannot guarantee that each edge follows the flow direction
            // ventilating direction path 
            foreach (ConduitEdge jsEdge in edges)
            {
                PressureDrop pDrop = new PressureDrop(
                    $"{prefix}_PI{edges.IndexOf(jsEdge)}", "Medium", false, true, true, jsEdge.friction, jsEdge.flowrate);

                // what is the start point? to source root or to any of the out port of T-joint
                if (jsNodeDict[jsEdge.startId].type == nodeTypeEnum.source)
                {
                    Port.Connect(pDrop.port_a, inlet);
                    // leave the res blank for the sum flowrate
                }
                else
                {
                    Junction inputJunction = jointDict[jsEdge.startId]; // get the inward flow port
                    if (inputJunction.outPort == 0)
                    {
                        // connect to the 1st out port
                        Port.Connect(pDrop.port_a, inputJunction.port_2);
                        // round to the top by granularity 0.05
                        // negative value means the fuild is leaving the component
                        inputJunction.flows[1] = -jsEdge.flowrate;
                        inputJunction.outPort += 1;
                    }
                    else if (inputJunction.outPort == 1)
                    {
                        // connect to the 2nd out port
                        Port.Connect(pDrop.port_a, inputJunction.port_3);
                        // round to the top by granularity 0.05
                        // negative value means the fuild is leaving the component
                        inputJunction.flows[2] = -jsEdge.flowrate;
                        inputJunction.outPort += 1;
                    }
                    else
                        Debug.Print("Port not enough for a T-joint");
                }

                // what is the end point? to room or to T-joint
                if (jsNodeDict[jsEdge.endId].type == nodeTypeEnum.terminal)
                {
                    //Port.Connect(pDrop.port_b, terminals[jsNodeDict[jsEdge.endId].linkedTerminalId].ports[2]);
                    Port.Connect(pDrop.port_b, terminalOutletDict[jsNodeDict[jsEdge.endId].id]);
                }
                else
                {
                    // there is no stream merge in this ducting work
                    Port.Connect(pDrop.port_b, jointDict[jsEdge.endId].port_1);
                    // leave the res blank for the sum flowrate
                }
                pDrops.Add(pDrop);
            }
            // return loop?
            foreach (ConduitEdge jsEdge in edges)
            {
                PressureDrop pDrop = new PressureDrop(
                    $"{prefix}_PO{edges.IndexOf(jsEdge)}", "Medium", false, true, true, jsEdge.friction, jsEdge.flowrate);
                // note that the start and end points of the edge remains the same
                // you need to reverse them when connecting the pipe/duct
                // either TO from the source or TO the T-joint (merging flow)
                if (jsNodeDict[jsEdge.startId].type == nodeTypeEnum.source)
                {
                    Port.Connect(pDrop.port_b, outlet);
                }
                else
                {
                    Junction outputJunction = jointDict_rev[jsEdge.startId]; // get the inward flow port
                    if (outputJunction.outPort == 0)
                    {
                        // connect to the 1st out port
                        Port.Connect(pDrop.port_b, outputJunction.port_2);
                        // round to the top by granularity 0.05
                        outputJunction.flows[1] = jsEdge.flowrate;
                        outputJunction.outPort += 1;
                    }
                    else if (outputJunction.outPort == 1)
                    {
                        // connect to the 2nd out port
                        Port.Connect(pDrop.port_b, outputJunction.port_3);
                        // round to the top by granularity 0.05
                        outputJunction.flows[2] = jsEdge.flowrate;
                        outputJunction.outPort += 1;
                    }
                    else
                        Debug.Print("Port not enough for a T-joint");
                }
                // either starting from the terminal or the merging port of a junction
                if (jsNodeDict[jsEdge.endId].type == nodeTypeEnum.terminal)
                {
                    //Port.Connect(zoneDict[jsNodeDict[jsEdge.endId].linkedTerminalId].ports[3], pDrop.port_a);
                    Port.Connect(terminalInletDict[jsNodeDict[jsEdge.endId].id], pDrop.port_a);
                }
                else
                {
                    Port.Connect(pDrop.port_a, jointDict_rev[jsEdge.endId].port_1);
                }
                pDrops.Add(pDrop);
            }

            // for all T-joints, fulfil their res value
            // take the port_1, e.g. the res[0] as the main port, [1], [2] are the branches
            foreach (Junction tJoint in tJoints)
            {
                tJoint.flows[0] = -(tJoint.flows[1] + tJoint.flows[2]);
                //tJoint.m_flow_nominal = Math.Abs(tJoint.res[1] + tJoint.res[2]);
                tJoint.m_flow_nominal = 1;
            }

            // 20231013 ------------------------------------------------------------------------------------
            // what if we remove all presure drops, only keep T-joints?
            // this is a temporary test because the pressure drops lead to fatal error in Modelica somehow
            Dictionary<string, Junction> jointNameDict = new Dictionary<string, Junction>() { };
            foreach (Junction tj in tJoints)
            {
                jointNameDict.Add(tj.Name, tj);
            }
            if (!isIdealFlow)
            {
                foreach (PressureDrop pDrop in pDrops)
                {
                    Port port_1 = pDrop.port_a.to;
                    Port port_2 = pDrop.port_b.to;
                    Port.Connect(port_1, port_2);

                    if (tJoints.Count > 0)
                    {
                        if (port_1.parent.Contains("TI"))
                        {
                            var tags = port_1.Name.Split('_');
                            if (tags.Last() == "2")
                                jointNameDict[port_1.parent].res[1] = -pDrop.dp_nominal;
                            if (tags.Last() == "3")
                                jointNameDict[port_1.parent].res[2] = -pDrop.dp_nominal;
                        }
                        else if (port_2.parent.Contains("TI"))
                            jointNameDict[port_2.parent].res[0] = pDrop.dp_nominal;
                        if (port_2.parent.Contains("TO"))
                        {
                            var tags = port_2.Name.Split('_');
                            if (tags.Last() == "2")
                                jointNameDict[port_2.parent].res[1] = pDrop.dp_nominal;
                            if (tags.Last() == "3")
                                jointNameDict[port_2.parent].res[2] = pDrop.dp_nominal;
                        }
                        else if (port_1.parent.Contains("TO"))
                            jointNameDict[port_1.parent].res[0] = -pDrop.dp_nominal;
                    }
                }
                pDrops = new List<PressureDrop>() { }; // empty the pDrop list
            }
                
            
            return;
        }
    }
}
