// 	Ported from PostGIS:
// 	http://svn.refractions.net/postgis/trunk/java/jdbc/src/org/postgis/binary/BinaryParser.java

using System;
using System.IO;
using GeoAPI;
using GeoAPI.Geometries;
using GeoAPI.IO;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Converts a PostGIS binary data to a <c>Geometry</c>.
    /// </summary>
    public class PostGisReader : IBinaryGeometryReader
    {
        private IGeometryFactory _factory;

        private readonly IPrecisionModel _precisionModel;
        private readonly ICoordinateSequenceFactory _coordinateSequenceFactory;

        ///// <summary>
        ///// <c>Geometry</c> builder.
        ///// </summary>
        //IGeometryFactory IGeometryReader<byte[]>.Factory
        //{
        //    get { return _factory; }
        //    set
        //    {
        //        if (value == null)
        //            return;

        //        _factory = value;
        //    }
        //}

        /// <summary>
        /// Initialize reader with a standard settings.
        /// </summary>
        public PostGisReader()
            : this(GeometryServiceProvider.Instance.DefaultCoordinateSequenceFactory,
                   GeometryServiceProvider.Instance.DefaultPrecisionModel,
                   Ordinates.None)
        { }

        /// <summary>
        /// Initialize reader with the given <c>GeometryFactory</c>.
        /// </summary>
        /// <param name="factory"></param>
        public PostGisReader(IGeometryFactory factory)
            : this(factory.CoordinateSequenceFactory, factory.PrecisionModel, Ordinates.XYZM)
        {
        }

        /// <summary>
        /// Initialize reader with the given coordinate sequence factory and the given precision model.
        /// </summary>
        /// <param name="coordinateSequenceFactory"></param>
        /// <param name="precisionModel"> </param>
        public PostGisReader(ICoordinateSequenceFactory coordinateSequenceFactory, IPrecisionModel precisionModel)
            : this(coordinateSequenceFactory, precisionModel, Ordinates.XYZM)
        { }

        /// <summary>
        /// Initialize reader with the given <c>GeometryFactory</c>.
        /// </summary>
        /// <param name="coordinateSequenceFactory"></param>
        /// <param name="precisionModel"> </param>
        /// <param name="handleOrdinates">The ordinates to handle</param>
        public PostGisReader(ICoordinateSequenceFactory coordinateSequenceFactory, IPrecisionModel precisionModel, Ordinates handleOrdinates)
        {
            _coordinateSequenceFactory = coordinateSequenceFactory;
            _precisionModel = precisionModel;

            HandleOrdinates = handleOrdinates;
        }

        /// <inheritdoc cref="IGeometryReader{TSource}.Read(TSource)"/>
        public IGeometry Read(byte[] data)
        {
            using (Stream stream = new MemoryStream(data))
                return Read(stream);
        }

        /// <inheritdoc cref="IGeometryReader{TSource}.Read(Stream)"/>
        public IGeometry Read(Stream stream)
        {
            using (var reader = new BiEndianBinaryReader(stream))
                return Read(reader);
        }

        /// <inheritdoc cref="IGeometryReader{TSource}.RepairRings"/>>
        public bool RepairRings { get; set; }

        /// <summary>
        /// Reads a geometry using the provided <see cref="BinaryReader"/>
        /// </summary>
        /// <param name="reader">The reader to use</param>
        /// <returns>A geometry</returns>
        protected IGeometry Read(BinaryReader reader)
        {
            if (!(reader is BiEndianBinaryReader))
                throw new ArgumentException("Reader must be BiEndianBinaryReader", nameof(reader));

            ((BiEndianBinaryReader)reader).Endianess = (ByteOrder)reader.ReadByte();

            int typeword = reader.ReadInt32();

            // cut off high flag bits
            var geometryType = (PostGisGeometryType)(typeword & 0x1FFFFFFF);

            bool hasZ = (typeword & 0x80000000) != 0;
            bool hasM = (typeword & 0x40000000) != 0;
            bool hasS = (typeword & 0x20000000) != 0;

            var receivedOrdinates = Ordinates.XY;
            if (hasZ) receivedOrdinates |= Ordinates.Z;
            if (hasM) receivedOrdinates |= Ordinates.M;

            int srid = -1;

            if (hasS)
                srid = reader.ReadInt32();

            if (_factory == null || _factory.SRID != srid)
                _factory = GeometryServiceProvider.Instance.CreateGeometryFactory(_precisionModel, srid, _coordinateSequenceFactory);

            var factory = _factory;

            IGeometry result;
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
                    result = ReadMultiPolygon(reader, factory);
                    break;
                case PostGisGeometryType.GeometryCollection:
                    result = ReadGeometryCollection(reader, factory);
                    break;
                default:
                    throw new ArgumentException("Geometry type not recognized. GeometryCode: " + geometryType);
            }

            //result.SRID = hasS ? srid : -1;
            return result;
        }

        /// <summary>
        /// Reads a point from the stream
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="receivedOrdinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The Point.</returns>
        protected IPoint ReadPoint(BinaryReader reader, IGeometryFactory factory, Ordinates receivedOrdinates)
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
        protected ICoordinateSequence ReadCoordinateSequence(BinaryReader reader, ICoordinateSequenceFactory factory, IPrecisionModel precisionModel, Ordinates receivedOrdinates)
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
        protected ICoordinateSequence ReadCoordinateSequenceRing(BinaryReader reader, ICoordinateSequenceFactory factory, IPrecisionModel precisionModel, Ordinates receivedOrdinates)
        {
            int numPoints = reader.ReadInt32();
            var sequence = ReadCoordinateSequence(reader, factory, precisionModel, numPoints, receivedOrdinates);
            if (!RepairRings) return sequence;
            if (CoordinateSequences.IsRing(sequence)) return sequence;
            return CoordinateSequences.EnsureValidRing(factory, sequence);
        }

        /// <summary>
        /// Reads a <see cref="ICoordinateSequence"/> from the stream
        /// </summary>
        /// <param name="reader">The binary reader</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="precisionModel">The precision model used to make x- and y-ordinates precise.</param>
        /// <param name="numPoints">The number of points in the coordinate sequence.</param>
        /// <param name="receivedOrdinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The coordinate sequence</returns>
        protected ICoordinateSequence ReadCoordinateSequence(BinaryReader reader, ICoordinateSequenceFactory factory, IPrecisionModel precisionModel, int numPoints, Ordinates receivedOrdinates)
        {
            var outputOrdinates = receivedOrdinates;
            if (HandleOrdinates != Ordinates.None)
                outputOrdinates &= HandleOrdinates;

            var sequence = factory.Create(numPoints, outputOrdinates);

            var receivedZ = receivedOrdinates.HasFlag(Ordinates.Z);
            var receivedM = receivedOrdinates.HasFlag(Ordinates.M);
            var outputtingZ = outputOrdinates.HasFlag(Ordinates.Z);
            var outputtingM = outputOrdinates.HasFlag(Ordinates.M);

            for (int i = 0; i < numPoints; i++)
            {
                sequence.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(reader.ReadDouble()));
                sequence.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(reader.ReadDouble()));

                if (receivedZ)
                {
                    var z = reader.ReadDouble();
                    if (outputtingZ)
                        sequence.SetOrdinate(i, Ordinate.Z, z);
                }

                if (receivedM)
                {
                    var m = reader.ReadDouble();
                    if (outputtingM)
                        sequence.SetOrdinate(i, Ordinate.M, m);
                }
            }

            return sequence;
        }

        /// <summary>
        /// Reads a <see cref="ILineString"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="ordinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The LineString.</returns>
        protected ILineString ReadLineString(BinaryReader reader, IGeometryFactory factory, Ordinates ordinates)
        {
            var coordinates = ReadCoordinateSequenceRing(reader, factory.CoordinateSequenceFactory, factory.PrecisionModel, ordinates);
            return factory.CreateLineString(coordinates);
        }

        /// <summary>
        /// Reads a <see cref="ILinearRing"/> line string from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="ordinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The LinearRing.</returns>
        protected ILinearRing ReadLinearRing(BinaryReader reader, IGeometryFactory factory, Ordinates ordinates)
        {
            var coordinates = ReadCoordinateSequence(reader, factory.CoordinateSequenceFactory, factory.PrecisionModel, ordinates);
            return factory.CreateLinearRing(coordinates);
        }

        /// <summary>
        /// Reads a <see cref="IPolygon"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <param name="ordinates">The ordinates to read. <see cref="Ordinates.XY"/> are always read.</param>
        /// <returns>The LineString.</returns>
        protected IPolygon ReadPolygon(BinaryReader reader, IGeometryFactory factory, Ordinates ordinates)
        {
            int numRings = reader.ReadInt32();
            var exteriorRing = ReadLinearRing(reader, factory, ordinates);
            var interiorRings = new ILinearRing[numRings - 1];
            for (int i = 0; i < numRings - 1; i++)
                interiorRings[i] = ReadLinearRing(reader, factory, ordinates);
            return factory.CreatePolygon(exteriorRing, interiorRings);
        }

        /// <summary>
        /// Reads an array of geometries
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="container">The container for the geometries</param>
        protected void ReadGeometryArray<TGeometry>(BinaryReader reader, TGeometry[] container)
            where TGeometry : IGeometry
        {
            for (int i = 0; i < container.Length; i++)
                container[i] = (TGeometry)Read(reader);
        }

        /// <summary>
        /// Reads a <see cref="IMultiPoint"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <returns>The MultiPoint</returns>
        protected IMultiPoint ReadMultiPoint(BinaryReader reader, IGeometryFactory factory)
        {
            int numGeometries = reader.ReadInt32();
            var points = new IPoint[numGeometries];
            ReadGeometryArray(reader, points);
            return factory.CreateMultiPoint(points);
        }

        /// <summary>
        /// Reads a <see cref="IMultiLineString"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <returns>The MultiLineString</returns>
        protected IMultiLineString ReadMultiLineString(BinaryReader reader, IGeometryFactory factory)
        {
            int numGeometries = reader.ReadInt32();
            var strings = new ILineString[numGeometries];
            ReadGeometryArray(reader, strings);
            return factory.CreateMultiLineString(strings);
        }

        /// <summary>
        /// Reads a <see cref="IMultiPolygon"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <returns>The MultiPolygon</returns>
        protected IMultiPolygon ReadMultiPolygon(BinaryReader reader, IGeometryFactory factory)
        {
            int numGeometries = reader.ReadInt32();
            var polygons = new IPolygon[numGeometries];
            ReadGeometryArray(reader, polygons);
            return factory.CreateMultiPolygon(polygons);
        }

        /// <summary>
        /// Reads a <see cref="IGeometryCollection"/> from the input stream.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="factory">The geometry factory to use for geometry creation.</param>
        /// <returns>The GeometryCollection</returns>
        protected IGeometryCollection ReadGeometryCollection(BinaryReader reader, IGeometryFactory factory)
        {
            int numGeometries = reader.ReadInt32();
            var geometries = new IGeometry[numGeometries];
            ReadGeometryArray(reader, geometries);
            return factory.CreateGeometryCollection(geometries);
        }

        #region Implementation of IGeometryIOSettings

        /// <inheritdoc cref="IGeometryIOSettings.HandleSRID"/>
        public bool HandleSRID
        {
            get { return true; }
            set { }
        }

        /// <inheritdoc cref="IGeometryIOSettings.AllowedOrdinates"/>
        public Ordinates AllowedOrdinates
        {
            get { return _coordinateSequenceFactory.Ordinates & Ordinates.XYZM; }
        }

        private Ordinates _handleOrdinates;

        /// <inheritdoc cref="IGeometryIOSettings.HandleOrdinates"/>
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

        #endregion Implementation of IGeometryIOSettings
    }
}
