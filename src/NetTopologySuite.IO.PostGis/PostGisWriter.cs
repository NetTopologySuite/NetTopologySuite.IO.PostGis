// Ported from PostGIS:
// http://svn.refractions.net/postgis/trunk/java/jdbc/src/org/postgis/binary/BinaryWriter.java

using System;
using System.IO;
using System.Runtime.InteropServices;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.Utilities;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Writes a PostGIS binary representation of a <c>Geometry</c>.
    /// </summary>
    public class PostGisWriter
    {
        private Ordinates _outputOrdinates;

        /// <summary>
        /// Initializes writer with LittleIndian byte order.
        /// </summary>
        public PostGisWriter() :
            this(ByteOrder.LittleEndian)
        { }

        /// <summary>
        /// Initializes writer with the specified byte order.
        /// </summary>
        /// <param name="byteOrder">Encoding type</param>
        public PostGisWriter(ByteOrder byteOrder)
        {
            ByteOrder = byteOrder;
            HandleOrdinates = Ordinates.None;
        }

        /// <summary>
        /// Gets the <see cref="IO.ByteOrder"/> value indicating the byte order to write out.
        /// </summary>
        public ByteOrder ByteOrder { get; }

        /// <summary>
        /// Gets an <see cref="Ordinates"/> mask indicating which ordinates present in the geometry
        /// should be transferred to the data that we write out, or <see cref="Ordinates.None"/> if
        /// all ordinates present in the geometry should be transferred.
        /// <para>
        /// Flags other than those in <see cref="Ordinates.XYZM"/> will be ignored.
        /// </para>
        /// </summary>
        public Ordinates HandleOrdinates
        {
            get { return _outputOrdinates; }
            set
            {
                _outputOrdinates = value == Ordinates.None
                    ? Ordinates.None
                    : (value | Ordinates.XY) & Ordinates.XYZM;
            }
        }

        /// <summary>
        /// Writes out a given <see cref="Geometry"/> to a byte array.
        /// </summary>
        /// <param name="geometry">The <see cref="Geometry"/> to write.</param>
        /// <returns>The byte array.</returns>
        public byte[] Write(Geometry geometry)
        {
            var maxCoords = HandleOrdinates == Ordinates.None ? Ordinates.XYZM : HandleOrdinates;
            int coordinateSpace = 8 * OrdinatesUtility.OrdinatesToDimension(maxCoords & CheckOrdinates(geometry));
            byte[] bytes = GetBytes(geometry, coordinateSpace);
            Write(geometry, new MemoryStream(bytes));

            return bytes;
        }

        /// <summary>
        /// Writes out a given <see cref="Geometry"/> to a <see cref="Stream"/>.
        /// </summary>
        /// <param name="geometry">The <see cref="Geometry"/> to write.</param>
        /// <param name="stream">The <see cref="Stream"/> to write the geometry to.</param>
        public void Write(Geometry geometry, Stream stream)
        {
            using (var writer = ByteOrder == ByteOrder.LittleEndian ? new BinaryWriter(stream) : new BEBinaryWriter(stream))
            {
                Write(geometry, ByteOrder, writer);
            }
        }

        /// <summary>
        /// Writes a binary encoded PostGIS or the given <paramref name="geometry"/> using the provided <paramref name="writer"/>.
        /// </summary>
        /// <param name="geometry">The geometry to write.</param>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="writer">The writer to use.</param>
        private void Write(Geometry geometry, ByteOrder byteOrder, BinaryWriter writer)
        {
            var ordinates = CheckOrdinates(geometry);
            if (HandleOrdinates != Ordinates.None)
            {
                ordinates &= HandleOrdinates;
            }
            Write(geometry, ordinates, byteOrder, true, writer);
        }

        private void Write(Geometry geometry, Ordinates ordinates, ByteOrder byteOrder, bool emitSRID, BinaryWriter writer)
        {
            switch (geometry)
            {
                case Point point:
                    Write(point, ordinates, byteOrder, emitSRID, writer);
                    break;

                case LinearRing linearRing:
                    Write(linearRing, ordinates, byteOrder, emitSRID, writer);
                    break;

                case LineString lineString:
                    Write(lineString, ordinates, byteOrder, emitSRID, writer);
                    break;

                case Polygon polygon:
                    Write(polygon, ordinates, byteOrder, emitSRID, writer);
                    break;

                case MultiPoint multiPoint:
                    Write(multiPoint, ordinates, byteOrder, emitSRID, writer);
                    break;

                case MultiLineString multiLineString:
                    Write(multiLineString, ordinates, byteOrder, emitSRID, writer);
                    break;

                case MultiPolygon multiPolygon:
                    Write(multiPolygon, ordinates, byteOrder, emitSRID, writer);
                    break;

                case GeometryCollection geometryCollection:
                    Write(geometryCollection, ordinates, byteOrder, emitSRID, writer);
                    break;

                default:
                    throw new ArgumentException("Geometry not recognized: " + geometry);
            }
        }

        /// <summary>
        /// Writes the binary encoded PostGIS header.
        /// </summary>
        /// <param name="type">The PostGIS geometry type.</param>
        /// <param name="srid">The spatial reference of the geometry</param>
        /// <param name="emitSrid">Flag indicating that <paramref name="srid"/> should be written</param>
        /// <param name="ordinates"></param>
        /// <param name="byteOrder">The byte order specified.</param>
        /// <param name="writer">The writer to use.</param>
        private static void WriteHeader(PostGisGeometryType type, int srid, bool emitSrid, Ordinates ordinates,
            ByteOrder byteOrder, BinaryWriter writer)
        {
            writer.Write((byte)byteOrder);

            // write typeword
            uint typeword = (uint)type;

            if (ordinates.HasFlag(Ordinates.Z))
            {
                typeword |= 0x80000000;
            }

            if (ordinates.HasFlag(Ordinates.M))
            {
                typeword |= 0x40000000;
            }

            emitSrid &= srid > 0;
            if (emitSrid)
            {
                typeword |= 0x20000000;
            }

            writer.Write(typeword);

            if (emitSrid)
            {
                writer.Write(srid);
            }
        }

        private static void Write(CoordinateSequence sequence, Ordinates ordinates, ByteOrder byteOrder, BinaryWriter writer, bool justOne)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));

            // Handle empty multi-coordinate geometries
            if (sequence.Count == 0)
            {
                if (!justOne)
                {
                    writer.Write(0);
                    return;
                }
            }

            // Get which ordinates to write
            bool writeZ = ordinates.HasFlag(Ordinates.Z);
            bool writeM = ordinates.HasFlag(Ordinates.M);

            // Write length if not points
            int length = 1;
            if (!justOne)
            {
                length = sequence.Count;
                writer.Write(length);
            }
            // Write empty point
            else if (sequence.Count == 0)
            {
                const long postgisNaN = 9221120237041090560;
                writer.Write(postgisNaN);
                writer.Write(postgisNaN);
                if (writeZ)
                    writer.Write(postgisNaN);
                if (writeM)
                    writer.Write(postgisNaN);
                return;
            }

            switch (sequence)
            {
                case PackedDoubleCoordinateSequence packedSequence when byteOrder == ByteOrder.LittleEndian == BitConverter.IsLittleEndian:
#if NETSTANDARD2_1
                    writer.Write(MemoryMarshal.AsBytes<double>(packedSequence.GetRawCoordinates()));
#else
                    writer.Write(MemoryMarshal.AsBytes<double>(packedSequence.GetRawCoordinates()).ToArray());
#endif
                break;

                case RawCoordinateSequence rawSequence when rawSequence.HACK_TryGetSingleOrdinateGroup(out var coords, out int[] dimensions):
                    for (int i = dimensions.Length - 1; i > 0; i--)
                    {
                        if (dimensions[i] != dimensions[i - 1] + 1)
                        {
                            goto default;
                        }
                    }

#if NETSTANDARD2_1
                    writer.Write(MemoryMarshal.AsBytes<double>(coords.Span));
#else
                    writer.Write(MemoryMarshal.AsBytes<double>(coords.Span).ToArray());
#endif
                    break;

                default:
                    for (int i = 0; i < length; i++)
                    {
                        writer.Write(sequence.GetX(i));
                        writer.Write(sequence.GetY(i));
                        if (writeZ)
                        {
                            writer.Write(sequence.GetZ(i));
                        }

                        if (writeM)
                        {
                            writer.Write(sequence.GetM(i));
                        }
                    }

                    break;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="point"></param>
        /// <param name="ordinates"></param>
        /// <param name="byteOrder"></param>
        /// <param name="emitSRID"></param>
        /// <param name="writer"></param>
        private void Write(Point point, Ordinates ordinates, ByteOrder byteOrder, bool emitSRID, BinaryWriter writer)
        {
            WriteHeader(PostGisGeometryType.Point, point.SRID, emitSRID, ordinates, byteOrder, writer);
            Write(point.CoordinateSequence, ordinates, byteOrder, writer, true);
        }

        /// <summary>
        /// Write an Array of "full" Geometries
        /// </summary>
        /// <param name="geometries"></param>
        /// <param name="ordinates"></param>
        /// <param name="byteOrder"></param>
        /// <param name="writer"></param>
        private void Write(Geometry[] geometries, Ordinates ordinates, ByteOrder byteOrder, BinaryWriter writer)
        {
            for (int i = 0; i < geometries.Length; i++)
            {
                Write(geometries[i], ordinates, byteOrder, false, writer);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="lineString"></param>
        /// <param name="ordinates"></param>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="emitSRID"></param>
        /// <param name="writer"></param>
        private void Write(LineString lineString, Ordinates ordinates, ByteOrder byteOrder, bool emitSRID, BinaryWriter writer)
        {
            WriteHeader(PostGisGeometryType.LineString, lineString.SRID, emitSRID, ordinates, byteOrder, writer);
            Write(lineString.CoordinateSequence, ordinates, byteOrder, writer, false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="linearRing"></param>
        /// <param name="ordinates"></param>
        /// <param name="byteOrder"></param>
        /// <param name="writer"></param>
        private void Write(LinearRing linearRing, Ordinates ordinates, ByteOrder byteOrder, BinaryWriter writer)
        {
            Write(linearRing.CoordinateSequence, ordinates, byteOrder, writer, false);
        }

        /// <summary>
        /// Writes a 'Polygon' to the stream.
        /// </summary>
        /// <param name="polygon">The polygon to write.</param>
        /// <param name="ordinates">The ordinates to write. <see cref="Ordinates.XY"/> are always written.</param>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="emitSRID">A flag indicating if <see cref="Geometry.SRID"/> value should be emitted.</param>
        /// <param name="writer">The writer to use.</param>
        private void Write(Polygon polygon, Ordinates ordinates, ByteOrder byteOrder, bool emitSRID, BinaryWriter writer)
        {
            WriteHeader(PostGisGeometryType.Polygon, polygon.SRID, emitSRID, ordinates, byteOrder, writer);

            // If polygon is empty, simply write 0
            if (polygon.IsEmpty)
            {
                writer.Write(0);
                return;
            }

            var holes = polygon.Holes;
            writer.Write(holes.Length + 1);

            Write(polygon.Shell, ordinates, byteOrder, writer);
            for (int i = 0; i < holes.Length; i++)
            {
                Write(holes[i], ordinates, byteOrder, writer);
            }
        }

        /// <summary>
        /// Writes a 'MultiPoint' to the stream.
        /// </summary>
        /// <param name="multiPoint">The polygon to write.</param>
        /// <param name="ordinates">The ordinates to write. <see cref="Ordinates.XY"/> are always written.</param>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="emitSRID">A flag indicating if <see cref="Geometry.SRID"/> value should be emitted.</param>
        /// <param name="writer">The writer to use.</param>
        private void Write(MultiPoint multiPoint, Ordinates ordinates, ByteOrder byteOrder, bool emitSRID, BinaryWriter writer)
        {
            WriteHeader(PostGisGeometryType.MultiPoint, multiPoint.SRID, emitSRID, ordinates, byteOrder, writer);
            writer.Write(multiPoint.NumGeometries);
            Write(multiPoint.Geometries, ordinates, byteOrder, writer);
        }

        /// <summary>
        /// Writes a 'MultiLineString' to the stream.
        /// </summary>
        /// <param name="multiLineString">The linestring to write.</param>
        /// <param name="ordinates">The ordinates to write. <see cref="Ordinates.XY"/> are always written.</param>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="emitSRID">A flag indicating if <see cref="Geometry.SRID"/> value should be emitted.</param>
        /// <param name="writer">The writer to use.</param>
        private void Write(MultiLineString multiLineString, Ordinates ordinates, ByteOrder byteOrder, bool emitSRID, BinaryWriter writer)
        {
            WriteHeader(PostGisGeometryType.MultiLineString, multiLineString.SRID, emitSRID, ordinates, byteOrder, writer);
            writer.Write(multiLineString.NumGeometries);
            Write(multiLineString.Geometries, ordinates, byteOrder, writer);
        }

        /// <summary>
        /// Writes a 'MultiPolygon' to the stream.
        /// </summary>
        /// <param name="multiPolygon">The polygon to write.</param>
        /// <param name="ordinates">The ordinates to write. <see cref="Ordinates.XY"/> are always written.</param>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="emitSRID">A flag indicating if <see cref="Geometry.SRID"/> value should be emitted.</param>
        /// <param name="writer">The writer to use.</param>
        private void Write(MultiPolygon multiPolygon, Ordinates ordinates, ByteOrder byteOrder, bool emitSRID, BinaryWriter writer)
        {
            WriteHeader(PostGisGeometryType.MultiPolygon, multiPolygon.SRID, emitSRID, ordinates, byteOrder, writer);
            writer.Write(multiPolygon.NumGeometries);
            Write(multiPolygon.Geometries, ordinates, byteOrder, writer);
        }

        /// <summary>
        /// Writes a 'GeometryCollection' to the stream.
        /// </summary>
        /// <param name="geomCollection">The polygon to write.</param>
        /// <param name="ordinates">The ordinates to write. <see cref="Ordinates.XY"/> are always written.</param>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="emitSRID">A flag indicating if <see cref="Geometry.SRID"/> value should be emitted.</param>
        /// <param name="writer">The writer to use.</param>
        private void Write(GeometryCollection geomCollection, Ordinates ordinates, ByteOrder byteOrder, bool emitSRID, BinaryWriter writer)
        {
            WriteHeader(PostGisGeometryType.GeometryCollection, geomCollection.SRID, emitSRID, ordinates, byteOrder, writer);
            writer.Write(geomCollection.NumGeometries);
            Write(geomCollection.Geometries, ordinates, byteOrder, writer);
        }

        #region Prepare Buffer
        /// <summary>
        /// Supplies a byte array for the  length for Byte Stream.
        /// </summary>
        /// <param name="geometry">The geometry that needs to be written.</param>
        /// <param name="coordinateSpace">The size that is needed per ordinate.</param>
        /// <returns></returns>
        private byte[] GetBytes(Geometry geometry, int coordinateSpace)
        {
            return new byte[GetByteStreamSize(geometry, coordinateSpace, true)];
        }

        /// <summary>
        /// Gets the required size for the byte stream's buffer to hold the geometry information.
        /// </summary>
        /// <param name="geometry">The geometry to write</param>
        /// <param name="coordinateSpace">The size for each ordinate entry.</param>
        /// <param name="emitSRID">Flag indicating if srid value should be written</param>
        /// <returns>The size</returns>
        private int GetByteStreamSize(Geometry geometry, int coordinateSpace, bool emitSRID)
        {
            int result = 0;

            // write endian flag
            result += 1;

            // write typeword
            result += 4;

            emitSRID &= geometry.SRID > 0;
            if (emitSRID)
            {
                result += 4;
            }

            switch (geometry)
            {
                case Point point:
                    result += GetByteStreamSize(point, coordinateSpace);
                    break;

                case LineString lineString:
                    result += GetByteStreamSize(lineString, coordinateSpace);
                    break;

                case Polygon polygon:
                    result += GetByteStreamSize(polygon, coordinateSpace);
                    break;

                case MultiPoint multiPoint:
                    result += GetByteStreamSize(multiPoint, coordinateSpace);
                    break;

                case MultiLineString multiLineString:
                    result += GetByteStreamSize(multiLineString, coordinateSpace);
                    break;

                case MultiPolygon multiPolygon:
                    result += GetByteStreamSize(multiPolygon, coordinateSpace);
                    break;

                case GeometryCollection geometryCollection:
                    result += GetByteStreamSize(geometryCollection, coordinateSpace);
                    break;

                default:
                    throw new ArgumentException("ShouldNeverReachHere");
            }

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        private int GetByteStreamSize(GeometryCollection geometry, int coordinateSpace)
        {
            // 4-byte count + subgeometries
            return 4 + GetByteStreamSize(geometry.Geometries, coordinateSpace);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        private int GetByteStreamSize(MultiPolygon geometry, int coordinateSpace)
        {
            // 4-byte count + subgeometries
            return 4 + GetByteStreamSize(geometry.Geometries, coordinateSpace);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        private int GetByteStreamSize(MultiLineString geometry, int coordinateSpace)
        {
            // 4-byte count + subgeometries
            return 4 + GetByteStreamSize(geometry.Geometries, coordinateSpace);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        private int GetByteStreamSize(MultiPoint geometry, int coordinateSpace)
        {
            // int size
            int result = 4;
            // Note: Not NumPoints, as empty points hold no coordinate!
            if (geometry.NumGeometries > 0)
            {
                // We can shortcut here, as all subgeoms have the same fixed size
                result += geometry.NumGeometries * GetByteStreamSize(geometry.GetGeometryN(0), coordinateSpace, false);
            }

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        private int GetByteStreamSize(Polygon geometry, int coordinateSpace)
        {
            if (geometry.IsEmpty) return 4;

            // int length
            int result = 4;

            // shell ordinates
            result += GetByteStreamSize(geometry.ExteriorRing, coordinateSpace);

            // holes and their ordinates
            var holes = geometry.Holes;
            for (int i = 0; i < holes.Length; i++)
                result += GetByteStreamSize(holes[i], coordinateSpace);

            return result;
        }

        /// <summary>
        /// Write an Array of "full" Geometries
        /// </summary>
        /// <param name="container"></param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        private int GetByteStreamSize(Geometry[] container, int coordinateSpace)
        {
            int result = 0;
            for (int i = 0; i < container.Length; i++)
            {
                result += GetByteStreamSize(container[i], coordinateSpace, false);
            }

            return result;
        }

        /// <summary>
        /// Calculates the amount of space needed to write this coordinate sequence.
        /// </summary>
        /// <param name="sequence">The sequence</param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        private static int GetByteStreamSize(CoordinateSequence sequence, int coordinateSpace)
        {
            // number of points
            const int result = 4;

            // And the amount of the points itsself, in consistent geometries
            // all points have equal size.
            if (sequence.Count == 0)
            {
                return result;
            }

            return result + sequence.Count * coordinateSpace;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        protected int GetByteStreamSize(LineString geometry, int coordinateSpace)
        {
            return GetByteStreamSize(geometry.CoordinateSequence, coordinateSpace);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="coordinateSpace">The size needed for each coordinate</param>
        /// <returns></returns>
        protected int GetByteStreamSize(Point geometry, int coordinateSpace)
        {
            return coordinateSpace;
        }

        #endregion

        #region Check ordinates

        private static Ordinates CheckOrdinates(Geometry geometry)
        {
            switch (geometry)
            {
                case Point point:
                    return CheckOrdinates(point.CoordinateSequence);

                case LineString lineString:
                    return CheckOrdinates(lineString.CoordinateSequence);

                case Polygon polygon:
                    return CheckOrdinates(polygon.ExteriorRing.CoordinateSequence);

                case GeometryCollection collection:
                    return collection.Count == 0 ? Ordinates.None : CheckOrdinates(collection.GetGeometryN(0));

                default:
                    Assert.ShouldNeverReachHere();
                    return Ordinates.None;
            }
        }

        private static Ordinates CheckOrdinates(CoordinateSequence sequence)
        {
            if (sequence == null) return Ordinates.None;

            var result = Ordinates.XY;
            if (sequence.HasZ) result |= Ordinates.Z;
            if (sequence.HasM) result |= Ordinates.M;

            return result;
        }

        #endregion
    }
}
