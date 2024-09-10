using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;

using Tellinclam.JSON;
using Tellinclam.Algorithms;

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
            pManager.AddTextParameter("Name correction?", "name",
                "Space tag in line with the thermal zone modeled in IDF", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Zone settings (thermostat, heating/cooling setpoint)", "thermostat",
                ">=0 : Index for space to place the thermostat \n" +
                "-1  : by temperature of the return duct \n" +
                "-2  : by numerical average of all room remperatures ", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Nominal load (W)", "load", 
                "Nominal load of each space, must in the same sequence of names. In Watt", GH_ParamAccess.list);
            // by default, only one thermostat serves one thermal zone
            // if multiple thermostats are used, this input has to be a data tree
            pManager.AddNumberParameter("On/off time sequence", "schedule", "On/off sequence for system [0, 24]", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Zone heating setpoint", "setpoint", "Heating setpoint for the zone control", GH_ParamAccess.list);
            pManager.AddIntegerParameter("System template", "sys_type",
                "Choose the system configuration template, for example, AHU self-circulation with personalized AC.", GH_ParamAccess.item);
            // only support one system type per floor (auto type pairing in future update)
            Param_Integer param = pManager[6] as Param_Integer;
            param.AddNamedValue("Ideal load system", 0);
            param.AddNamedValue("Fan-coil water loop", 1);
            param.AddNamedValue("VAV reheat", 2);
            //param.AddNamedValue("Radiator", 3);
            //param.AddNamedValue("VRF", 4);
            pManager.AddPathParameter("Sub-system path indicator", "subsys_path",
                "The path indicator which sub-system is serialized. {1; 2} means the system 1 and control zone 2.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Serialized configurations", "json", 
                "Serialized information of space layout, functional labeling, system configuration and so on.", GH_ParamAccess.list);
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
            List<string> spaceNames = new List<string>();
            List<int> sensorLocs = new List<int>();
            List<GH_Interval> spaceLoads = new List<GH_Interval>();
            List<double> schedule = new List<double>();
            List<GH_Interval> setPoints = new List<GH_Interval>();
            int system_type = 0;
            GH_Path subsys_path = new GH_Path(0);
            if (!DA.GetData(0, ref jsonSys))
                return;
            DA.GetDataList(1, spaceNames);
            DA.GetDataList(2, sensorLocs);
            if (sensorLocs.Count == 1)
                sensorLocs = Enumerable.Repeat(sensorLocs[0], spaceNames.Count).ToList();
            DA.GetDataList(3, spaceLoads);
            DA.GetDataList(4, schedule);
            DA.GetDataList(5, setPoints);
            if (spaceNames.Count != spaceLoads.Count)
                return;
            DA.GetData(6, ref system_type);
            DA.GetData(7, ref subsys_path);
            List<double> heatLoads = spaceLoads.Select(load => load.Value.T0).ToList();
            List<double> coolLoads = spaceLoads.Select(load => load.Value.T1).ToList();
            List<double> heatSetpoints = setPoints.Select(set => set.Value.T0).ToList();
            List<double> coolSetpoints = setPoints.Select(set => set.Value.T1).ToList();
            // if the cooling and heating setpoint have single value defined, override 
            if (setPoints.Count == 1)
            {
                heatSetpoints = Enumerable.Repeat(setPoints[0].Value.T0, spaceNames.Count).ToList();
                coolSetpoints = Enumerable.Repeat(setPoints[0].Value.T1, spaceNames.Count).ToList();
            }

            // now everything is unpacked from the JSON file
            Floorplan jsFloorplan = JsonSerializer.Deserialize<Floorplan>(jsonSys);
            foreach (SystemZone jsSystem in jsFloorplan.systems)
            {
                if (jsSystem.zones.Count == 0) // skip if no system
                    continue;
                // different system types have different distribution medium mappings
                //          | zone  |  sys  |
                // IDEAL    |   -   |   -   |
                // FCU      |  air  | water |
                // VAV      |  air  |  air  |
                // RAD      | water | water |
                // VRF      |  air  |  rf   |
                if (system_type == 0)
                    jsSystem.type = sysTypeEnum.IDE;
                else if (system_type == 1)
                    jsSystem.type = sysTypeEnum.FCU;
                else if (system_type == 2)
                    jsSystem.type = sysTypeEnum.VAV;
                //else if (system_type == 3)
                //    jsSystem.type = sysTypeEnum.Radiator;
                //else if (system_type == 4)
                //    jsSystem.type = sysTypeEnum.VRF;

                jsSystem.schedule = schedule.ToArray();

                // presume a sizing factor as 1.25
                SystemSizing.Sizing(jsSystem, spaceNames, sensorLocs, 
                    heatLoads, coolLoads, 1.25, heatSetpoints, coolSetpoints);
            }

            // ----------------------------------- DEBUG -------------------------------------
            // a single zone network implementation, only for test

            // GH_Path.Length returns the dimension of the path
            if (subsys_path.Length == 0)
            {
                jsonSys = JsonSerializer.Serialize(jsFloorplan, new JsonSerializerOptions { WriteIndented = true });
            }
            else if (subsys_path.Length == 1)
            {
                Floorplan _jsFloorplan = new Floorplan();
                SystemZone _jsSystem = jsFloorplan.systems[subsys_path.Indices[0]];
                _jsFloorplan.systems = new List<SystemZone>() { _jsSystem };
                jsonSys = JsonSerializer.Serialize(_jsFloorplan, new JsonSerializerOptions { WriteIndented = true });
            }
            // GH_Path.Indices returns an index list from the tree root to leaf 
            else if (subsys_path.Length == 2)
            {
                Floorplan _jsFloorplan = new Floorplan();
                SystemZone _jsSystem = new SystemZone();
                //Tuple<int, int> path = zone_path[subsys_path.Indices[1]];
                _jsSystem = jsFloorplan.systems[subsys_path.Indices[0]];
                _jsSystem.zones = new List<ControlZone>() { jsFloorplan.systems[subsys_path.Indices[0]].zones[subsys_path.Indices[1]] };
                _jsFloorplan.systems = new List<SystemZone>() { _jsSystem };
                jsonSys = JsonSerializer.Serialize(_jsFloorplan, new JsonSerializerOptions { WriteIndented = true });
            }

            DA.SetData(0, jsonSys);

            Util.ScriptPrint(jsonSys, $"network_gen.json", "");
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