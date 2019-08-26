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
                kvcc.Add("PostGisConnectionString", "Host=ivv-t3s.ivv-aachen.de;Port=5432;Database=obe;Integrated Security=true;");
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
    }
}
