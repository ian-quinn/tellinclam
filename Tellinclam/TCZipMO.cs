using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Tellinclam.Serialization;
using static Tellinclam.Serialization.SchemaJSON;
using System.Text.Json;
using System.IO;

namespace Tellinclam
{
    public class TCZipMO : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCZipMO()
          : base("Modelica Serializer", "ZipMO",
            "Compile current zoning scheme into SpawnEnergyplus Modelica scripts",
            "Clam", "IO")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Model name", "name",
                "Set the name tag of the model", GH_ParamAccess.item);
            // put a treedata here in future
            pManager.AddTextParameter("System info", "json",
                "System information serialized in a JSON file", GH_ParamAccess.item);
            pManager.AddTextParameter("Simulation settings", "ctrl", "Simulation settings", GH_ParamAccess.item);
            // change these inputs to a nested json file
            pManager.AddTextParameter("IDF path", "idf",
                "The path of IDF for reference", GH_ParamAccess.item);
            pManager.AddTextParameter("EPW/MOS path", "epw",
                "The path of EPW/MOS for EnergyPlus/Modelica simulation. The names must be the same except for the extension.", GH_ParamAccess.item);
            pManager.AddTextParameter("MO path", "mo",
                "The path of output Modelica scripts", GH_ParamAccess.item);
            pManager.AddTextParameter("Buildings Library path", "pkg",
                "The path of Modelica library Buildings (LBNL)", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run Simulation", "run",
                "Run Simulation (this may take a while. be careful doing this)", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Modelica Scripts", ".mo","The serialized Modelica scripts", GH_ParamAccess.item);
            pManager.AddTextParameter("Result file (.mat)", "res", "OpenModelica result file .mat", GH_ParamAccess.item);
            pManager.AddTextParameter("Simulation Log", "log", "Command line output during simulation batch", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string jsonSys = "";
            string jsonSet = "";
            string idfPath = "";
            string epwPath = "";
            string moPath = "";
            string pkgPath = "";
            string modelName = "unnamed";
            bool isRun = false;

            DA.GetData(0, ref modelName);
            DA.GetData(2, ref jsonSet);
            DA.GetData(3, ref idfPath);
            DA.GetData(4, ref epwPath);
            DA.GetData(7, ref isRun);
            if (!DA.GetData(1, ref jsonSys) || !DA.GetData(5, ref moPath) || !DA.GetData(6, ref pkgPath))
                return;

            // -------------------------- LEGACY VER ------------------------------------

            //breps = breps.Where(b => b.IsSolid).ToList();
            //if (breps == null || breps.Count == 0) return;

            //List<double> vols = new List<double>();
            //vols = breps.Select(b => b.GetVolume()).ToList();

            //List<string> labels = new List<string>() { "1" };
            //List<double> vols = new List<double>() { 2.0 };
            //int primary = 0;

            // here should be a loop for the generation of each zone
            //string scripts = SerializeMO.AirHeating(modelName, labels, vols, primary, idfPath);

            // --------------------------------------------------------------------------

            // now everything is unpacked from the JSON file
            Floorplan jsFloorplan = JsonSerializer.Deserialize<Floorplan>(jsonSys);
            SimulationSettings jsCtrls = JsonSerializer.Deserialize<SimulationSettings>(jsonSet);
            string script_mo = SerializeMO.RecFancoil(jsFloorplan, jsCtrls, modelName, idfPath, epwPath, false);

            string dir_package = Path.Combine(moPath, modelName);
            string dir_openstudio = Path.Combine(dir_package, "openmodelica");
            // If directory does not exist, create it
            if (!Directory.Exists(dir_package))
                Directory.CreateDirectory(dir_package);
            Util.ScriptPrint(script_mo, $"{modelName}.mo", dir_package);
            if (!Directory.Exists(dir_openstudio))
                Directory.CreateDirectory(dir_openstudio);

            string script_py = "";
            script_py += "import os\n";
            script_py += "from OMPython import OMCSessionZMQ\n\n";
            script_py += $"os.chdir(\"{dir_openstudio.Replace(@"\", "/")}\")\n";
            script_py += "omc = OMCSessionZMQ()\n";
            script_py += "omc.sendExpression(\"loadModel(Modelica)\")\n";
            script_py += $"omc.sendExpression(\"loadFile(\\\"{Path.Combine(pkgPath, "package.mo").Replace(@"\", "/")}\\\")\")\n";
            script_py += $"omc.sendExpression(\"loadFile(\\\"../{modelName}.mo\\\")\")\n";
            script_py += $"result = omc.sendExpression(\"simulate({modelName})\")\n";
            script_py += "print(result['resultFile'] + '@', end='')\n";
            script_py += "print(result['messages'] + '@', end='')\n";
            script_py += "print(result['timeTotal'], end='')\n";
            Util.ScriptPrint(script_py, $"{modelName}.py", dir_openstudio);

            //string script_bat = "";
            //script_bat += $"@echo off\npython {modelName}.py\npause";
            //Util.ScriptPrint(script_bat, $"{modelName}.bat", dir_openstudio);

            string log = "";
            if (isRun)
                log = Util.ExecuteBatch(Path.Combine(dir_openstudio, $"{modelName}.py"));

            if (dir_package != "")
                File.WriteAllText(Path.Combine(dir_package, $"{modelName}.json"), $"{jsonSys}");

            DA.SetData(0, Path.Combine(dir_package, $"{modelName}.mo"));
            DA.SetData(1, "");
            DA.SetData(2, "");
            if (log != "")
            {
                DA.SetData(1, log.Split('@')[0]);
                DA.SetData(2, log.Split('@')[1]);
            }
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
                return Properties.Resources.box_closed;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("084CB3DD-BEBB-4946-9CD1-B61547753622");
    }
}