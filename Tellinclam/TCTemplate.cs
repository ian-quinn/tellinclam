using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel.Parameters;
using System.Text.Json;
using static Tellinclam.Serialization.SchemaJSON;
using PsychroLib;
using System.Text.Json.Nodes;
using Rhino.DocObjects;
using Grasshopper.Kernel.Data;
using System.IO;
using System.Collections;
using System.Drawing.Text;
using System.Security.Cryptography.X509Certificates;

namespace Tellinclam
{
    public class TCTemplate : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCTemplate()
          : base("System Mockup", "SysMockup",
            "System configuration and zone template mockup",
            "Clam", "IO")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Serialized configurations", "json",
                "Serialized information of space layout, functional labeling, system configuration and so on.", GH_ParamAccess.item);
            // put a nested json string here in the future
            pManager.AddTextParameter("Name correction?", "names",
                "List of function label of each room, representing its basic usage configuration (loads, shcedules, conditioning needs)", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Zone settings (thermostat, heating/cooling setpoint)", "thermostat",
                "Index for space to place the thermostat / -1 -> by numerical average of all room remperatures / -2 -> by temperature of the return duct", GH_ParamAccess.list);

            pManager.AddNumberParameter("Nominal load", "loads",
                "List of nominal load of each room, representing its basic usage configuration (loads, shcedules, conditioning needs)", GH_ParamAccess.tree);
            // by default, only one thermostat serves one thermal zone
            // if multiple thermostats are used, this input has to be a data tree
            
            pManager.AddNumberParameter("Zone heating setpoint", "heat_temp", "Heating setpoint for the zone control", GH_ParamAccess.list);
            pManager.AddNumberParameter("Zone cooling setpoint", "cool_temp", "Cooling setpoint for the zone control", GH_ParamAccess.list);
            pManager.AddIntegerParameter("System template", "sys_type",
                "Choose the system configuration template, for example, AHU self-circulation with personalized AC.", GH_ParamAccess.item);
            Param_Integer param = pManager[6] as Param_Integer;
            param.AddNamedValue("Recirculation Electric Heat", 0);
            param.AddNamedValue("Rooftop AC Unit", 1);
            param.AddNamedValue("Recirculation Fan-coil Water", 2);
            param.AddNamedValue("VAV Electric Reheat", 3);
            pManager.AddPathParameter("Sub-system path indicator", "subsys_path",
                "The path indicator which sub-system is serialized. {1; 2} means the system 1 and control zone 2.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Serialized configurations", "json", "Serialized information of space layout, functional labeling, system configuration and so on.", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string jsonSys = "";
            // the labels are more about the system configuration part
            // thermal part has been addressed in the EnergyPlus model
            List<string> labels = new List<string>() { };
            //List<double> loads = new List<double>() { };
            GH_Structure<GH_Number> loads = new GH_Structure<GH_Number>() { };
            List<int> loc_thermostat = new List<int>() { };
            List<double> temp_heating = new List<double>() { };
            List<double> temp_cooling = new List<double>() { };
            int system_type = 0;
            GH_Path subsystem_id = new GH_Path(0);

            if (!DA.GetData(0, ref jsonSys))
                return;
            DA.GetDataList(1, labels);
            DA.GetDataTree(3, out loads);
            List<double> max_loads = new List<double>() { };
            List<double> avg_loads = new List<double>() { };
            foreach (var branch in loads.Branches)
            {
                List<double> load_series = branch.Select(s => s.Value).ToList();
                var sub_series = load_series.GetRange(0, 1440);
                max_loads.Add(sub_series.Count > 0 ? sub_series.Max() : 0.0); // for sizing
                List<double> load_eachday_avg = new List<double>() { };
                for (int i = 0; i < 60; i++)
                {
                    List<double> day_series = sub_series.GetRange(24 * i + 9, 9);
                    day_series.Sort();
                    load_eachday_avg.Add(day_series.GetRange(5, 4).Average());
                }
                avg_loads.Add(load_eachday_avg.Average() * 2.0);
            }
            if (labels.Count != max_loads.Count)
                return;
            DA.GetDataList(2, loc_thermostat);
            DA.GetDataList(4, temp_heating);
            DA.GetDataList(5, temp_cooling);
            DA.GetData(6, ref system_type);
            DA.GetData(7, ref subsystem_id);

            // if the cooling and heating setpoint are all the same
            if (temp_heating.Count == 1)
                for (int i = 1; i < loc_thermostat.Count; i++)
                    temp_heating.Add(temp_heating[0]);
            if (temp_cooling.Count == 1)
                for (int i = 1; i < loc_thermostat.Count; i++)
                    temp_cooling.Add(temp_cooling[0]);

            // now everything is unpacked from the JSON file
            Floorplan jsFloorplan = JsonSerializer.Deserialize<Floorplan>(jsonSys);
            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                if (system_type == 0)
                    jsSystem.type = "RecHeaterElectric";
                else if (system_type == 1)
                    jsSystem.type = "RecHeaterCoolerIdeal";
                else if (system_type == 2)
                    jsSystem.type = "RecHeaterCoolerWater";
                else if (system_type == 3)
                    jsSystem.type = "VAVReheatElectric";
                
                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    double load_sum = 0;
                    foreach (FunctionSpace jsSpace in jsZone.rooms)
                    {
                        int space_id = Convert.ToInt32(jsSpace.name.Split('_')[1]);
                        jsSpace.name = labels[space_id];
                        jsSpace.maxLoad = max_loads[space_id];
                        jsSpace.avgLoad = avg_loads[space_id];
                        load_sum += jsSpace.maxLoad;
                        // mapping the space label to the schedule/control setting, then assign it to the jsSpace
                    }
                    int zone_id = Convert.ToInt32(jsZone.name.Split('_')[1]);
                    if (loc_thermostat[zone_id] >= 0)
                        jsZone.thermostat = labels[loc_thermostat[zone_id]];
                    else if (loc_thermostat[zone_id] == -1)
                        jsZone.thermostat = "TempAverage";
                    else if (loc_thermostat[zone_id] == -2)
                        jsZone.thermostat = "TempReturnDuct";
                    jsZone.sizingLoad = load_sum;
                    jsZone.heating_set = temp_heating[zone_id];
                    jsZone.heating_vent = 35; // defined by the system type, pending for revision
                    jsZone.cooling_set = temp_cooling[zone_id];
                    jsZone.cooling_vent = 13; // defined by the system type, pending for revision
                }
            }

            var psySI = new Psychrometrics(UnitSystem.SI);

            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    double setPointHeating = jsZone.heating_set;
                    double venPointHeating = jsZone.heating_vent;
                    double setPointCooling = jsZone.cooling_set;
                    double venPointCooling = jsZone.cooling_set;
                    // get the dictionary of space and loads
                    Dictionary<string, double> dict_flowrate = new Dictionary<string, double>() { };
                    foreach (FunctionSpace jsSpace in jsZone.rooms)
                    {
                        var rho = psySI.GetDryAirDensity(venPointHeating, 101325);
                        // try 1.2 as the safty ratio, consider this as an user input
                        var flow = jsSpace.maxLoad / (psySI.GetDryAirEnthalpy(venPointHeating) - psySI.GetDryAirEnthalpy(setPointHeating));
                        // var speed -> ?
                        dict_flowrate.Add(jsSpace.id, Math.Round(flow, 3));
                        jsSpace.flowrate = flow;
                        // jsSpace.speed -> ?
                    }
                    // calculate the flow rate at each network node
                    Dictionary<string, ConduitNode> dict_jsNode = new Dictionary<string, ConduitNode>() { };
                    foreach (ConduitNode jsNode in jsZone.network.nodes)
                    {
                        dict_jsNode.Add(jsNode.id, jsNode);
                        if (jsNode.linkedTerminalId != null)
                        {
                            jsNode.flowrate = dict_flowrate[jsNode.linkedTerminalId];
                        }
                    }
                    // rank edges by their depth
                    // and follow this sequence, each edge should copy the end point value to the start point (terminal -> source)
                    var edges_ranked = jsZone.network.edges.OrderBy(x => -dict_jsNode[x.endId].depth);
                    foreach (ConduitEdge jsEdge in edges_ranked)
                    {
                        // the flowrate of this edge equals to the end point
                        jsEdge.flowrate = dict_jsNode[jsEdge.endId].flowrate;
                        dict_jsNode[jsEdge.startId].flowrate += dict_jsNode[jsEdge.endId].flowrate;
                    }
                    Debug.Print("");
                }
            }

            // Update the pressure loss of each duct/pipe by Equal Friction Method
            double epsilon = 0.0001524; // absolute roughness of Galvanized Steel air duct
            List<double> d_std = new List<double>() {0.1, 0.125, 0.15, 0.175, 0.2, 0.225, 0.25, 0.3, 0.35, 0.4, 0.45, 
                0.5, 0.55, 0.6, 0.65, 0.7, 0.75, 0.8, 0.85, 0.9, 0.95, 1.0, 1.1, 1.2, 1.3, 
                1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 2.0, 2.2, 2.4, 2.6, 2.8, 3.0, 3.5};

            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                double ductCost = 0;
                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    double max_flowrate = 0;
                    int max_index = -1;
                    foreach (ConduitEdge jsEdge in jsZone.network.edges)
                    {
                        if (jsEdge.flowrate > max_flowrate)
                        {
                            max_flowrate = jsEdge.flowrate;
                            max_index = jsZone.network.edges.IndexOf(jsEdge);
                        }
                    }
                    // presume the maximum velocity acceptable is 5 m/s
                    double d_guess = Math.Pow(max_flowrate / Math.PI / 5, 0.5) * 2;
                    // pick the ceiling diameter accroding to the standard diameter list
                    double d_main = 0.1;
                    foreach (double d in d_std)
                    {
                        if (d > d_guess)
                        {
                            d_main = d;
                            break;
                        }
                    }
                    double v_main = max_flowrate * 4 / Math.PI / Math.Pow(d_main, 2); // m/s
                    double f_main = DarcyFriction(d_main, v_main, epsilon);
                    // calculate the the pressure loss per length
                    double p_delta = f_main * 1.2 / 2 * Math.Pow(v_main, 2) / d_main; // Pa/m

                    // record the info of main duct/pipe
                    jsZone.network.edges[max_index].diameter = Convert.ToInt32(d_main * 1000); // mm
                    jsZone.network.edges[max_index].velocity = v_main; // m/s
                    jsZone.network.edges[max_index].friction = p_delta * jsZone.network.edges[max_index].length; // Pa

                    // for rest of the duct/pipe
                    foreach (ConduitEdge jsEdge in jsZone.network.edges)
                    {
                        if (jsZone.network.edges.IndexOf(jsEdge) == max_index)
                            continue;
                        // use the parameter of main duct to guess the possible diameter sizing of sub pipes (so called Equal Friction)
                        double d_approx = Math.Pow(f_main * 16 * 1.2 / 2 * Math.Pow(jsEdge.flowrate, 2) / Math.PI / Math.PI / p_delta, 0.2);
                        double min_bias = 10000;
                        int min_index = -1;
                        foreach (double d in d_std)
                        {
                            double bias = Math.Abs(d - d_approx);
                            if (bias < min_bias)
                            {
                                min_bias = bias;
                                min_index = d_std.IndexOf(d);
                            }
                        }
                        double d_sub = d_std[min_index];
                        double v_sub = jsEdge.flowrate * 4 / Math.PI / Math.Pow(d_std[min_index], 2);
                        jsEdge.diameter = Convert.ToInt32(d_sub * 1000);
                        jsEdge.velocity = v_sub;
                        jsEdge.friction = DarcyFriction(d_sub, v_sub, epsilon) * 1.2 / 2 * Math.Pow(v_sub, 2) / d_sub * jsEdge.length; // mm meters head to pascal
                    }

                    // set valve pressure loss for the compensation of distribution balance
                    // it is a binary tree. index the edge by its end node
                    Dictionary<string, ConduitEdge> edgeDict = new Dictionary<string, ConduitEdge>() { };
                    foreach (ConduitEdge edge in jsZone.network.edges)
                    {
                        edgeDict.Add(edge.endId, edge);
                    }
                    // the longest path
                    List<string> nodeListFarthest = RetrieveNodeList(jsZone.network.nodes, jsZone.network.maxNode);
                    double maxResistance = SumPathWeight(jsZone.network.edges, nodeListFarthest);
                    foreach (ConduitNode node in jsZone.network.nodes)
                    {
                        if (node.degree == 0) // indicates terminals
                        {
                            List<string> nodeList = RetrieveNodeList(jsZone.network.nodes, node.id);
                            double resistance = SumPathWeight(jsZone.network.edges, nodeList);
                            foreach (ConduitEdge edge in jsZone.network.edges)
                                if (edge.endId == node.id)
                                    edge.friction += maxResistance - resistance; // compensate the resistance at the terminal duct
                        }
                    }

                    // summarize the material for duct (area)
                    double sumMaterial = 0;
                    double sumLength = 0;
                    foreach (ConduitEdge edge in jsZone.network.edges)
                    {
                        sumMaterial += edge.length * Math.PI * Math.Pow((double)edge.diameter / 1000, 2);
                        sumLength += edge.length;
                    }
                    jsZone.network.sumMaterial = sumMaterial;
                    jsZone.network.sumLength = sumLength;
                    ductCost += sumMaterial;
                }
                jsSystem.ductCost = ductCost;
            }

            // a single zone network implementation, only for test
            if (subsystem_id.Length == 1)
            {
                jsonSys = JsonSerializer.Serialize(jsFloorplan, new JsonSerializerOptions { WriteIndented = true });
            }
            else if (subsystem_id.Length == 2)
            {
                Floorplan _jsFloorplan = new Floorplan();
                SystemZone _jsSystem = new SystemZone();
                _jsSystem.zones = new List<ControlZone>() { jsFloorplan.systems[subsystem_id.Indices[0]].zones[subsystem_id.Indices[1]] };
                _jsSystem.id = jsFloorplan.systems[subsystem_id.Indices[0]].id;
                _jsSystem.name = jsFloorplan.systems[subsystem_id.Indices[0]].name;
                _jsSystem.type = jsFloorplan.systems[subsystem_id.Indices[0]].type;
                _jsFloorplan.systems = new List<SystemZone>() { _jsSystem };
                jsonSys = JsonSerializer.Serialize(_jsFloorplan, new JsonSerializerOptions { WriteIndented = true });
            }

            DA.SetData(0, jsonSys);

            Util.ScriptPrint(jsonSys, $"network_gen.json", "");
        }

        protected double DarcyFriction(double diameter, double velocity, double epsilon)
        {
            double reynolds = diameter * velocity * 66340;
            double friction = 0.11 * Math.Pow(12 * epsilon / diameter + 68 / reynolds, 0.25);
            if (friction >= 0.018)
                return friction;
            else
                return friction * 0.85 + 0.0028;
        }

        protected List<string> RetrieveNodeList(List<ConduitNode> nodes, string terminalId)
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

        protected double SumPathWeight(List<ConduitEdge> edges, List<string> endIds)
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

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.mockup;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("E4C19942-AEBB-49B6-8622-E8AE38A9FD74");
    }
}