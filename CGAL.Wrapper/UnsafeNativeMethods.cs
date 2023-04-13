using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CGAL.Wrapper
{
    internal class UnsafeNativeMethods
    {
        private const string DLL_NAME = "CGAL.Native.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void OrientedBoudningBoxBySurfaceMesh(
            [MarshalAs(UnmanagedType.LPArray)] double[] vert_xyz_array, ulong vert_count, /* input - mesh vertices */
            ref IntPtr obb_pts_xyz
            );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ReleaseDoubleArray(IntPtr arr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ReleaseIntArray(IntPtr arr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void StraightSkeletonByPolygonWithHoles(
            [MarshalAs(UnmanagedType.LPArray)] double[] vert_xy_array, //ulong vert_count,
            [MarshalAs(UnmanagedType.LPArray)] int[] vert_count_array, ulong hole_count, 
            ref IntPtr ss_pts_xy, ref IntPtr ss_type_mask, ref IntPtr ss_edge_count
            );
    }
}
