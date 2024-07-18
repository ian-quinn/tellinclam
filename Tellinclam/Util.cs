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
using System.Diagnostics;

namespace Tellinclam
{
    class Util
    {
        static public string ExecuteBatch(string path)
        {
            //int exitCode;
            //ProcessStartInfo processInfo;
            //Process process;

            //processInfo = new ProcessStartInfo("cmd.exe", path);
            //processInfo.CreateNoWindow = false;
            //processInfo.UseShellExecute = false;
            //// *** Redirect the output ***
            //processInfo.RedirectStandardError = true;
            //processInfo.RedirectStandardOutput = true;

            //process = Process.Start(processInfo);
            //process.WaitForExit();

            //// *** Read the streams ***
            //// Warning: This approach can lead to deadlocks, see Edit #2
            //// https://stackoverflow.com/questions/5519328/executing-batch-file-in-c-sharp
            //string output = process.StandardOutput.ReadToEnd();
            //string error = process.StandardError.ReadToEnd();

            //exitCode = process.ExitCode;

            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            //p.StartInfo.WorkingDirectory = workingDir;
            p.StartInfo.Arguments = $"/C python {path}";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            string output = p.StandardOutput.ReadToEnd();

            Console.WriteLine("output>>" + (string.IsNullOrEmpty(output) ? "(none)" : output));
            //Console.WriteLine("error>>" + (String.IsNullOrEmpty(error) ? "(none)" : error));
            //Console.WriteLine("ExitCode: " + exitCode.ToString(), "ExecuteCommand");
            p.Close();

            return output;
        }

        public static void ScriptPrint(string msg, string filename, string outPath)
        {
            string thisAssemblyFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string scriptPath = thisAssemblyFolderPath + $"/{filename}";
            File.WriteAllText(scriptPath, $"{msg}");
            if (outPath != "")
                File.Copy(scriptPath, Path.Combine(outPath, $"{filename}"), true);
        }

        public static void LogPrint(string msg)
        {
            string thisAssemblyFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string logPath = thisAssemblyFolderPath + "/log.txt";
            using (var sw = File.AppendText(logPath))
            {
                sw.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {msg}");
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

        public static List<T> DeepCopy<T>(List<T> items)
        {
            List<T> _items = new List<T>() { };
            foreach (T item in items)
                _items.Add(item);
            return _items;
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

        // supporting functions
        public static List<List<T>> GetCombinations<T>(List<List<T>> lists)
        {
            List<List<T>> result = new List<List<T>>();
            int[] indices = new int[lists.Count];

            while (true)
            {
                List<T> combination = new List<T>();
                for (int i = 0; i < lists.Count; i++)
                {
                    combination.Add(lists[i][indices[i]]);
                }
                result.Add(combination);

                int k = lists.Count - 1;
                while (k >= 0 && indices[k] == lists[k].Count - 1)
                {
                    indices[k] = 0;
                    k--;
                }

                if (k < 0)
                    break;

                indices[k]++;
            }

            return result;
        }

        public static List<T> ConcateLists<T>(List<T> a, List<T> b)
        {
            List<T> newList = new List<T>() { };
            foreach (T item in a)
                newList.Add(item);
            foreach (T item in b)
                newList.Add(item);
            return newList;
        }
    }
}
