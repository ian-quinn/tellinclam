using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CGAL.Wrapper
{
    public class PolygonMeshProcessing
    {
        public static List<Point3d> ObbAsPoint3d(List<Point3d> pts)
        {
            // info of vertices
            // the xyz coordinates of vertices of a mesh
            // 0: 1,0,0
            // 1: 0,1,0
            // 2: 0,0,1
            // value: 100 010 001
            double[] vertXyzArray = new double[pts.Count * 3];
            for (int i = 0; i < pts.Count; i++)
            {
                vertXyzArray[i * 3 + 0] = pts[i].X;
                vertXyzArray[i * 3 + 1] = pts[i].Y;
                vertXyzArray[i * 3 + 2] = pts[i].Z;
            }

            var vertCount = (ulong)pts.Count;

            // declare output pointer
            IntPtr obb_xyz_pointer = IntPtr.Zero;

            // CGAL obb processing
            UnsafeNativeMethods.OrientedBoudningBoxBySurfaceMesh(
                vertXyzArray,
                vertCount,
                ref obb_xyz_pointer);

            // c++ double* => C# double[]
            // here we already know the length
            // the number of bounding box vertices must be 8
            double[] obb_xyz_array = new double[8 * 3];
            Marshal.Copy(obb_xyz_pointer, obb_xyz_array, 0, 8 * 3);

            // double[] => List<Point3d>
            List<Point3d> points = new List<Point3d>();
            for (int i = 0; i < obb_xyz_array.Length; i += 3)
            {
                points.Add(new Point3d(
                    obb_xyz_array[i + 0],
                    obb_xyz_array[i + 1],
                    obb_xyz_array[i + 2]));
            }

            // delete the pointer
            UnsafeNativeMethods.ReleaseDoubleArray(obb_xyz_pointer);

            return points;
        }
    }
}
