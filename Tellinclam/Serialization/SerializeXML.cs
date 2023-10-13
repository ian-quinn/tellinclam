using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

using Rhino.Geometry;
using Tellinclam;

namespace Tellinclam
{
    class SerializeXML
    {
        public static void Appendix(string XMLpath, string label, out List<List<Point3d>> loops)
        //    Dictionary<string, string> adjDict)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(gbXML));
            gbXML gbx;
            using (Stream reader = new FileStream(XMLpath, FileMode.Open, FileAccess.Read))
            {
                gbx = (gbXML)serializer.Deserialize(reader);
            }

            loops = new List<List<Point3d>>();
            if (label == "Column")
            {
                foreach (var item in gbx.Campus.Column)
                {
                    List<Point3d> loop = new List<Point3d>();
                    foreach (var cpt in item.PlanarGeometry.PolyLoop.Points)
                    {
                        Point3d pt = new Point3d(
                            double.Parse(cpt.Coordinate[0]),
                            double.Parse(cpt.Coordinate[1]),
                            double.Parse(cpt.Coordinate[2]));
                        loop.Add(pt);
                    }
                    loops.Add(loop);
                }
            }
            if (label == "Beam")
            {
                foreach (var item in gbx.Campus.Beam)
                {
                    List<Point3d> loop = new List<Point3d>();
                    foreach (var cpt in item.PlanarGeometry.PolyLoop.Points)
                    {
                        Point3d pt = new Point3d(
                            double.Parse(cpt.Coordinate[0]),
                            double.Parse(cpt.Coordinate[1]),
                            double.Parse(cpt.Coordinate[2]));
                        loop.Add(pt);
                    }
                    loops.Add(loop);
                }
            }
            if (label == "Shaft")
            {
                foreach (var item in gbx.Campus.Shaft)
                {
                    List<Point3d> loop = new List<Point3d>();
                    foreach (var cpt in item.PlanarGeometry.PolyLoop.Points)
                    {
                        Point3d pt = new Point3d(
                            double.Parse(cpt.Coordinate[0]),
                            double.Parse(cpt.Coordinate[1]),
                            double.Parse(cpt.Coordinate[2]));
                        loop.Add(pt);
                    }
                    loops.Add(loop);
                }
            }
        }

        public static void GetSpace(string XMLpath,
            out List<string> spaceIds,
            out List<List<List<Point3d>>> nestedSrfs,
            out List<List<List<Point3d>>> nestedOpenings)
        //    Dictionary<string, string> adjDict)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(gbXML));
            gbXML gbx;
            using (Stream reader = new FileStream(XMLpath, FileMode.Open, FileAccess.Read))
            {
                gbx = (gbXML)serializer.Deserialize(reader);
            }

            spaceIds = new List<string>();
            nestedSrfs = new List<List<List<Point3d>>>();
            nestedOpenings = new List<List<List<Point3d>>>();

            // in case the XML not valid
            if (gbx.Campus == null)
                return;
            if (gbx.Campus.Buildings == null)
                return;
            if (gbx.Campus.Buildings[0].Spaces == null)
                return;

            foreach (var space in gbx.Campus.Buildings[0].Spaces)
            {
                spaceIds.Add(space.id);
                nestedOpenings.Add(new List<List<Point3d>>());
                List<List<Point3d>> nestedSrf = new List<List<Point3d>>();
                foreach (var polyloop in space.ShellGeo.ClosedShell.PolyLoops)
                {
                    List<Point3d> loopPts = new List<Point3d>();
                    foreach (var cpt in polyloop.Points)
                    {
                        Point3d pt = new Point3d(
                            double.Parse(cpt.Coordinate[0]),
                            double.Parse(cpt.Coordinate[1]),
                            double.Parse(cpt.Coordinate[2]));
                        loopPts.Add(pt);
                    }
                    nestedSrf.Add(loopPts);
                }
                nestedSrfs.Add(nestedSrf);
            }


            foreach (var srf in gbx.Campus.Surface)
            {
                if (srf.Opening == null)
                    continue;

                List<List<Point3d>> openingOnSrf = new List<List<Point3d>>();
                foreach (var aperture in srf.Opening)
                {
                    if (aperture.pg == null)
                        continue;
                    if (aperture.pg.PolyLoop == null)
                        continue;

                    List<Point3d> openingPts = new List<Point3d>();
                    foreach (var cpt in aperture.pg.PolyLoop.Points)
                    {
                        Point3d pt = new Point3d(
                            double.Parse(cpt.Coordinate[0]),
                            double.Parse(cpt.Coordinate[1]),
                            double.Parse(cpt.Coordinate[2]));
                        openingPts.Add(pt);
                    }
                    openingOnSrf.Add(openingPts);
                }
                //List<string> adjSpaceIds = new List<string>();
                //foreach (var adjSpaceId in srf.AdjacentSpaceId)
                //{
                //    adjSpaceIds.Add(adjSpaceId.spaceIdRef);
                //}
                if (srf.AdjacentSpaceId == null)
                    continue;

                for (int i = 0; i < spaceIds.Count; i++)
                {
                    if (spaceIds[i] == srf.AdjacentSpaceId[0].spaceIdRef)
                    {
                        nestedOpenings[i] = openingOnSrf;
                    }
                    //if (spaceIds[i] == adjSpaceIds[0])
                }
            }

            //foreach (var item in gbx.Campus.Surface)
            //{
            //    List<Point3d> loop = new List<Point3d>();
            //    foreach (var cpt in item.PlanarGeometry.PolyLoop.Points)
            //    {
            //        Point3d pt = new Point3d(
            //            double.Parse(cpt.Coordinate[0]),
            //            double.Parse(cpt.Coordinate[1]),
            //            double.Parse(cpt.Coordinate[2]));
            //        loop.Add(pt);
            //    }
            //    foreach (var adjSpace in item.AdjacentSpaceId)
            //    {
            //        int spaceIndex = spaceIds.IndexOf(adjSpace.spaceIdRef);
            //        if (spaceIndex != -1)
            //        {
            //            nestedSrfs[spaceIndex].Add(loop);
            //        }
            //    }
            //}
        }

        #region geometric info translate
        public static CartesianPoint PtToCartesianPoint(Point3d pt)
        {
            CartesianPoint cpt = new CartesianPoint();
            cpt.Coordinate = new string[3];
            CultureInfo ci = new CultureInfo(String.Empty);
            string xformat = string.Format(ci, "{0:0.000000}", pt.X);
            string yformat = string.Format(ci, "{0:0.000000}", pt.Y);
            string zformat = string.Format(ci, "{0:0.000000}", pt.Z);
            cpt.Coordinate[0] = xformat;
            cpt.Coordinate[1] = yformat;
            cpt.Coordinate[2] = zformat;
            return cpt;
        }

        // note that all polyloops are not enclosed
        // also the input ptsLoop here is not closed
        public static PolyLoop PtsToPolyLoop(List<Point3d> ptsLoop)
        {
            PolyLoop pl = new PolyLoop();
            pl.Points = new CartesianPoint[ptsLoop.Count];
            for (int i = 0; i < ptsLoop.Count; i++)
            {
                CartesianPoint cpt = PtToCartesianPoint(ptsLoop[i]);
                pl.Points[i] = cpt;
            }
            return pl;
        }
        #endregion
    }
}
