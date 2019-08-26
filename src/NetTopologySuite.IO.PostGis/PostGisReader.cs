// Ported from PostGIS:
// http://svn.refractions.net/postgis/trunk/java/jdbc/src/org/postgis/binary/BinaryParser.java

using System;
using System.IO;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Converts a PostGIS binary data to a <c>Geometry</c>.
    /// </summary>
    public class PostGisReader
    {
        private readonly PrecisionModel _precisionModel;
        private readonly CoordinateSequenceFactory _coordinateSequenceFactory;

        private Ordinates _handleOrdinates;

        // cache the last GeometryFactory used so that we only have to create a new one when we read
        // a SRID that's different from the last SRID that we read.
        private GeometryFactory _factory;

        /// <summary>
        /// Initialize reader with a standard settings.
        /// </summary>
        public PostGisReader()
            : this(NtsGeometryServices.Instance.DefaultCoordinateSequenceFactory,
                   NtsGeometryServices.Instance.DefaultPrecisionModel,
                   Ordinates.None)
        {
        }

        /// <summary>
        /// Initialize reader with the given <c>GeometryFactory</c>.
        /// </summary>
        /// <param name="factory"></param>
        public PostGisReader(GeometryFactory factory)
            : this(factory.CoordinateSequenceFactory, factory.PrecisionModel, Ordinates.None)
        {
        }

        /// <summary>
        /// Initialize reader with the given coordinate sequence factory and the given precision model.
        /// </summary>
        /// <param name="coordinateSequenceFactory"></param>
        /// <param name="precisionModel"> </param>
        public PostGisReader(CoordinateSequenceFactory coordinateSequenceFactory, PrecisionModel precisionModel)
            : this(coordinateSequenceFactory, precisionModel, Ordinates.None)
        {
        }

        /// <summary>
        /// Initialize reader with the given <c>GeometryFactory</c>.
        /// </summary>
        /// <param name="coordinateSequenceFactory"></param>
        /// <param name="precisionModel"> </param>
        /// <param name="handleOrdinates">The ordinates to handle</param>
        public PostGisReader(CoordinateSequenceFactory coordinateSequenceFactory, PrecisionModel precisionModel, Ordinates handleOrdinates)
        {
            _coordinateSequenceFactory = coordinateSequenceFactory;
            _precisionModel = precisionModel;

            HandleOrdinates = handleOrdinates;
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to modify rings after reading them to
        /// close them if they are not already closed.
        /// </summary>
        public bool RepairRings { get; set; }

        /// <summary>
        /// Gets the <see cref="Ordinates"/> that this reader can possibly deal with.
        /// </summary>
        public Ordinates AllowedOrdinates
        {
            get { return _coordinateSequenceFactory.Ordinates & Ordinates.XYZM; }
        }

        /// <summary>
        /// Gets an <see cref="Ordinates"/> mask indicating which ordinates present in the data
        /// should be transferred to the geometries that we read, or <see cref="Ordinates.None"/> if
        /// all ordinates present in the data should be transferred.
        /// </summary>
        public Ordinates HandleOrdinates
        {
            get { return _handleOrdinates; }
            set
            {
                _handleOrdinates = value == Ordinates.None
                    ? Ordinates.None
                    : (value | Ordinates.XY) & AllowedOrdinates;
            }
        }

        /// <summary>
        /// Reads a <see cref="Geometry"/> from a byte array.
        /// </summary>
        /// <param name="data">The byte array to read from.</param>
        /// <returns>The <see cref="Geometry"/> that was read.</returns>
        public Geometry Read(byte[] data)
        {
            using (Stream stream = new MemoryStream(data))
            {
                return Read(stream);
            }
        }

        /// <summary>
        /// Reads a <see cref="Geometry"/> from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read from.</param>
        /// <returns>The <see cref="Geometry"/> that was read.</returns>
        public Geometry Read(Stream stream)
        {
            using (var reader = new BiEndianBinaryReader(stream))
            {
                return Read(reader);
            }
        }

        /// <summary>
        /// Reads a geometry using the provided <see cref="BinaryReader"/>
        /// </summary>
        /// <param name="reader">The reader to use</param>
        /// <returns>A geometry</returns>
        protected Geometry Read(BinaryReader reader)
        {
            if (!(reader is BiEndianBinaryReader biEndianReader))
            {
                throw new ArgumentException("Reader must be BiEndianBinaryReader", nameof(reader));
            }

            biEndianReader.Endianess = (ByteOrder)reader.ReadByte();

            int typeword = reader.ReadInt32();

            // cut off high flag bits
            var geometryType = (PostGisGeometryType)(typeword & 0x1FFFFFFF);

            var receivedOrdinates = Ordinates.XY;
            if ((typeword & 0x80000000) != 0)
            {
                receivedOrdinates |= Ordinates.Z;
            }

            if ((typeword & 0x40000000) != 0)
            {
                receivedOrdinates |= Ordinates.M;
            }

            int srid = (typeword & 0x20000000) != 0 ? reader.ReadInt32() : - 1;

            if (_factory?.SRID != srid)
            {
                _factory = NtsGeometryServices.Instance.CreateGeometryFactory(_precisionModel, srid, _coordinateSequenceFactory);
            }

            var factory = _factory;

            Geometry result;
            switch (geometryType)
            {
                case PostGisGeometryType.Point:
                    result = ReadPoint(reader, factory, receivedOrdinates);
                    break;
                case PostGisGeometryType.LineString:
                    result = ReadLineString(reader, factory, receivedOrdinates);
                    break;
                case PostGisGeometryType.Polygon:
                    result = ReadPolygon(reader, factory, receivedOrdinates);
                    break;
                case PostGisGeometryType.MultiPoint:
                    result = ReadMultiPoint(reader, factory);
                    break;
                case PostGisGeometryType.MultiLineString:
                    result = ReadMultiLineString(reader, factory);
                    break;
                case PostGisGeometryType.MultiPolygon:
                    result = ReadMultPolygon(reader, factory);
                    break;
                case PostGisGeometryType.GeometryCollection:
                    result = ReadGeometryCollection(reader, factory);
                    break;
                default:
                    throw new ArgumentException("Geometry type not recognized. GeometryCode: " + geometryType);
            }

            return result;
        }

        /// <summary>
        /// Reads a point from the stream
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="receivedOrdinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The Point.</returns>
        protected Point ReadPoint(BinaryReader reader, GeometryFactory factory, Ordinates receivedOrdinates)
        {
            return factory.CreatePoint(ReadCoordinateSequence(reader, factory.CoordinateSequenceFactory, factory.PrecisionModel, 1, receivedOrdinates));
        }

        /// <summary>
        /// Reads a coordinate sequence from the stream, which length is not yet known.
        /// </summary>
        /// <param name="reader">The binary reader</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="precisionModel">The precision model used to make x- and y-ordinates precise.</param>
        /// <param name="receivedOrdinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The coordinate sequence</returns>
        protected CoordinateSequence ReadCoordinateSequence(BinaryReader reader, CoordinateSequenceFactory factory, PrecisionModel precisionModel, Ordinates receivedOrdinates)
        {
            int numPoints = reader.ReadInt32();
            return ReadCoordinateSequence(reader, factory, precisionModel, numPoints, receivedOrdinates);
        }


        /// <summary>
        /// Reads a coordinate sequence from the stream, which length is not yet known.
        /// </summary>
        /// <param name="reader">The binary reader</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="precisionModel">The precision model used to make x- and y-ordinates precise.</param>
        /// <param name="receivedOrdinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The coordinate sequence</returns>
        protected CoordinateSequence ReadCoordinateSequenceRing(BinaryReader reader, CoordinateSequenceFactory factory, PrecisionModel precisionModel, Ordinates receivedOrdinates)
        {
            int numPoints = reader.ReadInt32();
            var sequence = ReadCoordinateSequence(reader, factory, precisionModel, numPoints, receivedOrdinates);
            return !RepairRings || CoordinateSequences.IsRing(sequence)
                ? sequence
                : CoordinateSequences.EnsureValidRing(factory, sequence);
        }

        /// <summary>
        /// Reads a <see cref="CoordinateSequence"/> from the stream
        /// </summary>
        /// <param name="reader">The binary reader</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="precisionModel">The precision model used to make x- and y-ordinates precise.</param>
        /// <param name="numPoints">The number of points in the coordinate sequence.</param>
        /// <param name="receivedOrdinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The coordinate sequence</returns>
        protected CoordinateSequence ReadCoordinateSequence(BinaryReader reader, CoordinateSequenceFactory factory, PrecisionModel precisionModel, int numPoints, Ordinates receivedOrdinates)
        {
            var outputOrdinates = receivedOrdinates;
            if (HandleOrdinates != Ordinates.None)
            {
                outputOrdinates &= HandleOrdinates;
            }

            var sequence = factory.Create(numPoints, outputOrdinates);

            bool receivedZ = receivedOrdinates.HasFlag(Ordinates.Z);
            bool receivedM = receivedOrdinates.HasFlag(Ordinates.M);
            bool outputtingZ = outputOrdinates.HasFlag(Ordinates.Z) && sequence.HasZ;
            bool outputtingM = outputOrdinates.HasFlag(Ordinates.M) && sequence.HasM;

            for (int i = 0; i < numPoints; i++)
            {
                sequence.SetX(i, precisionModel.MakePrecise(reader.ReadDouble()));
                sequence.SetY(i, precisionModel.MakePrecise(reader.ReadDouble()));

                if (receivedZ)
                {
                    double z = reader.ReadDouble();
                    if (outputtingZ)
                    {
                        sequence.SetZ(i, z);
                    }
                }

                if (receivedM)
                {
                    double m = reader.ReadDouble();
                    if (outputtingM)
                    {
                        sequence.SetM(i, m);
                    }
                }
            }

            return sequence;
        }

        /// <summary>
        /// Reads a <see cref="LineString"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="ordinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The LineString.</returns>
        protected LineString ReadLineString(BinaryReader reader, GeometryFactory factory, Ordinates ordinates)
        {
            var coordinates = ReadCoordinateSequenceRing(reader, factory.CoordinateSequenceFactory, factory.PrecisionModel, ordinates);
            return factory.CreateLineString(coordinates);
        }

        /// <summary>
        /// Reads a <see cref="LinearRing"/> line string from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="ordinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The LinearRing.</returns>
        protected LinearRing ReadLinearRing(BinaryReader reader, GeometryFactory factory, Ordinates ordinates)
        {
            var coordinates = ReadCoordinateSequence(reader, factory.CoordinateSequenceFactory, factory.PrecisionModel, ordinates);
            return factory.CreateLinearRing(coordinates);
        }

        /// <summary>
        /// Reads a <see cref="Polygon"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="ordinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The LineString.</returns>
        protected Polygon ReadPolygon(BinaryReader reader, GeometryFactory factory, Ordinates ordinates)
        {
            int numRings = reader.ReadInt32();
            var exteriorRing = ReadLinearRing(reader, factory, ordinates);
            var interiorRings = new LinearRing[numRings - 1];
            for (int i = 0; i < interiorRings.Length; i++)
            {
                interiorRings[i] = ReadLinearRing(reader, factory, ordinates);
            }

            return factory.CreatePolygon(exteriorRing, interiorRings);
        }

        /// <summary>
        /// Reads an array of geometries
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="container">The container for the geometries</param>
        protected void ReadGeometryArray<TGeometry>(BinaryReader reader, TGeometry[] container)
            where TGeometry : Geometry
        {
            for (int i = 0; i < container.Length; i++)
            {
                container[i] = (TGeometry)Read(reader);
            }
        }

        /// <summary>
        /// Reads a <see cref="MultiPoint"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <returns>The MultiPoint</returns>
        protected MultiPoint ReadMultiPoint(BinaryReader reader, GeometryFactory factory)
        {
            int numGeometries = reader.ReadInt32();
            var points = new Point[numGeometries];
            ReadGeometryArray(reader, points);
            return factory.CreateMultiPoint(points);
        }

        /// <summary>
        /// Reads a <see cref="MultiLineString"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <returns>The MultiLineString</returns>
        protected MultiLineString ReadMultiLineString(BinaryReader reader, GeometryFactory factory)
        {
            int numGeometries = reader.ReadInt32();
            var strings = new LineString[numGeometries];
            ReadGeometryArray(reader, strings);
            return factory.CreateMultiLineString(strings);
        }

        /// <summary>
        /// Reads a <see cref="MultiPolygon"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <returns>The MultPolygon</returns>
        protected MultiPolygon ReadMultPolygon(BinaryReader reader, GeometryFactory factory)
        {
            int numGeometries = reader.ReadInt32();
            var polygons = new Polygon[numGeometries];
            ReadGeometryArray(reader, polygons);
            return factory.CreateMultiPolygon(polygons);
        }

        /// <summary>
        /// Reads a <see cref="GeometryCollection"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <returns>The GeometryCollection</returns>
        protected GeometryCollection ReadGeometryCollection(BinaryReader reader, GeometryFactory factory)
        {
            int numGeometries = reader.ReadInt32();
            var geometries = new Geometry[numGeometries];
            ReadGeometryArray(reader, geometries);
            return factory.CreateGeometryCollection(geometries);
        }
    }
}
