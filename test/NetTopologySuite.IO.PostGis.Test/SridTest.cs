using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NUnit.Framework;

namespace NetTopologySuite.IO.PostGis.Test
{
    [TestFixture]
    public class SridTest
    {
        [TestCase("POINT(1 2)")]
        [TestCase("POLYGON EMPTY")]
        [TestCase("MULTIPOINT((1 2), (3 4))")]
        [TestCase("MULTILINESTRING((0 0, 1 1), (2 2, 3 3))")]
        [TestCase("MULTIPOLYGON(((0 0, 0 1, 1 1, 1 0, 0 0)), ((2 2, 2 3, 3 3, 3 2, 2 2)))")]
        [TestCase("GEOMETRYCOLLECTION(POINT(1 2), MULTIPOLYGON(((0 0, 0 1, 1 1, 1 0, 0 0))))")]
        public void Srid_is_kept_for_geometry_and_its_parts(string wkt)
        {
            var source = new WKTReader(new GeometryFactory(new PrecisionModel(), 4326)).Read(wkt);
            byte[] bytes = new PostGisWriter().Write(source);

            var target = new PostGisReader().Read(bytes);

            foreach (var geometry in Flatten(target))
            {
                Assert.That(geometry.SRID, Is.EqualTo(4326), geometry.GeometryType);
                Assert.That(geometry.Factory.SRID, Is.EqualTo(4326), geometry.GeometryType);
            }
        }

        [Test]
        public void Shared_reader_keeps_srid_under_concurrent_reads()
        {
            // A shared reader, as in Npgsql. Points and parts of multi-geometries
            // without an SRID must not leak into geometries that have one.
            var factory = new GeometryFactory(new PrecisionModel(), 3006);
            var writer = new PostGisWriter();
            byte[] withSrid = writer.Write(factory.CreatePoint(new Coordinate(1, 2)));
            byte[] withoutSrid = writer.Write(new Point(1, 2));
            byte[] multiPolygon = writer.Write(new WKTReader(factory).Read(
                "MULTIPOLYGON(((0 0, 0 1, 1 1, 1 0, 0 0)), ((2 2, 2 3, 3 3, 3 2, 2 2)))"));

            var reader = new PostGisReader();
            const int threads = 8;
            const int iterations = 20000;
            int bad = 0;
            using (var start = new Barrier(threads))
            {
                var tasks = new Task[threads];
                for (int t = 0; t < threads; t++)
                {
                    int id = t;
                    tasks[t] = Task.Factory.StartNew(() =>
                    {
                        start.SignalAndWait();
                        for (int i = 0; i < iterations; i++)
                        {
                            if (id % 2 == 0)
                            {
                                if (reader.Read(withSrid).SRID != 3006)
                                    Interlocked.Increment(ref bad);
                            }
                            else
                            {
                                reader.Read(withoutSrid);
                                reader.Read(multiPolygon);
                            }
                        }
                    }, TaskCreationOptions.LongRunning);
                }

                Task.WaitAll(tasks);
            }

            Assert.That(bad, Is.Zero, $"{bad} of {threads / 2 * iterations} reads returned the wrong SRID");
        }

        private static IEnumerable<Geometry> Flatten(Geometry geometry)
        {
            yield return geometry;
            if (geometry is GeometryCollection collection)
            {
                foreach (var part in collection.Geometries)
                {
                    foreach (var child in Flatten(part))
                    {
                        yield return child;
                    }
                }
            }
        }
    }
}
