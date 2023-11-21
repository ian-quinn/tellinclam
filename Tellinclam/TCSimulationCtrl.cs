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

namespace Tellinclam
{
    public class TCSimulationCtrl : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCSimulationCtrl()
          : base("Simulation Control", "SimCtrl",
            "Simulation configurations",
            "Clam", "IO")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Start time (Month)", "start_month", "Start time (Month)", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Start time (Day)", "start_day", "Start time (Day)", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Stop time (Month)", "stop_month", "Stop time (Month)", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Stop time (Day)", "stop_day", "Stop time (Day)", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Interval of time step", "timestep", "Interval of teim step", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "tolerance", "tolerance", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Solver Algorithm", "algorithm", "Pick the solver algorithms", GH_ParamAccess.item);
            Param_Integer param = pManager[6] as Param_Integer;
            param.AddNamedValue("Dassl", 0);
            param.AddNamedValue("Euler", 1);
            param.AddNamedValue("IDA", 2);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Serialized simulation settings", "ctrl", "Serialized simulation settings.", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string jsonSet = "";
            int start_month = 1;
            int start_day = 1;
            int stop_month = 1;
            int stop_day = 5;

            int interval = 600;
            double tolerance = 0.000001;

            int algorithm = 0;

            DA.GetData(0, ref start_month);
            DA.GetData(1, ref start_day);
            DA.GetData(2, ref stop_month);
            DA.GetData(3, ref stop_day);

            DA.GetData(4, ref interval);
            DA.GetData(5, ref tolerance);

            DA.GetData(6, ref algorithm);

            // if the cooling and heating setpoint are all the same
            
            SimulationSettings simSets = new SimulationSettings();
            simSets.info = "";
            int year = DateTime.Now.Year;
            simSets.startTime = (new DateTime(year, start_month, start_day) - new DateTime(year, 1, 1)).Days * 3600 * 24;
            simSets.stopTime = (new DateTime(year, stop_month, stop_day) - new DateTime(year, 1, 1)).Days * 3600 * 24;
            simSets.interval = interval;
            simSets.tolerance = tolerance;
            if (algorithm == 0)
                simSets.algorithm = "Dassl";
            else if (algorithm == 1)
                simSets.algorithm = "Euler";
            else
                simSets.algorithm = "IDA";

            jsonSet = JsonSerializer.Serialize(simSets, new JsonSerializerOptions { WriteIndented = true });

            DA.SetData(0, jsonSet);
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
                return Properties.Resources.config;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("DF5BC462-FFD4-43F6-A3AA-9596609AA971");
    }
}