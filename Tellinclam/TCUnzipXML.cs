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
            "A quick gbXML deSerializer",
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
            pManager.AddTextParameter("Surface id", "id",
                "Kookup geometry item by id", GH_ParamAccess.item);
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
            pManager.AddCurveParameter("Retrieved Surface", "surface",
                "The surface retrieved", GH_ParamAccess.item);
            pManager.AddCurveParameter("Space Faces", "space",
                "All vertices loops of surfaces nested in each space", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Space Openings", "opening",
                "All opening loops of each space", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Column", "column", "", GH_ParamAccess.list);
            pManager.AddBrepParameter("Beam", "beam", "", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string path = "";
            string srfId = "";
            bool run = false;

            if (!DA.GetData(0, ref path) || !DA.GetData(2, ref run))
                return;
            DA.GetData(1, ref srfId);
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
            Polyline surface = new Polyline(new Point3d[0]);
            DataTree<Polyline> spaceTree = new DataTree<Polyline>();
            DataTree<Polyline> openingTree = new DataTree<Polyline>();
            SerializeXML.GetSpace(path, out List<string> ids,
                out List<List<Polyline>> spaces, out List<List<Polyline>> openings);
            SerializeXML.GetColBeam(path, out List<Brep> columns, out List<Brep> beams);
            //Debug.Print($"Space members: {spaces.Count}");

            // prepare outputs
            // with specific surface id given, only output adjacent spaces
            if (SerializeXML.GetSurface(path, srfId, out Polyline boundary, out Tuple<string, string> adjSpace))
            {
                surface = boundary;
                for (int i = 0; i < spaces.Count; i++)
                {
                    if (ids[i] == adjSpace.Item1 || ids[i] == adjSpace.Item2)
                    {
                        idList.Add(ids[i]);
                        for (int j = 0; j < spaces[i].Count; j++)
                            spaceTree.Add(spaces[i][j], new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i }));
                        if (openings[i].Count == 0)
                            openingTree.EnsurePath(new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i }));
                        for (int j = 0; j < openings[i].Count; j++)
                            openingTree.Add(openings[i][j], new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i }));
                    }
                }
            }
            else
            {
                for (int i = 0; i < spaces.Count; i++)
                {
                    idList.Add(ids[i]);
                    for (int j = 0; j < spaces[i].Count; j++)
                        spaceTree.Add(spaces[i][j], new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i}));
                    if (openings[i].Count == 0)
                        openingTree.EnsurePath(new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i}));
                    for (int j = 0; j < openings[i].Count; j++)
                        openingTree.Add(openings[i][j], new Grasshopper.Kernel.Data.GH_Path(new int[] { 0, i}));
                }
            }
            
            DA.SetDataList(0, idList);
            DA.SetData(1, surface);
            DA.SetDataTree(2, spaceTree);
            DA.SetDataTree(3, openingTree);
            DA.SetDataList(4, columns);
            DA.SetDataList(5, beams);
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