using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using SharpProj;
using SharpProj.Proj;

namespace VL.PROJ;

// Everything that touches PROJ, behind one lock.
//
// - One ProjContext for the process. PROJ objects are not thread-safe, and a vvvv patch may call
//   a node from more than one thread (a ForEach region in parallel, an async loop), so every use
//   is under Gate. The cost is a monitor enter per call; a transform is microseconds.
// - CRSs and transforms are built once and cached, failures included: a static node runs sixty
//   times a second (family memory: VL evaluates every frame), and building a PROJ transform per
//   frame - or retrying a bad EPSG code per frame - would be the whole cost of the patch.
// - Every CRS is axis-normalised (WithAxisNormalized): lon/lat and easting/northing, x = east,
//   whatever EPSG says officially. EPSG:4326 is officially lat/lon; GeoJSON, NTS, Mapsui and
//   this family are lon/lat, so that is what goes in and comes out.
// - Network grids are off (the context's default): an offline patch gives the same answer every
//   time. docs/DESIGN.md.
//
// The caches live for the process. A few hundred CRSs is the most any patch would ask for.
//
// Area of use (measured 2026-11-11): SharpProj's UsageArea Min/Max are in the CRS's OWN units -
// RD New gives metres (634 .. 284300), not degrees. And PROJ does not fail outside it: WGS 84 ->
// Web Mercator at latitude 89 gives y = 30,240,971, at 90 y = 242,528,680, all finite, where the
// CRS is defined to +-20,048,966. So a coordinate outside the area of use is not an error (a point
// a few km over the Dutch border is still well served by RD New) but it is never silent: the
// message says so. Only an infinite or NaN result is a failure.

/// <summary>What an EPSG code is, for the CRS node.</summary>
internal sealed record CrsInfo(string Name, bool IsGeographic, string Units, Envelope? AreaOfUse);

internal static class Engine
{
    private static readonly object Gate = new();
    private static ProjContext? _context;
    private static readonly Dictionary<int, object> Crss = new();                 // CoordinateReferenceSystem or string (the error)
    private static readonly Dictionary<int, (Envelope? Area, string Name)> Areas = new(); // in the CRS's own units
    private static readonly Dictionary<(int, int), object> Transforms = new();    // CoordinateTransform or string
    private static readonly ConcurrentDictionary<(int, PrecisionModel), GeometryFactory> Factories = new();

    private static ProjContext Context => _context ??= new ProjContext();

    /// <summary>The EPSG database's version, for messages and tests.</summary>
    public static string EpsgVersion
    {
        get { lock (Gate) return Context.EpsgVersion?.ToString() ?? "?"; }
    }

    /// <summary>Unknown means 0 (the family's "unset") or negative (NTS's WKTReader gives -1).</summary>
    public static bool IsUnknown(int srid) => srid <= 0;

    private static bool TryCrs(int srid, out CoordinateReferenceSystem crs, out string error)
    {
        if (!Crss.TryGetValue(srid, out var cached))
        {
            try
            {
                cached = CoordinateReferenceSystem.CreateFromEpsg(srid, Context).WithAxisNormalized();
            }
            catch (Exception e)
            {
                cached = $"EPSG:{srid} is not a coordinate reference system in PROJ's database ({e.Message.Trim()}).";
            }
            Crss[srid] = cached;
        }
        if (cached is CoordinateReferenceSystem c)
        {
            crs = c;
            error = "";
            return true;
        }
        crs = null!;
        error = (string)cached;
        return false;
    }

    // The area of use of a CRS, in that CRS's own units, and its name; null when none is recorded.
    private static (Envelope? Area, string Name) AreaOf(int srid, CoordinateReferenceSystem crs)
    {
        if (Areas.TryGetValue(srid, out var known)) return known;
        (Envelope?, string) found = (null, "");
        try
        {
            var u = crs.UsageArea;
            if (u is not null) found = (new Envelope(u.MinX, u.MaxX, u.MinY, u.MaxY), u.Name ?? "");
        }
        catch (Exception) { /* none recorded */ }
        Areas[srid] = found;
        return found;
    }

    private static string? OutsideNote(int srid, CoordinateReferenceSystem crs, Envelope coordinates, string which)
    {
        var (area, name) = AreaOf(srid, crs);
        if (area is null || area.Contains(coordinates)) return null;
        return $" Careful: some {which} coordinates lie outside where EPSG:{srid} is meant to be used ({name.TrimEnd('.')}).";
    }

    private static bool TryTransform(int from, int to, out CoordinateTransform transform, out string error)
    {
        if (!Transforms.TryGetValue((from, to), out var cached))
        {
            if (!TryCrs(from, out var src, out error) || !TryCrs(to, out var dst, out error))
                cached = error;
            else
            {
                try { cached = CoordinateTransform.Create(src, dst, Context); }
                catch (Exception e) { cached = $"PROJ has no way from EPSG:{from} to EPSG:{to} ({e.Message.Trim()})."; }
            }
            Transforms[(from, to)] = cached;
        }
        if (cached is CoordinateTransform t)
        {
            transform = t;
            error = "";
            return true;
        }
        transform = null!;
        error = (string)cached;
        return false;
    }

    /// <summary>
    /// A copy of <paramref name="geometry"/> in EPSG:<paramref name="toSrid"/>, its SRID set to it.
    /// False with the input unchanged and a reason when it cannot be done.
    /// </summary>
    public static bool Reproject(Geometry geometry, int toSrid, out Geometry result, out string message, out double accuracy)
    {
        result = geometry;
        accuracy = -1;
        var from = geometry.SRID;

        if (IsUnknown(from))
        {
            message = $"The geometry's SRID is {from}, which says nothing about its coordinates. Give it the EPSG code they are in first.";
            return false;
        }
        if (IsUnknown(toSrid))
        {
            message = $"The target SRID {toSrid} is not an EPSG code.";
            return false;
        }

        var factory = Factories.GetOrAdd((toSrid, geometry.PrecisionModel),
            k => new GeometryFactory(k.Item2, k.Item1, geometry.Factory.CoordinateSequenceFactory));

        if (from == toSrid)
        {
            // Nothing to move; still a copy, so the caller's geometry is never handed back to be mutated.
            result = factory.CreateGeometry(geometry);
            accuracy = 0;
            message = $"Already in EPSG:{toSrid}.";
            return true;
        }

        lock (Gate)
        {
            if (!TryTransform(from, toSrid, out var transform, out var error))
            {
                message = error;
                return false;
            }

            // CreateGeometry is a deep copy made by the target factory, so the result carries the
            // target SRID - also through any later NTS operation, which builds with the factory.
            var copy = factory.CreateGeometry(geometry);
            var filter = new TransformFilter(transform);
            try
            {
                copy.Apply(filter);
            }
            catch (Exception e)
            {
                message = $"PROJ could not transform these coordinates from EPSG:{from} to EPSG:{toSrid} ({e.Message.Trim()}).";
                return false;
            }
            if (filter.Failed)
            {
                message = $"Some coordinates lie outside what EPSG:{toSrid} can express (PROJ returned infinity).";
                return false;
            }
            copy.GeometryChanged();

            result = copy;
            accuracy = transform.Accuracy is double a && double.IsFinite(a) ? a : -1;
            message = accuracy >= 0
                ? $"EPSG:{from} to EPSG:{toSrid}, accurate to about {accuracy:0.###} m."
                : $"EPSG:{from} to EPSG:{toSrid}, accuracy unknown.";
            if (!geometry.IsEmpty && TryCrs(from, out var src, out _) && TryCrs(toSrid, out var dst, out _))
                message += OutsideNote(from, src, geometry.EnvelopeInternal, "input")
                         + OutsideNote(toSrid, dst, copy.EnvelopeInternal, "output");
            return true;
        }
    }

    private static void Include(Envelope lonLat, CoordinateTransform t, double x, double y)
    {
        try
        {
            var p = t.Apply(new PPoint(x, y));
            if (double.IsFinite(p.X) && double.IsFinite(p.Y)) lonLat.ExpandToInclude(p.X, p.Y);
        }
        catch (Exception) { /* a corner PROJ cannot take: leave it out */ }
    }

    /// <summary>What EPSG:<paramref name="srid"/> is, or false and a reason.</summary>
    public static bool Describe(int srid, out CrsInfo info, out string error)
    {
        info = new CrsInfo("", false, "", null);
        if (IsUnknown(srid))
        {
            error = $"SRID {srid} is not an EPSG code.";
            return false;
        }
        lock (Gate)
        {
            if (!TryCrs(srid, out var crs, out error)) return false;

            var geographic = false;
            var units = "";
            try
            {
                var cs = crs.CoordinateSystem;
                geographic = cs.CoordinateSystemType == CoordinateSystemType.Ellipsoidal;
                if (cs.Axis.Count > 0) units = cs.Axis[0].UnitName ?? "";
            }
            catch (Exception)
            {
                // A compound CRS (EPSG:7415 = RD New + NAP) has no single coordinate system;
                // its horizontal part says what x and y are.
                try
                {
                    var horizontal = crs.GeodeticCRS;
                    geographic = crs.Type is ProjType.Geographic2DCrs or ProjType.Geographic3DCrs;
                    units = horizontal?.CoordinateSystem.Axis[0].UnitName ?? "";
                }
                catch (Exception) { /* leave unknown */ }
            }

            // The recorded area is in the CRS's own units; a geographic CRS's are already lon/lat,
            // a projected one's go through PROJ along a densified outline (a projected rectangle's
            // edges are curves in lon/lat, so the corners alone would cut it short).
            Envelope? area = null;
            var (own, _) = AreaOf(srid, crs);
            if (own is not null)
            {
                if (geographic) area = own;
                else if (TryTransform(srid, Wgs84.Srid, out var toLonLat, out _))
                {
                    var lonLat = new Envelope();
                    const int steps = 16;
                    for (var i = 0; i <= steps; i++)
                    {
                        var s = (double)i / steps;
                        Include(lonLat, toLonLat, own.MinX + s * own.Width, own.MinY);
                        Include(lonLat, toLonLat, own.MinX + s * own.Width, own.MaxY);
                        Include(lonLat, toLonLat, own.MinX, own.MinY + s * own.Height);
                        Include(lonLat, toLonLat, own.MaxX, own.MinY + s * own.Height);
                    }
                    if (!lonLat.IsNull) area = lonLat;
                }
            }

            info = new CrsInfo(crs.Name ?? $"EPSG:{srid}", geographic, units, area);
            return true;
        }
    }
}
