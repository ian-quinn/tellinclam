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
    public class StraightSkeleton
    {
        public static Tuple<List<Line>, List<Tuple<double, double>>, List<Line>, List<Line>> SsAsPoint3d(List<Polyline> plines)
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
            IntPtr ss_time_pointer = IntPtr.Zero;
            IntPtr ss_mask_pointer = IntPtr.Zero;
            IntPtr ss_edge_count_pointer = IntPtr.Zero;

            // CGAL ss processing
            UnsafeNativeMethods.StraightSkeletonByPolygonWithHoles(
                vertXyArray,
                vertCountArray,
                holeCount, 
                ref ss_xy_pointer,
                ref ss_time_pointer, 
                ref ss_mask_pointer, 
                ref ss_edge_count_pointer);

            // c++ double* => C# double[]
            int[] ss_edge_count_array = new int[1];
            Marshal.Copy(ss_edge_count_pointer, ss_edge_count_array, 0, 1);
            int memory_length = ss_edge_count_array[0] * 4;
            double[] ss_xy_array = new double[memory_length];
            Marshal.Copy(ss_xy_pointer, ss_xy_array, 0, memory_length);
            double[] ss_time_array = new double[memory_length];
            Marshal.Copy(ss_time_pointer, ss_time_array, 0, memory_length);
            int[] ss_mask_array = new int[memory_length];
            Marshal.Copy(ss_mask_pointer, ss_mask_array, 0, memory_length);

            // double[] => List<Point3d>
            List<Line> skeletons = new List<Line>();
            List<Tuple<double, double>> heights = new List<Tuple<double, double>>() { };
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
                    heights.Add(new Tuple<double, double>(
                    ss_time_array[i * 2 + 0],
                    ss_time_array[i * 2 + 1])
                    );
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
            UnsafeNativeMethods.ReleaseDoubleArray(ss_time_pointer);
            UnsafeNativeMethods.ReleaseIntArray(ss_edge_count_pointer);
            UnsafeNativeMethods.ReleaseIntArray(ss_mask_pointer);

            return new Tuple<List<Line>, List<Tuple<double, double>>, List<Line>, List<Line>> (skeletons, heights, bisectors, contours);
        }


        public static List<List<Point3d>> OffsetPolygon(List<Polyline> plines, double depth)
        {
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
            double offset = depth;

            //var vertCount = (ulong)p.Count;
            var holeCount = (ulong)plines.Count;

            // declare output pointer
            IntPtr poly_xy_pointer = IntPtr.Zero;
            IntPtr poly_vt_pointer = IntPtr.Zero;
            IntPtr poly_count_pointer = IntPtr.Zero;
            IntPtr pt_count_pointer = IntPtr.Zero;

            // CGAL ss processing
            UnsafeNativeMethods.CreateOffsetPolygons(
                vertXyArray,
                vertCountArray,
                holeCount,
                offset,
                ref poly_xy_pointer,
                ref poly_vt_pointer,
                ref poly_count_pointer,
                ref pt_count_pointer);

            // c++ double* => C# double[]
            int[] poly_count_array = new int[1];
            Marshal.Copy(poly_count_pointer, poly_count_array, 0, 1);
            int[] pt_count_array = new int[1];
            Marshal.Copy(pt_count_pointer, pt_count_array, 0, 1);

            int memory_length = pt_count_array[0] * 2;  // does the polygon has only four vertices? need debugging
            // or you just make it as large as enough

            double[] poly_xy_array = new double[memory_length];
            Marshal.Copy(poly_xy_pointer, poly_xy_array, 0, memory_length);
            int[] poly_vt_array = new int[poly_count_array[0]];
            Marshal.Copy(poly_vt_pointer, poly_vt_array, 0, poly_count_array[0]);


            // double[] => List<Point3d>
            List<List<Point3d>> polys = new List<List<Point3d>>() { };
            int pointer = 0;
            for (int i = 0; i < poly_count_array[0]; i += 1)
            {
                // loop each polygon
                List<Point3d> poly = new List<Point3d>() { };
                for (int j = 0; j < poly_vt_array[i]; j += 1)
                {
                    // loop each point
                    poly.Add(new Point3d(
                        poly_xy_array[pointer * 2 + 0],
                        poly_xy_array[pointer * 2 + 1], 0));
                    pointer++;
                }
                polys.Add(poly);
            }

            // delete the pointer
            UnsafeNativeMethods.ReleaseDoubleArray(poly_xy_pointer);
            UnsafeNativeMethods.ReleaseIntArray(poly_count_pointer);
            UnsafeNativeMethods.ReleaseIntArray(poly_vt_pointer);

            return polys;
        }

    }
}
