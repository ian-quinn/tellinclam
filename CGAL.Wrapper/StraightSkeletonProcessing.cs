using CGAL.Wrapper;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CGAL.Wrapper
{
    public class StraightSkeletonProcessing
    {
        public static Tuple<List<Line>, List<Line>, List<Line>> SsAsPoint3d(List<Polyline> plines)
        {
            // clean and combine the mesh
            //Polyline p = poly.Duplicate();

            // by default
            // the first pline is the outer boundary
            // the rest plines are inner holes

            // info of vertices
            // the xyz coordinates of vertices 
            // System.Object
            //  Rhino.Collections.RhinoList<Point3d>
            //    Rhino.Collections.Point3dList
            //      Rhino.Geometry.Polyline
            // 0: 1,0,0
            // 1: 0,1,0
            // value: 10 01
            int[] vertCountArray = new int[plines.Count];
            int numPts = 0;
            for (int i = 0; i < plines.Count; i++)
            {
                vertCountArray[i] = plines[i].Count;
                numPts += vertCountArray[i];
            }
            double[] vertXyArray = new double[numPts * 2];
            numPts = 0;
            for (int i = 0; i < plines.Count; i++)
            {
                for (int j = 0; j < plines[i].Count; j++)
                {
                    vertXyArray[numPts * 2 + 0] = plines[i][j].X;
                    vertXyArray[numPts * 2 + 1] = plines[i][j].Y;
                    numPts += 1;
                }
            }

            //var vertCount = (ulong)p.Count;
            var holeCount = (ulong)plines.Count;

            // declare output pointer
            IntPtr ss_xy_pointer = IntPtr.Zero;
            IntPtr ss_mask_pointer = IntPtr.Zero;
            IntPtr ss_edge_count_pointer = IntPtr.Zero;

            // CGAL obb processing
            UnsafeNativeMethods.StraightSkeletonByPolygonWithHoles(
                vertXyArray,
                vertCountArray,
                holeCount, 
                ref ss_xy_pointer,
                ref ss_mask_pointer, 
                ref ss_edge_count_pointer);

            // c++ double* => C# double[]
            int[] ss_edge_count_array = new int[1];
            Marshal.Copy(ss_edge_count_pointer, ss_edge_count_array, 0, 1);
            int memory_length = ss_edge_count_array[0] * 4;
            double[] ss_xy_array = new double[memory_length];
            Marshal.Copy(ss_xy_pointer, ss_xy_array, 0, memory_length);
            int[] ss_mask_array = new int[memory_length];
            Marshal.Copy(ss_mask_pointer, ss_mask_array, 0, memory_length);

            // double[] => List<Point3d>
            List<Line> skeletons = new List<Line>();
            List<Line> bisectors = new List<Line>();
            List<Line> contours = new List<Line>();
            //for (int i = 0; i < ss_xy_array.Length; i += 2)
            //{
            //    points.Add(new Point3d(ss_xy_array[i + 0], ss_xy_array[i + 1], 0));
            //}
            for (int i = 0; i * 4 < ss_xy_array.Length; i += 1)
            {
                Line line = new Line(
                    new Point3d(
                        ss_xy_array[i * 4 + 0],
                        ss_xy_array[i * 4 + 1], 0),
                    new Point3d(
                        ss_xy_array[i * 4 + 2],
                        ss_xy_array[i * 4 + 3], 0)
                    );
                if (ss_mask_array[i] == 0)
                {
                    skeletons.Add(line);
                }
                if (ss_mask_array[i] == 1)
                {
                    bisectors.Add(line);
                }
                if (ss_mask_array[i] == 2)
                {
                    contours.Add(line);
                }
            }

            // delete the pointer
            UnsafeNativeMethods.ReleaseDoubleArray(ss_xy_pointer);
            UnsafeNativeMethods.ReleaseIntArray(ss_edge_count_pointer);
            UnsafeNativeMethods.ReleaseIntArray(ss_mask_pointer);

            return new Tuple<List<Line>, List<Line>, List<Line>> (skeletons, bisectors, contours);
        }
        
    }
}
