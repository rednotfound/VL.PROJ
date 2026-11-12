using System;
using VL.Core;
using VL.Core.CompilerServices;
using VL.NetTopologySuite;

[assembly: AssemblyInitializer(typeof(_VL_.PROJ.Initializer))]

// The _VL_ namespace keeps this class out of the NodeBrowser: ImportAsIs(Namespace = "VL") imports
// only VL.*. Both are VL.IO.Redis's (shipped with vvvv 7.4): [assembly: AssemblyInitializer(typeof(
// _VL_.IO.Redis.Initializer))].
namespace _VL_.PROJ;

// Registers PROJ's EPSG catalogue for VL.NetTopologySuite (see ProjCrsCatalog.cs). Into
// AppHost.GLOBAL's services, not the app host passed in: the editor window and the Geometry IOBox
// that ask for it run in VL.NetTopologySuite's HDE companion, an app host of its own that never
// loads this package, and a per-app registry falls back to the global one, not to another app's.
// Configure runs once per app host that loads this package; registering the same instance again
// is harmless.
//
// Guarded: VL.PROJ's own nodes need none of this, so a failure here (an older VL.NetTopologySuite
// without ICrsCatalog, no global host) must not take the package down with it.

/// <summary>Registers PROJ's EPSG catalogue for VL.NetTopologySuite.</summary>
public sealed class Initializer : AssemblyInitializer<Initializer>
{
    /// <summary>Registers the catalogue in the global service registry.</summary>
    public override void Configure(AppHost appHost)
    {
        base.Configure(appHost);
        try
        {
            AppHost.Global.Services.RegisterService<ICrsCatalog>(global::VL.PROJ.ProjCrsCatalog.Instance);
        }
        catch (Exception e)
        {
            appHost.DefaultLogger?.LogWarningSafe($"VL.PROJ could not offer its CRS catalogue to VL.NetTopologySuite: {e.Message}");
        }
    }
}

internal static class LoggerExtensions
{
    // Logging is a nicety; never let it throw out of an initializer.
    public static void LogWarningSafe(this Microsoft.Extensions.Logging.ILogger logger, string message)
    {
        try { Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger, message); }
        catch (Exception) { }
    }
}
