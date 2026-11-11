using NetTopologySuite.Geometries;

namespace VL.PROJ;

// The one factory this package creates geometry with on its own (the CRS node's Area Of Use):
// WGS 84 longitude/latitude, SRID 4326. Cached, as VL.NetTopologySuite caches its factories:
// a static node runs every frame.

/// <summary>Geometry in WGS 84 longitude and latitude (SRID 4326).</summary>
internal static class Wgs84
{
    public const int Srid = 4326;

    public static readonly GeometryFactory Factory = new(new PrecisionModel(), Srid);

    /// <summary>A lon/lat rectangle as a polygon, or a point / line when it is degenerate.</summary>
    public static Geometry ToGeometry(Envelope e) => Factory.ToGeometry(e);
}
