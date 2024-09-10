using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

using Rhino.Geometry;
using Tellinclam.XML;

namespace Tellinclam
{
    class SerializeXML
    {
        public static void Appendix(string XMLpath, string label, out List<List<Point3d>> loops)
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
            out List<List<Polyline>> nestedSrfs,
            out List<List<Polyline>> nestedOpenings)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(gbXML));
            gbXML gbx;
            using (Stream reader = new FileStream(XMLpath, FileMode.Open, FileAccess.Read))
            {
                gbx = (gbXML)serializer.Deserialize(reader);
            }

            spaceIds = new List<string>();
            nestedSrfs = new List<List<Polyline>>();
            nestedOpenings = new List<List<Polyline>>();

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
                nestedOpenings.Add(new List<Polyline>());
                List<Polyline> nestedSrf = new List<Polyline>();
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
                    loopPts.Add(loopPts[0]);
                    nestedSrf.Add(new Polyline(loopPts));
                }
                nestedSrfs.Add(nestedSrf);
            }


            foreach (var srf in gbx.Campus.Surface)
            {
                if (srf.Opening == null)
                    continue;

                List<Polyline> openingOnSrf = new List<Polyline>();
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
                    openingPts.Add(openingPts[0]);
                    openingOnSrf.Add(new Polyline(openingPts));
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

        public static bool GetSurface(string XMLpath, string surfaceId,
            out Polyline boundary, out Tuple<string, string> adjSpace)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(gbXML));
            gbXML gbx;
            using (Stream reader = new FileStream(XMLpath, FileMode.Open, FileAccess.Read))
            {
                gbx = (gbXML)serializer.Deserialize(reader);
            }

            boundary = new Polyline(new Point3d[0]);
            adjSpace = new Tuple<string, string>("", "");

            // in case the XML not valid
            if (gbx.Campus == null)
                return false;
            if (gbx.Campus.Buildings == null)
                return false;

            foreach (var srf in gbx.Campus.Surface)
            {
                if (srf.id == surfaceId || srf.id.ToLower() == surfaceId.ToLower())
                {
                    List<Point3d> srfPts = new List<Point3d>();
                    foreach (var cpt in srf.PlanarGeometry.PolyLoop.Points)
                    {
                        Point3d pt = new Point3d(
                            double.Parse(cpt.Coordinate[0]),
                            double.Parse(cpt.Coordinate[1]),
                            double.Parse(cpt.Coordinate[2]));
                        srfPts.Add(pt);
                    }
                    srfPts.Add(srfPts[0]);
                    boundary = new Polyline(srfPts);

                    if (srf.AdjacentSpaceId.Length == 1)
                        adjSpace = Tuple.Create(srf.AdjacentSpaceId[0].spaceIdRef, "");
                    if (srf.AdjacentSpaceId.Length == 2)
                        adjSpace = Tuple.Create(srf.AdjacentSpaceId[0].spaceIdRef, srf.AdjacentSpaceId[1].spaceIdRef);
                    // only return the first qualified surface
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// especially for output gbXML of Gingerbread 
        /// </summary>
        /// <param name="XMLpath"></param>
        /// <param name="columns"></param>
        /// <param name="beams"></param>
        public static void GetColBeam(string XMLpath,
        out List<Brep> columns,
        out List<Brep> beams)
        //    Dictionary<string, string> adjDict)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(gbXML));
            gbXML gbx;
            using (Stream reader = new FileStream(XMLpath, FileMode.Open, FileAccess.Read))
            {
                gbx = (gbXML)serializer.Deserialize(reader);
            }

            columns = new List<Brep>();
            beams = new List<Brep>();

            // in case the XML not valid
            if (gbx.Campus == null)
                return;
            if (gbx.Campus.Column == null)
                return;
            if (gbx.Campus.Beam == null)
                return;

            foreach (var col in gbx.Campus.Column)
            {
                if (col.PlanarGeometry == null)
                    continue;
                if (col.PlanarGeometry.PolyLoop == null)
                    continue;

                List<Point3d> colPts = new List<Point3d>();
                foreach (var cpt in col.PlanarGeometry.PolyLoop.Points)
                {
                    Point3d pt = new Point3d(
                        double.Parse(cpt.Coordinate[0]),
                        double.Parse(cpt.Coordinate[1]),
                        double.Parse(cpt.Coordinate[2]));
                    colPts.Add(pt);
                }
                colPts.Add(colPts[0]);
                PolylineCurve ply = new PolylineCurve(colPts);

                LineCurve ax = new LineCurve(
                    new Point3d(
                        double.Parse(col.Axis.Points[0].Coordinate[0]),
                        double.Parse(col.Axis.Points[0].Coordinate[1]),
                        double.Parse(col.Axis.Points[0].Coordinate[2])),
                    new Point3d(
                        double.Parse(col.Axis.Points[1].Coordinate[0]),
                        double.Parse(col.Axis.Points[1].Coordinate[1]),
                        double.Parse(col.Axis.Points[1].Coordinate[2]))
                    ); ;

                SweepOneRail railSweep = new SweepOneRail();
                var breps = railSweep.PerformSweep(ax, ply);

                columns.AddRange(breps);
            }

            foreach (var beam in gbx.Campus.Beam)
            {
                if (beam.PlanarGeometry == null)
                    continue;
                if (beam.PlanarGeometry.PolyLoop == null)
                    continue;
                if (beam.Axis == null)
                    continue;

                List<Point3d> beamPts = new List<Point3d>();
                foreach (var cpt in beam.PlanarGeometry.PolyLoop.Points)
                {
                    Point3d pt = new Point3d(
                        double.Parse(cpt.Coordinate[0]),
                        double.Parse(cpt.Coordinate[1]),
                        double.Parse(cpt.Coordinate[2]));
                    beamPts.Add(pt);
                }
                beamPts.Add(beamPts[0]);
                PolylineCurve ply = new PolylineCurve(beamPts);

                LineCurve ax = new LineCurve(
                    new Point3d(
                        double.Parse(beam.Axis.Points[0].Coordinate[0]),
                        double.Parse(beam.Axis.Points[0].Coordinate[1]),
                        double.Parse(beam.Axis.Points[0].Coordinate[2])),
                    new Point3d(
                        double.Parse(beam.Axis.Points[1].Coordinate[0]),
                        double.Parse(beam.Axis.Points[1].Coordinate[1]),
                        double.Parse(beam.Axis.Points[1].Coordinate[2]))
                    ); ;

                SweepOneRail railSweep = new SweepOneRail();
                var breps = railSweep.PerformSweep(ax, ply);

                beams.AddRange(breps);
            }
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
