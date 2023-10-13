using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Tellinclam.Algorithms;

namespace Tellinclam
{
    // just have a try...
    public class gbRegion
    {
        public string label; // label of current region
        public List<Point3d> loop; // vertice loop of this region
        public List<string> match; // label of the adjacent edge
        public bool isShell = false; // reconsider this
        //public bool isMCR = false; // reconsider this

        // null by default
        public List<List<Point3d>> innerLoops;
        public List<List<string>> innerMatchs;
        public List<List<Point3d>> tiles;

        public gbRegion(string label, List<Point3d> loop, List<string> match)
        {
            this.label = label;
            this.loop = loop;
            this.match = match;
        }
        //public void InitializeMCR()
        //{
        //    if (innerLoops != null && innerMatchs != null)
        //    {
        //        List<List<Point3d>> mcr = new List<List<Point3d>>();
        //        mcr.Add(loop);
        //        mcr.AddRange(innerLoops);
        //        tiles = RegionTessellate.Rectangle(mcr);
        //        //isMCR = true;
        //        return;
        //    }
        //    return;
        //}
    }

    public static class RegionDetect
    {
        public static object Rhino { get; private set; }

        // this function is only for the sorted, grouped, fixed wall centerlines
        // which will be generalized to the entire floorplan. For floorplan, there will be
        // nested lists of points representing boundaries of each space
        // nested lists of points representing boundaries of each floor slab (there may be multiple isolated slabs)
        // nested lists of strings representing the surface matching relationships.
        // the surface matching across different levels will not be covered here
        public static void GetRegion(List<Line> lines, out List<Polyline> regions, 
            out List<Line> orphans) // out List<gbXYZ> shell, 
        {

            List<Point3d> Vtc = new List<Point3d>(); // all unique vertices
            List<Line> HC = new List<Line>(); // list of all shattered half-curves
            List<int> HCI = new List<int>(); // half curve indices
            List<int> HCO = new List<int>(); // half curve reversed
            List<int> HCN = new List<int>(); // next index for each half-curve (counter-clockwise)
            List<int> HCV = new List<int>(); // vertex representing this half-curve
            List<int> HCF = new List<int>(); // half-curve face
            List<Vector3d> HCPln = new List<Vector3d>();
            List<bool> HCK = new List<bool>(); // mark if a half-curve needs to be killed
                                               // (if it either starts or ends hanging, but does not exclude redundant curves that not exclosing a room)
            Dictionary<int, List<Line>> F = new Dictionary<int, List<Line>>(); // data tree for faces
            Dictionary<int, List<int>> FIdx = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> VOut = new Dictionary<int, List<int>>(); // data tree of outgoing half-curves from each vertex

            foreach (Line line in lines) // cycle through each curve
            {
                for (int CRun = 0; CRun <= 2; CRun += 2) // create two half-curves: first in one direction, and then the other...
                {
                    Point3d testedPt = line.PointAt(0);

                    HC.Add(line);
                    HCI.Add(HCI.Count); // count this iteration
                    HCO.Add(HCI.Count - CRun); // a little index trick
                    HCN.Add(-1);
                    HCF.Add(-1);
                    HCK.Add(false);

                    int VtcSet = -1;

                    for (int VtxCheck = 0; VtxCheck <= Vtc.Count - 1; VtxCheck++)
                    {
                        if (Vtc[VtxCheck].DistanceTo(testedPt) < 1e-6) // set to a global value!!
                        {
                            VtcSet = VtxCheck; // get the vertex index, if it already exists
                            break;
                        }
                    }

                    if (VtcSet > -1)
                    {
                        HCV.Add(VtcSet); // If the vertex already exists, set the half-curve vertex
                        VOut[VtcSet].Add(HCI.Last());
                    }
                    else
                    {
                        HCV.Add(Vtc.Count); // if the vertex doesn't already exist, add a new vertex index
                        VOut.Add(Vtc.Count, new List<int>() { HCI.Last() });
                        // add the new half-curve index to the list of outgoing half-curves associated with the vertex
                        Vtc.Add(testedPt);
                        // add the new vertex to the vertex list
                    }
                    HCPln.Add(line.Direction / line.Length);

                    // reverse the curve for creating the opposite
                    // half - curve in the second part of the loop
                    line.Flip();
                }
            }

            // For each Vertex that has only one outgoing half-curve, kill the half-curve and its opposite
            foreach (KeyValuePair<int, List<int>> path in VOut)
            {
                //Debug.Print("SpaceDetect:: " + "This point has been connected to " + path.Value.Count.ToString() + " curves");
                if (path.Value.Count == 1)
                {
                    HCK[path.Value[0]] = true;
                    HCK[HCO[path.Value[0]]] = true;
                }
            }


            // Find the "next" half-curve for each starting half curve by
            // identifying the outgoing half-curve from the end vertex
            // that presents the smallest angle by calculating its plane's x-axis angle
            // from x-axis of the starting half-curve's opposite plane
            foreach (int HCIdx in HCI)
            {
                int minIdx = -1;
                double minAngle = 2 * Math.PI;
                //Debug.Print("SpaceDetect:: " + VOut[HCV[HCO[HCIdx]]].Count().ToString());
                foreach (int HCOut in VOut[HCV[HCO[HCIdx]]])
                {
                    if (HCOut != HCO[HCIdx] & HCK[HCIdx] == false & HCK[HCOut] == false)
                    {
                        double testAngle = 2 * Math.PI - Basic.VectorAngle2PI(HCPln[HCOut], HCPln[HCO[HCIdx]]);

                        //Rhino.RhinoApp.Write(testAngle.ToString() + "\n");
                        // The comparing order is important to ensure a right-hand angle under z-axis
                        if (testAngle < minAngle)
                        {
                            minIdx = HCOut;
                            minAngle = testAngle;
                        }
                    }
                }
                HCN[HCIdx] = minIdx;
            }


            // Sequence half-curves into faces by running along "next" half-curves in order
            // until the starting half-curve is returned to

            // this list contain the generated face with stray edge or polys not enclosed
            // which will be deleted. typically, a well trimmed and documented half line input
            // will produce no orphan faces. the F dictionary only contains a counter-clockwise
            // outer shell and the rest clockwise enclosed regions.
            List<int> orphanId = new List<int>();
            // cycle through each half-curve
            foreach (int HCIdx in HCI)
            {
                int emExit = 0;
                if (HCF[HCIdx] == -1)
                {
                    int faceIdx = F.Count();
                    int currentIdx = HCIdx;
                    F.Add(faceIdx, new List<Line>() { HC[currentIdx] });
                    FIdx.Add(faceIdx, new List<int>() { currentIdx });
                    HCF[currentIdx] = faceIdx;
                    do
                    {
                        // this denotes a half-curve 
                        if (HCN[currentIdx] == -1)
                        {
                            orphanId.Add(faceIdx);
                            //Debug.Print("Log 1 orphan for missing next HC " + orphanId.Count);
                            //Util.LogPrint($"RegionDetect: Orphan located as Z::{faceIdx} for missing next outward segment");
                            break;
                        }

                        currentIdx = HCN[currentIdx];
                        F[faceIdx].Add(HC[currentIdx]);
                        FIdx[faceIdx].Add(currentIdx);
                        HCF[currentIdx] = faceIdx;
                        if (HCN[currentIdx] == HCIdx)
                            break;
                        // emergency exit prevents infinite loops
                        emExit += 1;
                        if (emExit == lines.Count - 1)
                        {
                            orphanId.Add(faceIdx);
                            //Util.LogPrint($"RegionDetect: Orphan located as Z::{faceIdx} for infinite looping");
                            break;
                        }
                    }
                    while (true);
                    // exit once the starting half-curve is reached again
                }
            }


            // this list cache the outer shell face id
            List<int> shellId = new List<int>();
            foreach (KeyValuePair<int, List<Line>> kvp in F)
            {
                if (orphanId.Contains(kvp.Key))
                    continue;

                // if the face edges are not enclosed, regard them as orphans

                //segIntersectEnum intersectionCheck = GBMethod.SegIntersection(
                //    kvp.Value[0], kvp.Value.Last(), 0.0001, out gbXYZ intersection, out double t1, out double t2);
                //if (!(intersectionCheck == segIntersectEnum.IntersectOnBoth ||
                //    intersectionCheck == segIntersectEnum.ColineJoint))
                var ccx = Intersection.CurveCurve(
                    new LineCurve(kvp.Value[0]), new LineCurve(kvp.Value.Last()), 0.0001, 0.0001);
                if (ccx.Count == 0)
                {
                    orphanId.Add(kvp.Key);
                    //Debug.Print($"Log #{orphanId.Count} orphan for {intersectionCheck}. at {kvp.Key}");
                    //foreach (gbSeg edge in kvp.Value)
                    //Debug.Print($"{{{edge}}}");
                    //Debug.Print($"Log #{orphanId.Count} orphan for not joined. at {kvp.Key}");
                    //Util.LogPrint($"RegionDetect: Orphan located as Z::{kvp.Key} for missing {intersectionCheck}");

                    continue;
                }
                // if the loop is clockwise, regard it as outer shell
                // typically there's only one outer shell
                Polyline ptLoop = new Polyline(SortPtLoop(kvp.Value));
                if (Basic.IsClockwise(ptLoop))
                    shellId.Add(kvp.Key);
            }

            //Util.LogPrint(Util.IntListToString(orphanId));
            //Util.LogPrint(Util.IntListToString(shellId));

            // crvLoops to cache all space boundaries. The lines are following counter-clockwise order around the space,
            // but the direction of each is random.
            List<List<Line>> edgeLoops = new List<List<Line>>();  // for debugging
            List<List<Point3d>> ptLoops = new List<List<Point3d>>();
            List<gbRegion> regions_gb = new List<gbRegion>();
            // for debugging. considering to generate gbZone/gbSurface directly
            List<List<string>> infoLoops = new List<List<string>>();

            //int renumberOffset = 0;
            // only output those faces that haven't been identified as either the perimeter or open
            // note that the region loop should be counter-clockwise and closed and that's how we sort them out

            foreach (KeyValuePair<int, List<Line>> kvp in F)
            {
                // if the face is orphan skip the matching process
                if (orphanId.Contains(kvp.Key)) // || shellId.Contains(kvp.Key)
                {
                    //renumberOffset++;
                    ptLoops.Add(new List<Point3d>());
                    infoLoops.Add(new List<string>());
                    regions_gb.Add(new gbRegion("G" + /*groupId*/"0" + "::Z" + kvp.Key.ToString(),
                        new List<Point3d>(), new List<string>()));
                    //Debug.Print($"SpaceDetect:: Unexpected orphan detected at F{levelId}::B{blockId}::G{groupId}::Z{kvp.Key}");
                    continue;
                }

                List<Line> edgeLoop = new List<Line>();
                List<string> infoLoop = new List<string>();
                for (int j = 0; j < kvp.Value.Count; j++)
                {
                    int adjCrvIdx = GetMatchIdx(FIdx[kvp.Key][j]);
                    //FIdx.TryGetValue(HCF[adjCrvIdx], out List<int> adjFace);
                    List<int> banned = new List<int>();
                    banned.AddRange(orphanId);
                    banned.AddRange(shellId);
                    int faceIdx = GetMatchFIdx(FIdx, adjCrvIdx, banned);

                    string boundaryCondition;
                    // the naming convention should follow the XML serialization
                    //if (orphanId.Contains(HCF[adjCrvIdx]) || shellId.Contains(HCF[adjCrvIdx]))
                    //{
                    //    boundaryCondition = "F" + levelId + "::B" + blockId + "::G" + groupId + "::Z" + HCF[adjCrvIdx].ToString() +
                    //        "::Outside_" + adjFace.IndexOf(adjCrvIdx).ToString();
                    //}
                    // the HCF[adjCrvIdx] is wrong. This only returns the first belonging space which probably could be the orphan space
                    if (faceIdx == -1)
                    {
                        //boundaryCondition = "Outside";
                        // record the matching relations between shell and inner zone boundaries
                        int shellIdx = GetMatchFIdx(FIdx, adjCrvIdx, orphanId);
                        if (shellIdx == -1)
                            boundaryCondition = "G0" + /*groupId +*/ "::Z" + shellIdx.ToString() +
                                "::Outside_X";
                        else
                            // at this stage, HCF[adjCrvIdx] only indicates to the orphan space
                            boundaryCondition = "G0" + /*groupId +*/ "::Z" + HCF[adjCrvIdx].ToString() +
                                "::Outside_" + FIdx[shellIdx].IndexOf(adjCrvIdx).ToString();
                    }
                    else
                        //boundaryCondition = "Level_" + levelId + "::Zone_" + (HCF[adjCrvIdx] - renumberOffset).ToString() +
                        //    "::Wall_" + adjFace.IndexOf(adjCrvIdx).ToString();
                        boundaryCondition = "G0" + /*groupId +*/ "::Z" + faceIdx.ToString() +
                            "::Wall_" + FIdx[faceIdx].IndexOf(adjCrvIdx).ToString();

                    edgeLoop.Add(kvp.Value[j]);
                    infoLoop.Add(boundaryCondition);
                }
                edgeLoops.Add(edgeLoop);

                List<Point3d> ptLoop = SortPtLoop(edgeLoop);
                ptLoops.Add(ptLoop);

                infoLoops.Add(infoLoop);

                // mark the shell region. make it the first element in the list: regions
                // this could mandate every nested regions to have its outer shell
                //Debug.Print($"Log preparing for region F{levelId}::B{blockId}::G{groupId}::Z{kvp.Key} with {ptLoop.Count} edges");
                gbRegion newRegion = new gbRegion("G" + /*groupId +*/ "::Z" + kvp.Key.ToString(), ptLoop, infoLoop);
                if (shellId.Contains(kvp.Key))
                {
                    newRegion.isShell = true;
                    regions_gb.Insert(0, newRegion);
                }
                else
                    regions_gb.Add(newRegion);
            }

            // outputs
            regions = new List<Polyline>() { };
            foreach (gbRegion region_gb in regions_gb)
            {
                if (region_gb.loop.Count > 0)
                {
                    List<Point3d> vertices = region_gb.loop;
                    vertices.Add(vertices[0]);
                    Polyline closedPoly = new Polyline(vertices);
                    //if (closedPoly.IsValid && closedPoly.IsClosed)
                    regions.Add(closedPoly);
                }
            }

            List<List<Line>> orphanLoops = new List<List<Line>>();
            foreach (int id in orphanId)
                orphanLoops.Add(F[id]);
            orphans = Util.FlattenList(orphanLoops);
        }

        public static void GetMCR(List<List<gbRegion>> nestedRegion)
        // nest all loops
        //List<List<gbXYZ>> nestedShell // out List<List<List<gbXYZ>>> mcrs)
        {
            List<Tuple<int, int>> containRef = new List<Tuple<int, int>>(); // containment relations
            List<int> roots = new List<int>(); // shell index as root node
            List<int> branches = new List<int>(); // branches[0] encloses branches[1] encloses branches[2]

            // iterate to find all containment relations
            for (int i = 0; i < nestedRegion.Count; i++)
            {
                // nestedRegion[i].Count must > 1 to avoid [0] indexation incurs 'System.ArgumentOutOfRangeException'
                if (nestedRegion[i].Count == 0)
                    continue;

                // to prevent null shells exist
                if (nestedRegion[i][0].loop.Count == 0)
                    continue;
                for (int j = i + 1; j < nestedRegion.Count; j++)
                {
                    // nestedRegion[j][0] may trigger 'System.ArgumentOutOfRangeException'
                    if (nestedRegion[j].Count == 0)
                        continue;

                    if (Basic.IsPtInPoly(nestedRegion[i][0].loop[0], new Polyline(nestedRegion[j][0].loop), false) == true)
                    {
                        containRef.Add(Tuple.Create(j, i));
                        if (!branches.Contains(i))
                            branches.Add(i);
                        continue;
                    }
                    if (Basic.IsPtInPoly(nestedRegion[j][0].loop[0], new Polyline(nestedRegion[i][0].loop), false) == true)
                    {
                        containRef.Add(Tuple.Create(i, j));
                        if (!branches.Contains(j))
                            branches.Add(j);
                        continue;
                    }
                }
            }

            // locate root nodes
            for (int i = 0; i < nestedRegion.Count; i++)
            {
                if (!branches.Contains(i))
                    roots.Add(i);
            }

            // DEBUG
            //Debug.Write("SpaceDetect:: " + "Roots: ");
            //foreach (int num in roots)
            //    Debug.Write(num.ToString() + ", ");
            //Debug.Write("\n");
            //Debug.Write("SpaceDetect:: " + "Branches: ");
            //foreach (int num in branches)
            //    Debug.Write(num.ToString() + ", ");
            //Debug.Write("\n");
            //foreach (Tuple<int, int> idx in containRef)
            //{
            //    Debug.Print("SpaceDetect:: " + "Containment: ({0}, {1})", idx.Item1, idx.Item2);
            //}

            // create root nodes (creating containment tree)
            List<List<int>> chains = new List<List<int>>();
            for (int i = containRef.Count - 1; i >= 0; i--)
            {
                if (roots.Contains(containRef[i].Item1))
                {
                    List<int> chain = new List<int>() { containRef[i].Item1, containRef[i].Item2 };
                    chains.Add(chain);
                    containRef.RemoveAt(i);
                }
            }
            // create branch nodes (creating containment tree)
            int safeLock = 0;
            // not possible to have a nesting over 10 levels
            while (containRef.Count > 0 && safeLock < 10)
            {
                //Debug.Print("SpaceDetect:: " + "Iteration at: " + safeLock.ToString() + " with " + containRef.Count.ToString() + "chains.");
                int delChainIdx = -1;
                int delCoupleIdx = -1;
                foreach (var couple in containRef)
                {
                    foreach (List<int> chain in chains)
                    {
                        if (couple.Item2 == chain[chain.Count - 1])
                            delChainIdx = chains.IndexOf(chain);
                    }
                    foreach (List<int> chain in chains)
                    {
                        if (couple.Item1 == chain[chain.Count - 1])
                        {
                            delCoupleIdx = containRef.IndexOf(couple);
                            chain.Add(couple.Item2);
                        }
                    }
                }
                if (delChainIdx >= 0 && delCoupleIdx >= 0)
                {
                    chains.RemoveAt(delChainIdx);
                    containRef.RemoveAt(delCoupleIdx);
                }
                safeLock++;
            }

            // DEBUG
            //Debug.Print("SpaceDetect:: " + "Num of Chains " + chains.Count.ToString());
            //foreach (List<int> chain in chains)
            //{
            //    Debug.Write("SpaceDetect:: " + "Chain-" + chains.IndexOf(chain).ToString());
            //    Debug.Write(" ::Index-");
            //    foreach (int num in chain)
            //        Debug.Write(num.ToString() + ", ");
            //    Debug.Write("\n");
            //}

            int depth = 0;
            foreach (List<int> chain in chains)
            {
                if (nestedRegion[chain.Last()].Count == 2)
                    if (Basic.GetPolyArea(nestedRegion[chain.Last()][0].loop.ToList()) < 10)
                    {
                        // the nestRegion with only one region
                        // one is the clockwise region and the other is the counter-clockwise shell
                        nestedRegion[chain.Last()][0].loop = null;
                        nestedRegion[chain.Last()][1].loop = null;
                        nestedRegion[chain.Last()][0].isShell = false;
                        chain.RemoveAt(chain.Count - 1);
                        //Debug.Print("SpaceDetect:: Remove one region with too small area");
                    }
                if (chain.Count > depth)
                {
                    depth = chain.Count;
                }
            }
            //Debug.Print("SpaceDetect:: Containment chain has depth: " + depth);
            // DEBUG
            // Rhino.RhinoApp.WriteLine("Note the depth of tree is: " + depth.ToString());
            // foreach (List<Point3d[]> item in groups)
            // {
            //   Rhino.RhinoApp.WriteLine("Length of the first " + item.Count.ToString());
            // }

            // Prepare regex for label decoding
            var pattern = "(.+)::(.+)";

            // generate Point Array pairs for multi-connected region
            // List<List<List<gbXYZ>>> mcrs = new List<List<List<gbXYZ>>>();
            List<string> mcrParentLabel = new List<string>();
            for (int i = 1; i < depth; i++)
            {
                foreach (List<int> chain in chains)
                {
                    if (i < chain.Count)
                    {
                        // loop through the parent group to find the right parent loop
                        foreach (gbRegion region in nestedRegion[chain[i - 1]])
                        {
                            if (region.isShell == true)
                                continue;
                            // to prevent there is null region with no data at all
                            if (region.loop.Count == 0)
                                continue;
                            // check if the shell at this level is enclosed by any loop of the parent level
                            // if true, generate MCR and switch the current shell's isShell attribute to false
                            if (Basic.IsPtInPoly(nestedRegion[chain[i]][0].loop[0], new Polyline(region.loop), false))
                            {
                                // mcr.Add(region.loop); // add the parent loop
                                if (region.innerLoops == null)
                                {
                                    region.innerLoops = new List<List<Point3d>>();
                                    region.innerMatchs = new List<List<string>>();
                                }
                                // add this shell loop to the parent region as innerLoops
                                region.innerLoops.Add(nestedRegion[chain[i]][0].loop);
                                region.innerMatchs.Add(nestedRegion[chain[i]][0].match);
                                //region.InitializeMCR();

                                foreach (gbRegion subRegion in nestedRegion[chain[i]])
                                {
                                    if (subRegion.isShell)
                                        continue;
                                    for (int j = 0; j < subRegion.match.Count; j++)
                                    {
                                        if (subRegion.match[j].Contains("Outside"))
                                        {
                                            Match match = Regex.Match(subRegion.match[j], pattern);
                                            string appendix = match.Groups[2].Value.Split('_')[1];
                                            subRegion.match[j] = region.label + "::Wall" + (region.innerLoops.Count - 1) + "_" + appendix;
                                        }
                                    }
                                }
                                nestedRegion[chain[i]][0].isShell = false; // or just delete it

                                string parentLabel = chain[i - 1].ToString() + ":" +
                                  nestedRegion[chain[i - 1]].IndexOf(region).ToString(); // which loop in which group
                                mcrParentLabel.Add(parentLabel);
                                //mcrs.Add(mcr);
                            }
                        }
                    }
                }
            }
            // DEBUG
            // foreach (string label in mcrParentLabel)
            // {
            //   Rhino.RhinoApp.WriteLine("MCR label: " + label);
            // }

            // me embed following belonging relations in the gbRegion.innerLoops

            // merge mcr for those share the same parent polyline
            // this creates mcr with multiple holes
            //for (int i = mcrs.Count - 1; i >= 0; i--)
            //{
            //    for (int j = i - 1; j >= 0; j--)
            //    {
            //        if (mcrParentLabel[j] == mcrParentLabel[i])
            //        {
            //            mcrs[i].RemoveAt(0);
            //            mcrs[j].AddRange(mcrs[i]);
            //            mcrs.RemoveAt(i);
            //        }
            //    }
            //}

            return;
        }

        /// <summary>
        /// ONLY used after the region detect function
        /// the input crvs must follows the right order (counter-clockwise)
        /// </summary>
        private static List<Point3d> SortPtLoop(List<Line> lines)
        {
            if (lines.Count == 0)
            {
                //Rhino.RhinoApp.WriteLine("NO CURVE AS INPUT");
                return new List<Point3d>();
            }
            List<Point3d> ptLoop = new List<Point3d>();

            // first define the start point
            // all curve in crvs are shuffled up with random directions
            // but the start point has to be the joint of the first and last curve
            Point3d startPt = new Point3d();
            if (
                (lines[0].PointAt(0) - lines.Last().PointAt(0)).Length < 0.0001 || 
                (lines[0].PointAt(0) - lines.Last().PointAt(1)).Length < 0.0001
                )
                startPt = lines[0].PointAt(0);
            else
                startPt = lines[0].PointAt(1);

            ptLoop.Add(startPt);

            for (int i = 0; i < lines.Count; i++)
                if ((lines[i].PointAt(0) - ptLoop[i]).Length < 0.0001)
                    ptLoop.Add(lines[i].PointAt(1));
                else
                    ptLoop.Add(lines[i].PointAt(0));

            return ptLoop;
        }

        private static bool IsSegJoined(Line a, Line b, double tol)
        {
            if (
                (a.PointAt(0) - b.PointAt(0)).Length < tol ||
                (a.PointAt(1) - b.PointAt(0)).Length < tol ||
                (a.PointAt(0) - b.PointAt(1)).Length < tol ||
                (a.PointAt(1) - b.PointAt(1)).Length < tol
                )
                return true;
            else return false;
        }

        /// <summary>
        /// Each half curve is stored with its reversed pair
        /// to get its pair you only need to find the opposite in the bundled two
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        private static int GetMatchIdx(int idx)
        {
            if (idx < 0)
                return -1;
            else if (idx % 2 == 0)
                return idx + 1;
            else
                return idx - 1;
        }

        private static int GetMatchFIdx(Dictionary<int, List<int>> FIdx, int idx, List<int> banned)
        {
            foreach (KeyValuePair<int, List<int>> kvp in FIdx)
            {
                if (banned.Contains(kvp.Key))
                    continue;
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    if (kvp.Value[i] == idx)
                        return kvp.Key;
                }
            }
            return -1;
        }

    }
}
