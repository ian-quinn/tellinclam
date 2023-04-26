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
    public class MinSpanningTree
    {
        public static List<Tuple<int, int>> GetKruskalMST(List<Tuple<int, int>> edges, List<double> weights, out int count)
        {
            int[] edgeArray = new int[weights.Count * 2];
            double[] weightArray = new double[weights.Count];
            for (int i = 0; i < weights.Count; i++)
            {
                edgeArray[2 * i] = edges[i].Item1;
                edgeArray[2 * i + 1] = edges[i].Item2;
                weightArray[i] = weights[i];
            }

            //var vertCount = (ulong)p.Count;
            var edgeCount = (ulong)weights.Count;
            int memory_length = weights.Count * 2;

            // declare output pointer
            IntPtr mst_edge_array_ptr = IntPtr.Zero;
            IntPtr mst_edge_count_ptr = IntPtr.Zero;

            // CGAL obb processing
            UnsafeNativeMethods.KruskalMST(
                edgeArray,
                weightArray,
                edgeCount,
                ref mst_edge_array_ptr, 
                ref mst_edge_count_ptr);

            // c++ double* => C# double[]
            int[] mst_edge_array = new int[memory_length];
            Marshal.Copy(mst_edge_array_ptr, mst_edge_array, 0, memory_length);
            int[] mst_edge_count = new int[1];
            Marshal.Copy(mst_edge_count_ptr, mst_edge_count, 0, 1);

            // double[] => List<Point3d>
            List<Tuple<int, int>> mst_edges = new List<Tuple<int, int>>();

            for (int i = 0; i * 2 < mst_edge_array.Length; i += 1)
            {
                mst_edges.Add(new Tuple<int, int>(
                    mst_edge_array[2 * i], mst_edge_array[2 * i + 1]));
            }

            count = mst_edge_count[0];

            // delete the pointer
            UnsafeNativeMethods.ReleaseDoubleArray(mst_edge_array_ptr);
            UnsafeNativeMethods.ReleaseDoubleArray(mst_edge_count_ptr);

            return mst_edges;
        }

    }
}
