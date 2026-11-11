using NetTopologySuite.Geometries;
using VL.Core.Import;

namespace VL.PROJ;

// Category PROJ, straight from the namespace: [SkipCategory] drops the type level, so the nodes
// sit in PROJ itself rather than PROJ.ProjNodes.
//
// The /// text is the tooltip (family rule, NodeDocsTests): one-line summary, short remarks, one
// line per pin, plain text. The reasoning lives here.
//
// Reproject reads the SOURCE CRS from the geometry's SRID and never takes it as a pin: the CRS
// travels with the data (docs/DESIGN.md, "The model"). An unknown SRID (0 or -1) is refused, not
// guessed. On any failure the input comes back unchanged with Success false and a reason - the
// same contract as VL.NetTopologySuite's FromWKT - so a patch can be wired before it is right.
// Its first parameter and its return are both Geometry, so VL names the output pin Output (the
// family's fluent rule), like Buffer's.

/// <summary>
/// Nodes that move geometry between coordinate reference systems with PROJ.
/// </summary>
[SkipCategory]
public static class ProjNodes
{
    /// <summary>
    /// Moves a geometry into another coordinate reference system.
    /// </summary>
    /// <remarks>
    /// The geometry's SRID says what CRS it is in now; set it first if it is 0.
    /// Coordinates are x = east: longitude, latitude for geographic systems.
    /// </remarks>
    /// <param name="geometry">The geometry, with the SRID of the CRS it is in.</param>
    /// <param name="toSRID">The EPSG code to move it to, for example 4326 for WGS 84 longitude and latitude.</param>
    /// <param name="success">False when it could not be moved; the geometry then comes out unchanged.</param>
    /// <param name="message">What was done, or why not.</param>
    /// <param name="accuracy">How accurate PROJ says the move is, in metres; -1 when unknown.</param>
    /// <returns>The moved geometry, carrying the new SRID.</returns>
    public static Geometry? Reproject(
        Geometry? geometry,
        [Pin(Name = "To SRID")] int toSRID,
        out bool success,
        out string message,
        out double accuracy)
    {
        if (geometry is null)
        {
            success = false;
            message = "No geometry.";
            accuracy = -1;
            return null;
        }
        success = Engine.Reproject(geometry, toSRID, out var result, out message, out accuracy);
        return result;
    }

    /// <summary>
    /// Says what an EPSG code is.
    /// </summary>
    /// <remarks>
    /// Area Of Use is where the system is meant to be used, in longitude and latitude.
    /// </remarks>
    /// <param name="srid">The EPSG code, for example 28992.</param>
    /// <param name="name">Its name, for example Amersfoort / RD New.</param>
    /// <param name="isGeographic">True for longitude and latitude in degrees, false for a projected system.</param>
    /// <param name="units">The unit of its coordinates, for example metre or degree.</param>
    /// <param name="areaOfUse">Where it is meant to be used, as a longitude and latitude rectangle with SRID 4326.</param>
    /// <param name="success">False when the code is unknown.</param>
    public static void CRS(
        [Pin(Name = "SRID")] int srid,
        out string name,
        out bool isGeographic,
        out string units,
        out Geometry? areaOfUse,
        out bool success)
    {
        success = Engine.Describe(srid, out var info, out var error);
        name = success ? info.Name : error;
        isGeographic = info.IsGeographic;
        units = info.Units;
        areaOfUse = info.AreaOfUse is { } e ? Wgs84.ToGeometry(e) : null;
    }
}
