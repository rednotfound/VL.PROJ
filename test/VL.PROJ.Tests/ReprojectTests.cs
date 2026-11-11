using System;
using NetTopologySuite.Geometries;
using Xunit;

namespace VL.PROJ.Tests;

/// <summary>
/// Reproject against known answers: the RD New origin (measured 2026-11-11, PROJ 9.5.1 and
/// DotSpatial agreeing to 5 mm), Web Mercator's closed form, round trips; and the contract -
/// source CRS from the geometry, unknown refused, input never touched, SRID carried.
/// </summary>
public class ReprojectTests
{
    private static GeometryFactory F(int srid) => new(new PrecisionModel(), srid);

    // The Onze Lieve Vrouwetoren in Amersfoort, origin of the Dutch grid.
    private static readonly Coordinate AmersfoortRd = new(155000, 463000);
    private const double AmersfoortLon = 5.387203508, AmersfoortLat = 52.155172301;

    private static Geometry Reproject(Geometry g, int to, out bool ok, out string message, out double accuracy)
        => ProjNodes.Reproject(g, to, out ok, out message, out accuracy)!;

    [Fact]
    public void The_RD_origin_lands_on_the_Amersfoort_tower()
    {
        var p = Reproject(F(28992).CreatePoint(AmersfoortRd), 4326, out var ok, out var message, out var accuracy);
        Assert.True(ok, message);
        Assert.Equal(AmersfoortLon, p.Coordinate.X, 7);
        Assert.Equal(AmersfoortLat, p.Coordinate.Y, 7);
        Assert.Equal(1, accuracy, 6); // PROJ's stated accuracy for this operation, metres
    }

    [Fact]
    public void Longitude_comes_first_whatever_EPSG_says()
    {
        // EPSG:4326 is officially latitude, longitude. Here x is east, always.
        var p = Reproject(F(28992).CreatePoint(AmersfoortRd), 4326, out _, out _, out _);
        Assert.InRange(p.Coordinate.X, 5, 6);   // longitude
        Assert.InRange(p.Coordinate.Y, 52, 53); // latitude
    }

    [Fact]
    public void The_result_carries_the_target_SRID_and_so_does_its_factory()
    {
        var p = Reproject(F(28992).CreatePoint(AmersfoortRd), 4326, out _, out _, out _);
        Assert.Equal(4326, p.SRID);
        Assert.Equal(4326, p.Factory.SRID);
        Assert.Equal(4326, p.Buffer(0.001).SRID); // a later NTS operation keeps it
    }

    [Fact]
    public void Web_Mercator_matches_its_closed_form()
    {
        const double R = 6378137;
        var lon = 139.7671; var lat = 35.6812; // Tokyo Station
        var p = Reproject(F(4326).CreatePoint(new Coordinate(lon, lat)), 3857, out var ok, out var message, out _);
        Assert.True(ok, message);
        Assert.Equal(R * lon * Math.PI / 180, p.Coordinate.X, 3);
        Assert.Equal(R * Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360)), p.Coordinate.Y, 3);
    }

    [Fact]
    public void A_round_trip_returns_within_a_millimetre()
    {
        var there = Reproject(F(28992).CreatePoint(AmersfoortRd), 4326, out _, out _, out _);
        var back = Reproject(there, 28992, out var ok, out _, out _);
        Assert.True(ok);
        Assert.Equal(AmersfoortRd.X, back.Coordinate.X, 3);
        Assert.Equal(AmersfoortRd.Y, back.Coordinate.Y, 3);
    }

    [Fact]
    public void The_input_is_never_touched()
    {
        var input = F(28992).CreatePoint(AmersfoortRd.Copy());
        Reproject(input, 4326, out _, out _, out _);
        Assert.Equal(155000, input.Coordinate.X);
        Assert.Equal(28992, input.SRID);
    }

    [Fact]
    public void The_same_SRID_is_a_copy_not_the_input()
    {
        var input = F(4326).CreatePoint(new Coordinate(1, 2));
        var output = Reproject(input, 4326, out var ok, out _, out var accuracy);
        Assert.True(ok);
        Assert.NotSame(input, output);
        Assert.Equal(0, accuracy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void An_unknown_source_SRID_is_refused_and_the_input_comes_back(int srid)
    {
        var input = F(srid).CreatePoint(new Coordinate(155000, 463000));
        var output = Reproject(input, 4326, out var ok, out var message, out var accuracy);
        Assert.False(ok);
        Assert.Same(input, output);
        Assert.Contains("SRID", message);
        Assert.Equal(-1, accuracy);
    }

    [Fact]
    public void An_EPSG_code_PROJ_does_not_know_is_refused_with_a_reason()
    {
        var input = F(4326).CreatePoint(new Coordinate(1, 2));
        var output = Reproject(input, 999999, out var ok, out var message, out _);
        Assert.False(ok);
        Assert.Same(input, output);
        Assert.Contains("999999", message);
    }

    [Fact]
    public void Past_Web_Mercator_s_latitude_limit_PROJ_still_answers_so_the_message_warns()
    {
        // Measured: latitude 89 gives y = 30,240,971 where 3857 is defined to +-20,048,966. PROJ
        // does not fail there, so the node cannot rely on it to: the message must say so.
        var p = Reproject(F(4326).CreatePoint(new Coordinate(0, 89)), 3857, out var ok, out var message, out _);
        Assert.True(ok);
        Assert.True(p.Coordinate.Y > 20_048_966);
        Assert.Contains("outside", message);
        Assert.Contains("85.06", message); // PROJ's own name for the area
    }

    [Fact]
    public void Inside_both_areas_of_use_there_is_no_warning()
    {
        Reproject(F(28992).CreatePoint(AmersfoortRd), 4326, out _, out var message, out _);
        Assert.DoesNotContain("outside", message);
    }

    [Fact]
    public void A_result_outside_the_target_CRS_is_warned_about_too()
    {
        // Tokyo Station given to RD New: transforms, but RD New was never meant for it.
        var tokyo = Reproject(F(4326).CreatePoint(new Coordinate(139.7671, 35.6812)), 28992, out var ok, out var message, out _);
        Assert.True(ok, message);
        Assert.Contains("output", message);
        Assert.Contains("EPSG:28992", message);
    }

    [Fact]
    public void A_polygon_with_a_hole_keeps_its_structure()
    {
        var f = F(28992);
        var shell = f.CreateLinearRing(new[] { new Coordinate(155000, 463000), new Coordinate(155100, 463000), new Coordinate(155100, 463100), new Coordinate(155000, 463100), new Coordinate(155000, 463000) });
        var hole = f.CreateLinearRing(new[] { new Coordinate(155040, 463040), new Coordinate(155060, 463040), new Coordinate(155060, 463060), new Coordinate(155040, 463040) });
        var poly = f.CreatePolygon(shell, new[] { hole });
        var result = (Polygon)Reproject(poly, 4326, out var ok, out _, out _);
        Assert.True(ok);
        Assert.Equal(1, result.NumInteriorRings);
        Assert.True(result.IsValid);
        Assert.Equal(result.ExteriorRing.Coordinates[0], result.ExteriorRing.Coordinates[^1]); // still closed
    }

    [Fact]
    public void Z_is_kept_and_a_2D_point_gets_no_invented_height()
    {
        var f = F(28992);
        var p3 = Reproject(f.CreatePoint(new CoordinateZ(155000, 463000, 12.5)), 4326, out var ok3, out _, out _);
        var p2 = Reproject(f.CreatePoint(new Coordinate(155000, 463000)), 4326, out var ok2, out _, out _);
        Assert.True(ok3 && ok2);
        Assert.Equal(12.5, p3.Coordinate.Z, 3);
        Assert.True(double.IsNaN(p2.Coordinate.Z));
    }

    [Fact]
    public void A_null_geometry_is_no_geometry()
    {
        Assert.Null(ProjNodes.Reproject(null, 4326, out var ok, out var message, out _));
        Assert.False(ok);
        Assert.NotEmpty(message);
    }
}
