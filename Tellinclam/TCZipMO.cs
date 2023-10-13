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
            // change these inputs to a nested json file
            pManager.AddTextParameter("IDF path", "idf",
                "The path of IDF for reference", GH_ParamAccess.item);
            pManager.AddTextParameter("EPW/MOS path", "epw",
                "The path of EPW/MOS for EnergyPlus/Modelica simulation. The names must be the same except for the extension.", GH_ParamAccess.item);
            pManager.AddTextParameter("MO path", "mo",
                "The path of output Modelica scripts", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Debug Mode", "DEV",
                "Switch to debug mode. In debug mode, the pressure loss at pipe/duct will not be modeled as real world.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Modelica Scripts", "out","The serialized Modelica scripts", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string json = "";
            string idfPath = "";
            string epwPath = "";
            string moPath = "";
            string modelName = "unnamed";
            bool isDev = true;

            DA.GetData(0, ref modelName);
            DA.GetData(2, ref idfPath);
            DA.GetData(3, ref epwPath);
            DA.GetData(5, ref isDev);
            if (!DA.GetData(1, ref json) || !DA.GetData(4, ref moPath))
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
            Floorplan jsFloorplan = JsonSerializer.Deserialize<Floorplan>(json);
            string scripts = SerializeMO.RecFancoil(jsFloorplan, modelName, idfPath, epwPath, !isDev);

            Util.ScriptPrint(scripts, $"{modelName}.mo", moPath);

            DA.SetData(0, scripts);
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