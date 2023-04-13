using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel.Data;

namespace Tellinclam
{
    class Util
    {
        public static void LogPrint(string msg)
        {
            using (var sw = File.AppendText(@"D:\tellinclam\log.txt"))
            {
                sw.WriteLine(msg);
            }
        }

        public static string PtLoopToString(List<Point3d> pts)
        {
            string msg = "";
            for (int i = 0; i < pts.Count; i++)
            {
                msg = msg + string.Format("({0}, {1}) ", pts[i].X, pts[i].Y);
            }
            return msg;
        }

        public static void Swap<T>(ref T left, ref T right)
        {
            T temp;
            temp = left;
            left = right;
            right = temp;
        }

        public static string IntListToString(List<int> idx)
        {
            string msg = "";
            for (int i = 0; i < idx.Count; i++)
            {
                msg = msg + string.Format("({0} - ) ", idx[i]);
            }
            return msg;
        }

        public static string DoubleListToString(List<double> nums)
        {
            string msg = "";
            for (int i = 0; i < nums.Count; i++)
            {
                msg += $"{nums[i]}";
                if (i < nums.Count - 1)
                    msg += ", ";
            }
            return msg;
        }

        public static string StringListToString(List<string> labels)
        {
            string msg = "";
            for (int i = 0; i < labels.Count; i++)
            {
                msg += $"{labels[i]}";
                if (i < labels.Count - 1)
                    msg += ", ";
            }
            return msg;
        }


        // convert nested lists to datatree for output
        public static DataTree<T> ListToTree<T>(List<List<T>> list)
        {
            DataTree<T> tree = new DataTree<T>();
            int i = 0;
            foreach (List<T> innerList in list)
            {
                tree.AddRange(innerList, new GH_Path(new int[] { i }));
                i++;
            }
            return tree;
        }
        public static DataTree<T> ListToTree<T>(List<List<List<T>>> list)
        {
            DataTree<T> tree = new DataTree<T>();
            int i = 0;
            foreach (List<List<T>> innerList in list)
            {
                int j = 0;
                foreach (List<T> minorList in innerList)
                {
                    tree.AddRange(minorList, new GH_Path(new int[] { i, j }));
                    j++;
                }
                i++;
            }
            return tree;
        }
        // convert datatree to nested lists for input
        public static List<List<T>> TreeToList<T>(DataTree<T> tree)
        {
            List<List<T>> list = new List<List<T>>();
            for (int i = 0; i < tree.BranchCount; i++)
            {
                GH_Path pth = new GH_Path(i);
                List<T> vecs = tree.Branch(pth);
                list.Add(vecs);
            }
            return list;
        }

        public static List<T> FlattenList<T>(List<List<T>> nestedList)
        {
            List<T> flatList = new List<T>();
            foreach (List<T> list in nestedList)
                flatList.AddRange(list);
            return flatList;
        }
    }
}
