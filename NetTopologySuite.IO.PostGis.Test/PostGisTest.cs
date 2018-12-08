﻿using GeoAPI.Geometries;
using GeoAPI.IO;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetTopologySuite.IO.PostGis.Test
{

    /// <summary>
    /// Ported from PostGIS:
    /// http://svn.refractions.net/postgis/trunk/java/jdbc/src/examples/TestParser.java
    /// </summary>
    [TestFixture]
    public class PostGisTest
    {
        // Our set of geometries to test.
        public static string[] testset = new string[]
        {
		        // 2D
				"POINT(10 10)",
				// 3D with 3rd coordinate set to 0
				"POINT(10 10 0)",
				// 3D
				"POINT(10 10 20)",
				// 3D with scientific notation
//				"POINT(1e100 1.2345e-100 -2e-5)",
				// 2D + Measures
//				"POINTM(10 10 20)",
				// 3D + Measures
//				"POINT(10 10 20 30)",
				// broken format, see http://lists.jump-project.org/pipermail/jts-devel/2006-April/001572.html
				"MULTIPOINT(11 12, 20 20)",
				// broken format
				"MULTIPOINT(11 12 13, 20 20 20)",
				// broken format
//				"MULTIPOINTM(11 12 13, 20 20 20)",
				// broken format
//				"MULTIPOINT(11 12 13 14,20 20 20 20)",
				// OGC conforming format
//TODO:			"MULTIPOINT((11 12), (20 20))",
//TODO:			"MULTIPOINT((11 12 13), (20 20 20))",
//				"MULTIPOINTM((11 12 13), (20 20 20))",
//				"MULTIPOINT((11 12 13 14),(20 20 20 20))",            
				"LINESTRING(10 10,20 20,50 50,34 34)",
                "LINESTRING(10 10 20,20 20 20,50 50 50,34 34 34)",
//				"LINESTRINGM(10 10 20,20 20 20,50 50 50,34 34 34)",
//				"LINESTRING(10 10 20 20,20 20 20 20,50 50 50 50,34 34 34 50)",
				"POLYGON((10 10,20 10,20 20,20 10,10 10),(5 5,5 6,6 6,6 5,5 5))",
                "POLYGON((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))",
//				"POLYGONM((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))",
//				"POLYGON((10 10 0 7,20 10 0 7,20 20 0 7,20 10 0 7,10 10 0 7),(5 5 0 7,5 6 0 7,6 6 0 7,6 5 0 7,5 5 0 7))",
				"MULTIPOLYGON(((10 10,20 10,20 20,20 10,10 10),(5 5,5 6,6 6,6 5,5 5)),((10 10,20 10,20 20,20 10,10 10),(5 5,5 6,6 6,6 5,5 5)))",
                "MULTIPOLYGON(((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)))",
//				"MULTIPOLYGONM(((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)))",
//				"MULTIPOLYGON(((10 10 0 7,20 10 0 7,20 20 0 7,20 10 0 7,10 10 0 7),(5 5 0 7,5 6 0 7,6 6 0 7,6 5 0 7,5 5 0 7)),((10 10 0 7,20 10 0 7,20 20 0 7,20 10 0 7,10 10 0 7),(5 5 0 7,5 6 0 7,6 6 0 7,6 5 0 7,5 5 0 7)))",
				"MULTILINESTRING((10 10,20 10,20 20,20 10,10 10),(5 5,5 6,6 6,6 5,5 5))",
                "MULTILINESTRING((10 10 5,20 10 5,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))",
//				"MULTILINESTRINGM((10 10 7,20 10 7,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))",
//				"MULTILINESTRING((10 10 0 7,20 10 0 7,20 20 0 7,20 10 0 7,10 10 0 7),(5 5 0 7,5 6 0 7,6 6 0 7,6 5 0 7,5 5 0 7))",
				"GEOMETRYCOLLECTION(POINT(10 10),POINT(20 20))",
                "GEOMETRYCOLLECTION(POINT(10 10 20),POINT(20 20 20))",
//				"GEOMETRYCOLLECTIONM(POINT(10 10 20),POINT(20 20 20))",
//				"GEOMETRYCOLLECTION(POINT(10 10 20 7),POINT(20 20 20 7))",
				"GEOMETRYCOLLECTION(LINESTRING(10 10 20,20 20 20, 50 50 50, 34 34 34),LINESTRING(10 10 20,20 20 20, 50 50 50, 34 34 34))",
                "GEOMETRYCOLLECTION(POLYGON((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),POLYGON((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)))",
				// Cannot be parsed by 0.X servers, broken format
				"GEOMETRYCOLLECTION(MULTIPOINT(10 10 10, 20 20 20),MULTIPOINT(10 10 10, 20 20 20))",
				// Cannot be parsed by 0.X servers, OGC conformant
//TODO:			"GEOMETRYCOLLECTION(MULTIPOINT((10 10 10), (20 20 20)),MULTIPOINT((10 10 10), (20 20 20)))",
				 // PostGIs 0.X "flattens" this geometry, so it is not
				// equal after reparsing.
				"GEOMETRYCOLLECTION(MULTILINESTRING((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)))",
				// PostGIs 0.X "flattens" this geometry, so it is not equal
				// after reparsing.
				"GEOMETRYCOLLECTION(MULTIPOLYGON(((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))),MULTIPOLYGON(((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))))",
                "GEOMETRYCOLLECTION(POINT(10 10 20),LINESTRING(10 10 20,20 20 20, 50 50 50, 34 34 34),POLYGON((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)))",
				// Collections that contain both X and MultiX do not work on
				// PostGIS 0.x, broken format
				"GEOMETRYCOLLECTION(POINT(10 10 20),MULTIPOINT(10 10 10, 20 20 20),LINESTRING(10 10 20,20 20 20, 50 50 50, 34 34 34),POLYGON((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),MULTIPOLYGON(((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))),MULTILINESTRING((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)))",
				// Collections that contain both X and MultiX do not work on
				// PostGIS 0.x, OGC conformant
//TODO:			"GEOMETRYCOLLECTION(POINT(10 10 20),MULTIPOINT((10 10 10), (20 20 20)),LINESTRING(10 10 20,20 20 20, 50 50 50, 34 34 34),POLYGON((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),MULTIPOLYGON(((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)),((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))),MULTILINESTRING((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0)))",
				// Old (bad) PostGIS 0.X Representation
//				"GEOMETRYCOLLECTION(EMPTY)",
				// new (correct) representation
				"GEOMETRYCOLLECTION EMPTY",
				// new (correct) representation - does not work on 0.X
//TODO:			"POINT EMPTY",
				// new (correct) representation - does not work on 0.X
				"LINESTRING EMPTY",
				// new (correct) representation - does not work on 0.X
				"POLYGON EMPTY",
				// new (correct) representation - does not work on 0.X
				"MULTIPOINT EMPTY",
				// new (correct) representation - does not work on 0.X
				"MULTILINESTRING EMPTY",
				// new (correct) representation - does not work on 0.X
				"MULTIPOLYGON EMPTY",
			// end
		};

        // The srid we use for the srid tests
        public static int SRID = 4326;

        private static readonly PostGisReader br = new PostGisReader();
        private static readonly WKTReader wr = new WKTReader();

        [OneTimeSetUp]
        public void TestFixtureSetup()
        {
            GeoAPI.GeometryServiceProvider.Instance = NtsGeometryServices.Instance;
        }

        /// <summary>
        /// 
        /// </summary>
		[Test]
        public void General()
        {
            for (var i = 0; i < testset.Length; i++)
            {
                General(testset[i], -1);
                General(testset[i], SRID);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wkt"></param>
        /// <param name="srid"></param>
		private static void General(string wkt, int srid)
        {
            var geom = wr.Read(wkt);
            var parsed = geom.AsText();
            var regeom = wr.Read(parsed);
            var reparsed = regeom.AsText();

            geom.SRID = srid;
            regeom.SRID = srid;

            Assert.IsTrue(geom.EqualsExact(regeom));
            Assert.AreEqual(parsed, reparsed);

            var bytesB = new PostGisWriter(ByteOrder.BigEndian).Write(regeom);
            var regeom2 = br.Read(bytesB);
            Assert.IsTrue(geom.EqualsExact(regeom2));

            byte[] bytesL = new PostGisWriter(ByteOrder.LittleEndian).Write(regeom);
            var regeom3 = br.Read(bytesL);
            Assert.IsTrue(geom.EqualsExact(regeom3));
            Assert.IsTrue(regeom2.EqualsExact(regeom3));


            Assert.AreEqual(bytesB.Length, bytesL.Length);
        }

        [Test]
        public void Point3D()
        {
            // Warm up assertions:
            IPoint point2D = new Point(1, 2);
            Assert.IsTrue(Double.IsNaN(point2D.Z));

            IPoint point3D = new Point(1, 2, 3);
            Assert.IsFalse(Double.IsNaN(point3D.Z));

            // The real thing:
            IPoint source = new Point(123, 456, 789);
            var pgWriter = new PostGisWriter { HandleOrdinates = Ordinates.XYZ };
            byte[] bytes = pgWriter.Write(source);
            IPoint target = (IPoint)new PostGisReader().Read(bytes);
            Assert.AreEqual(source.X, target.X);
            Assert.AreEqual(source.Y, target.Y);
            Assert.AreEqual(source.Z, target.Z);
        }

        [Test]
        public void LineString3D()
        {
            const int size = 10;
            Coordinate[] points = new Coordinate[size];
            for (int i = 0; i < size; i++)
            {
                // just some arbitrary values
                points[i] = new Coordinate(100 * Math.Sin(i), 200 * Math.Cos(i), 300 * Math.Tan(i));
            }
            ILineString source = new LineString(points);
            var pgWriter = new PostGisWriter() { HandleOrdinates = Ordinates.XYZ };
            byte[] bytes = pgWriter.Write(source);
            ILineString target = (ILineString)new PostGisReader().Read(bytes);
            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual(source.Coordinates[i].X, target.Coordinates[i].X);
                Assert.AreEqual(source.Coordinates[i].Y, target.Coordinates[i].Y);
                Assert.AreEqual(source.Coordinates[i].Z, target.Coordinates[i].Z);
            }
        }

        [Test]
        public void GeometryCollection_empty()
        {
            IGeometryCollection source = GeometryCollection.Empty;

            // don't assign ordinates, needs to call `CheckOrdinates(...)`
            byte[] bytes = new PostGisWriter().Write(source);

            IGeometryCollection target = (IGeometryCollection)new PostGisReader().Read(bytes);

            Assert.AreEqual(source.Count, target.Count);
        }
    }
}
