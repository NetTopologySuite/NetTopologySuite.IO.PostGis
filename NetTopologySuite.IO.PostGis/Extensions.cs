using GeoAPI.Geometries;

namespace NetTopologySuite.IO
{
    internal static class Extensions
    {
        // needed because net35-client doesn't have Enum.HasFlag:
        internal static bool HasFlag(this Ordinates ordinates, Ordinates flag) => (ordinates & flag) == flag;
    }
}
