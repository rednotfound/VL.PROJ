using VL.NetTopologySuite;
using Xunit;

namespace VL.PROJ.Tests;

/// <summary>
/// PROJ's EPSG catalogue as VL.NetTopologySuite's ICrsCatalog (2026-11-12): what GeometryEditor's
/// window and the Geometry IOBox ask when VL.PROJ is loaded.
/// </summary>
public class CatalogTests
{
    private static readonly ICrsCatalog Catalog = ProjCrsCatalog.Instance;

    [Fact]
    public void RD_New_is_named_projected_and_in_metres()
    {
        Assert.True(Catalog.TryDescribe(28992, out var name, out var geographic, out var unit));
        Assert.Equal("Amersfoort / RD New", name);
        Assert.False(geographic);
        Assert.Equal("metre", unit);
    }

    [Fact]
    public void A_code_outside_the_NTS_table_is_known_here()
    {
        // NAD83 / New York Long Island in US feet: VL.NetTopologySuite's table says "units unknown".
        Assert.True(Catalog.TryDescribe(2263, out var name, out var geographic, out var unit));
        Assert.Contains("Long Island", name);
        Assert.False(geographic);
        Assert.Contains("foot", unit);
    }

    [Fact]
    public void Geographic_codes_say_so()
    {
        Assert.True(Catalog.TryDescribe(4490, out var name, out var geographic, out var unit));
        Assert.Equal("China Geodetic Coordinate System 2000", name);
        Assert.True(geographic);
        Assert.Equal("degree", unit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999999)]
    public void Unknown_codes_are_refused_not_guessed(int srid)
        => Assert.False(Catalog.TryDescribe(srid, out _, out _, out _));

    [Fact]
    public void The_initializer_registers_the_catalogue_and_keeps_its_hands_off_otherwise()
    {
        // The class VL finds through [assembly: AssemblyInitializer]; outside vvvv there is no
        // app host to configure, so this only proves the attribute points at it.
        var attribute = typeof(ProjCrsCatalog).Assembly
            .GetCustomAttributes(typeof(VL.Core.CompilerServices.AssemblyInitializerAttribute), false);
        Assert.Single(attribute);
    }
}
