using NetTopologySuite.Geometries;
using Xunit;

namespace VL.PROJ.Tests;

/// <summary>The CRS node: what an EPSG code is, read from PROJ's database.</summary>
public class CrsTests
{
    [Fact]
    public void RD_New_is_projected_in_metres_and_its_area_of_use_holds_Amersfoort()
    {
        ProjNodes.CRS(28992, out var name, out var geographic, out var units, out var area, out var ok);
        Assert.True(ok, name);
        Assert.Contains("RD New", name);
        Assert.False(geographic);
        Assert.Equal("metre", units);
        Assert.NotNull(area);
        Assert.Equal(4326, area!.SRID);
        Assert.True(area.Contains(area.Factory.CreatePoint(new Coordinate(5.3872, 52.1552))));
    }

    [Fact]
    public void WGS_84_is_geographic_in_degrees()
    {
        ProjNodes.CRS(4326, out var name, out var geographic, out var units, out _, out var ok);
        Assert.True(ok, name);
        Assert.Contains("WGS 84", name);
        Assert.True(geographic);
        Assert.Equal("degree", units);
    }

    [Fact]
    public void Japan_s_JGD_is_there_under_its_current_name()
    {
        // EPSG renamed 6668 from JGD2011 to JGD2024 on 2025-04-01 (no change of definition).
        ProjNodes.CRS(6668, out var name, out var geographic, out _, out _, out var ok);
        Assert.True(ok, name);
        Assert.Contains("JGD", name);
        Assert.True(geographic);
    }

    [Fact]
    public void A_compound_CRS_is_described_too()
    {
        // EPSG:7415 = RD New + NAP height, what 3D BAG (vl-cityjson) is in.
        ProjNodes.CRS(7415, out var name, out _, out _, out _, out var ok);
        Assert.True(ok, name);
        Assert.Contains("RD New", name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999999)]
    public void An_unknown_code_says_so(int srid)
    {
        ProjNodes.CRS(srid, out var name, out _, out _, out var area, out var ok);
        Assert.False(ok);
        Assert.Contains(srid.ToString(), name);
        Assert.Null(area);
    }
}
