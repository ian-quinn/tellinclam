using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Tellinclam
{
    public class TCUnzipXML : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public TCUnzipXML()
          : base("gbXML DeSerializer", "UnzipXML",
            "A quick gbXML analyser",
            "Clam", "IO")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("XML path", "Path",
                "The gbXML path for generated gbXML file", GH_ParamAccess.item);
            //pManager.AddTextParameter("Label to lookup", "Label",
            //    "Labels for retreiving polygon loops", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Run", "Run",
                "Run export scripts", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            //pManager.AddPointParameter("Loops", "Groups",
            //    "All vertices loops under the selected label", GH_ParamAccess.tree);
            pManager.AddTextParameter("Space ID", "id",
                "List of space id", GH_ParamAccess.list);
            pManager.AddPointParameter("Space Faces", "space",
                "All vertices loops of surfaces nested in each space", GH_ParamAccess.tree);
            pManager.AddPointParameter("Space Openings", "opening",
                "All opening loops of each space", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string path = "";
            List<string> labels = new List<string>();
            bool run = false;

            if (!DA.GetData(0, ref path) || !DA.GetData(1, ref run))
                return;

            if (!run)
                return;

            DataTree<Point3d> loopTree = new DataTree<Point3d>();
            //for (int i = 0; i < labels.Count; i++)
            //{
            //    XMLDeserialize.Appendix(path, labels[i], out List<List<Point3d>> loops);
            //    for (int j = 0; j < loops.Count; j++)
            //        foreach (Point3d pt in loops[j])
            //            loopTree.Add(pt, new Grasshopper.Kernel.Data.GH_Path(new int[] {0, i, j }));
            //}

            List<string> idList = new List<string>();
            DataTree<Point3d> spaceTree = new DataTree<Point3d>();
            DataTree<Point3d> openingTree = new DataTree<Point3d>();
            DecodeXML.GetSpace(path, out List<string> ids,
                out List<List<List<Point3d>>> spaces, out List<List<List<Point3d>>> openings);
            //Debug.Print($"Space members: {spaces.Count}");
            for (int i = 0; i < spaces.Count; i++)
            {
                idList.Add(ids[i]);
                for (int j = 0; j < spaces[i].Count; j++)
                    foreach (Point3d pt in spaces[i][j])
                    {
                        spaceTree.Add(pt, new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i, j }));
                        //Debug.Print($"Iterate to 0_{i}_{j}");
                    }
                if (openings[i].Count == 0)
                    openingTree.EnsurePath(new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i, 0 }));
                for (int j = 0; j < openings[i].Count; j++)
                    foreach (Point3d pt in openings[i][j])
                    {
                        openingTree.Add(pt, new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i, j }));
                    }
            }

            DA.SetDataList(0, idList);
            DA.SetDataTree(1, spaceTree);
            DA.SetDataTree(2, openingTree);
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
                return Properties.Resources.box_open;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("70DB38E5-147A-4C58-9883-5B1D7C092CDF");
    }
}