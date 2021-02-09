using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using Perfolizer.Horology;

namespace NetTopologySuite.IO.PostGis.Benchmarks
{
    public class Roundtrip
    {
        private static readonly PostGisReader br1 = new PostGisReader();
        private static readonly PostGisReader br2 = new PostGisReader(new PackedCoordinateSequenceFactory(), new PrecisionModel());
        private static readonly PostGisWriter bw1 = new PostGisWriter();
        private static readonly WKTReader wr = new WKTReader();

        private static byte[] pg1;

        public Roundtrip()
        {
            var geom = wr.Read("POLYGON((10 10 0,20 10 0,20 20 0,20 10 0,10 10 0),(5 5 0,5 6 0,6 6 0,6 5 0,5 5 0))");
            pg1 = new PostGisWriter().Write(geom);
        }

        [Benchmark]
        public Geometry RoundtripDefault()
        {
            var g = br1.Read(pg1);
            var pgtmp = bw1.Write(g);
            return br1.Read(pgtmp);
        }

        [Benchmark]
        public Geometry RoundtripPackedCoordinateSequenceFactory()
        {
            var g = br2.Read(pg1);
            var pgtmp = bw1.Write(g);
            return br2.Read(pgtmp);
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summaryStyle = new BenchmarkDotNet.Reports.SummaryStyle(null, false, SizeUnit.B, TimeUnit.Microsecond);
            var config = DefaultConfig.Instance.WithSummaryStyle(summaryStyle);
            config.AddJob(Job.Default
               .WithArguments(new[] { new MsBuildArgument("/p:GenerateProgramFile=false") }).AsDefault());
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}