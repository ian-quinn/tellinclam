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
            pManager.AddNumberParameter("Nominal load", "loads",
                "List of nominal load of each room, representing its basic usage configuration (loads, shcedules, conditioning needs)", GH_ParamAccess.list);
            // by default, only one thermostat serves one thermal zone
            // if multiple thermostats are used, this input has to be a data tree
            pManager.AddIntegerParameter("Room index of thermostat", "sets",
                "The thermostat position of each zone according to the zoning scheme", GH_ParamAccess.list);
            pManager.AddIntegerParameter("System template", "sys",
                "Choose the system configuration template, for example, AHU self-circulation with personalized AC.", GH_ParamAccess.item);
            Param_Integer param = pManager[4] as Param_Integer;
            param.AddNamedValue("Rooftop packaged unit", 0);
            param.AddNamedValue("AHU self-circulation", 1);
            param.AddNamedValue("VAV + personalized AC", 2);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Serialized consifurations", "json", "Serialized information of space layout, functional labeling, system configuration and so on.", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string jsonString = "";
            // the labels are more about the system configuration part
            // thermal part has been addressed in the EnergyPlus model
            List<string> labels = new List<string>() { };
            List<double> loads = new List<double>() { };
            List<int> loc_thermostat = new List<int>() { };
            int system_type = 0;

            if (!DA.GetData(0, ref jsonString))
                return;
            DA.GetDataList(1, labels);
            DA.GetDataList(2, loads);
            if (labels.Count != loads.Count)
                return;
            DA.GetDataList(3, loc_thermostat);
            DA.GetData(4, ref system_type);

            // now everything is unpacked from the JSON file
            Floorplan jsFloorplan = JsonSerializer.Deserialize<Floorplan>(jsonString);
            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    foreach (FunctionSpace jsSpace in jsZone.rooms)
                    {
                        int space_id = Convert.ToInt32(jsSpace.name.Split('_')[1]);
                        jsSpace.name = labels[space_id];
                        jsSpace.load = loads[space_id];
                        // mapping the space label to the schedule/control setting, then assign it to the jsSpace
                        jsSpace.heating_set = 20;
                        jsSpace.heating_vent = 30;
                        jsSpace.cooling_set = 27;
                        jsSpace.cooling_vent = 13;
                    }
                    int zone_id = Convert.ToInt32(jsZone.name.Split('_')[1]);
                    jsZone.thermostat = labels[loc_thermostat[zone_id]];
                }
            }

            var psySI = new Psychrometrics(UnitSystem.SI);

            // try duct sizing here
            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                foreach (ControlZone jsZone in jsSystem.zones)
                {
                    // get the dictionary of space and loads
                    Dictionary<string, double> dict_flowrate = new Dictionary<string, double>() { };
                    foreach (FunctionSpace jsSpace in jsZone.rooms)
                    {
                        var rho = psySI.GetDryAirDensity((jsSpace.heating_vent - jsSpace.heating_set) / 2, 101325);
                        var flow = jsSpace.load / (psySI.GetDryAirEnthalpy(jsSpace.heating_vent) - psySI.GetDryAirEnthalpy(jsSpace.heating_set)) / rho;
                        dict_flowrate.Add(jsSpace.id, Math.Round(flow, 3));
                        jsSpace.flowrate = flow;
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

            Floorplan _jsFloorplan = new Floorplan();
            SystemZone _jsSystem = new SystemZone();
            _jsSystem.zones = new List<ControlZone>() { jsFloorplan.systems[0].zones[0] };
            _jsFloorplan.systems = new List<SystemZone>() { _jsSystem };

            jsonString = JsonSerializer.Serialize(jsFloorplan, new JsonSerializerOptions { WriteIndented = true });

            DA.SetData(0, jsonString);

            Util.ScriptPrint(jsonString, $"network_gen.json", "");
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
                return Properties.Resources.template;
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