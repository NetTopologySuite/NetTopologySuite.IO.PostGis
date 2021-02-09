using System;
using System.Configuration;

using NetTopologySuite.Geometries;

using Npgsql;
using NpgsqlTypes;

using NUnit.Framework;

namespace NetTopologySuite.IO.PostGis.Test
{
    [TestFixture]
    [Category("Database.IO")]
    public class PostgisFixture : AbstractIOFixture
    {
        protected override void AddAppConfigSpecificItems(KeyValueConfigurationCollection kvcc)
        {
            // NOTE: insert a valid connection string to a postgis db
            if (kvcc["PostGisConnectionString"] == null)
            {
                string server = TestContext.Parameters.Get("server", "localhost");
                string port = TestContext.Parameters.Get("port", "5432");
                string database = TestContext.Parameters.Get("database", "postgis");
                string user = TestContext.Parameters.Get("user", "postgres");
                string pwd = TestContext.Parameters.Get("password", "1.Kennwort!");
                kvcc.Add("PostGisConnectionString", $"Server={server};Port={port};Database={database};user id={user};Password={pwd}");
            }
        }

        protected override void ReadAppConfigInternal(KeyValueConfigurationCollection kvcc)
        {
            ConnectionString = kvcc["PostGisConnectionString"].Value;
        }

        private string PostGisVersion()
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT postgis_version();";
                    object res = cmd.ExecuteScalar();
                    return res.ToString();
                }
            }
        }

        protected override void CreateTestStore()
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    string version = PostGisVersion();
                    if (version.StartsWith("1."))
                    {
                        cmd.CommandText = "DELETE FROM \"geometry_columns\" WHERE \"f_table_name\" = 'nts_io_postgis_2d'; ";
                        cmd.ExecuteNonQuery();
                    }

                    cmd.CommandText = "DROP TABLE IF EXISTS \"nts_io_postgis_2d\";";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText =
                        "CREATE TABLE \"nts_io_postgis_2d\" (id int primary key, wkt text);"
                      + "SELECT AddGeometryColumn('nts_io_postgis_2d', 'the_geom', " + 4326 + ",'GEOMETRY', 2);"
                        ;
                    cmd.ExecuteNonQuery();
                }
            }
            RandomGeometryHelper.Ordinates = Ordinates.XY;
        }

        protected override Geometry Read(byte[] b)
        {
            var pgReader = new PostGisReader(RandomGeometryHelper.Factory);
            return pgReader.Read(b);
        }

        protected override byte[] Write(Geometry gIn)
        {
            var pgWriter = new PostGisWriter();
            byte[] b = pgWriter.Write(gIn);
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO \"nts_io_postgis_2d\" VALUES(@P1, @P2, @P3);";
                    var p1 = new NpgsqlParameter("P1", NpgsqlDbType.Integer) { NpgsqlValue = this.Counter };
                    var p2 = new NpgsqlParameter("P2", NpgsqlDbType.Text) { NpgsqlValue = gIn.AsText() };
                    var p3 = new NpgsqlParameter("P3", NpgsqlDbType.Bytea) { NpgsqlValue = b };
                    cmd.Parameters.AddRange(new[] { p1, p2, p3 });
                    cmd.ExecuteNonQuery();
                }
            }

            return b;
        }

        [Test, Explicit]
        public void TestInt64PostGisNaN()
        {
            byte[] buffer = new byte[] {0, 0, 0, 0, 0, 0, 248, 127};
            TestContext.Write(BitConverter.ToInt64(buffer));
            
        }

        [TestCase("SRID=4326;POINT (10 11)")]
        [TestCase("SRID=1;POINT (10 11)")]
        [TestCase("SRID=0;POINT (10 11)")]
        [TestCase("SRID=-1;POINT (10 11)")]
        [TestCase("POINT EMPTY")]
        [TestCase("POINT (10 11)")]
        [TestCase("POINT Z (10 11 12)")]
        [TestCase("POINT M (10 11 13)")]
        [TestCase("POINT ZM (10 11 12 13)")]
        [TestCase("LINESTRING EMPTY")]
        [TestCase("POLYGON EMPTY")]
        [TestCase("POLYGON Z EMPTY")]
        [TestCase("POLYGON M EMPTY")]
        [TestCase("POLYGON ZM EMPTY")]
        [TestCase("GEOMETRYCOLLECTION M(POINT M(10 11 13), LINESTRING M(10 11 13, 20 21 23), POLYGON M EMPTY)")]
        [TestCase("SRID=25832;MULTIPOINT Z((10 11 13), (20 21 23), EMPTY, (30 31 33))")]
        [TestCase("SRID=31466;MULTILINESTRING Z((10 11 12, 20 21 22), EMPTY, (30 31 32, 20 21 22))")]
        [TestCase("POLYGON ZM((10 10 0 7,20 10 0 7,20 20 0 7,20 10 0 7,10 10 0 7),(5 5 0 7,5 6 0 7,6 6 0 7,6 5 0 7,5 5 0 7))")]
        public void TestByEWkt(string ewkt, string ignoreReason = null)
        {
            // Ignore?
            if (!string.IsNullOrEmpty(ignoreReason))
                Assert.Ignore(ignoreReason);

            // Arrange
            var pgr = new PostGisReader(NtsGeometryServices.Instance.CreateGeometryFactory());
            var pgw = new PostGisWriter { HandleOrdinates = Ordinates.XYZM };
            
            byte[] postgisBuffer = null;
            try
            {

                using (var cn = new NpgsqlConnection(ConnectionString))
                {
                    cn.Open();
                    var cm = cn.CreateCommand();
                    cm.CommandText = "SELECT ST_AsEWKB(ST_GeomFromText(@P0));";
                    var p = cm.Parameters.Add("P0", NpgsqlDbType.Varchar);
                    p.Value = ewkt;

                    postgisBuffer = (byte[]) cm.ExecuteScalar();
                }
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            // Reader
            Geometry geom = null;
            Assert.That(() => geom = pgr.Read(postgisBuffer), Throws.Nothing);
            Assert.That(geom, Is.Not.Null);

            // Writer
            byte[] postgisWriterBuffer = null;
            Assert.That(() => postgisWriterBuffer = pgw.Write(geom), Throws.Nothing);
            Assert.That(postgisWriterBuffer, Is.Not.Null);

            // Equality
            Assert.That(postgisWriterBuffer, Is.EqualTo(postgisBuffer));
        }
    }
}
