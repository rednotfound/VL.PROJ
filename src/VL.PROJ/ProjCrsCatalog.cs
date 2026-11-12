using VL.NetTopologySuite;

namespace VL.PROJ;

// PROJ's EPSG catalogue, offered to VL.NetTopologySuite (2026-11-12, step 2 of the SRID plan): with
// it, GeometryEditor's window names any CRS ("EPSG:28992 Amersfoort / RD New · metres") and the
// Geometry IOBox draws any geographic CRS at true proportions, not only the codes in that package's
// short built-in table. The interface is that package's; registration is Initializer.cs.
//
// Engine.Describe does the work under its lock and caches the CRS; VL.NetTopologySuite caches each
// answer per code, so PROJ is asked once per code, not per frame.

/// <summary>PROJ's EPSG catalogue, as VL.NetTopologySuite's ICrsCatalog.</summary>
internal sealed class ProjCrsCatalog : ICrsCatalog
{
    public static readonly ProjCrsCatalog Instance = new();

    public bool TryDescribe(int srid, out string name, out bool isGeographic, out string unitName)
    {
        if (Engine.Describe(srid, out var info, out _))
        {
            // Engine axis-normalises every CRS, and PROJ appends that to the name (measured: "China
            // Geodetic Coordinate System 2000 (with axis order normalized for visualization)"); a
            // status bar wants the EPSG name.
            name = info.Name.Replace(" (with axis order normalized for visualization)", "");
            isGeographic = info.IsGeographic;
            unitName = info.Units;
            return true;
        }
        name = "";
        isGeographic = false;
        unitName = "";
        return false;
    }
}
