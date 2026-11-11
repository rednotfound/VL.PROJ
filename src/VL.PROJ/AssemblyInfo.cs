using System.Runtime.CompilerServices;
using VL.Core.Import;

[assembly: InternalsVisibleTo("VL.PROJ.Tests")]

// Without this every public static method is demoted to a raw .NET reflection node, which the
// NodeBrowser hides; the package loads, compiles and packs with zero warnings regardless. Nine
// VL.GIS releases shipped that way (family rule 1, docs in VL.NetTopologySuite's RULES.md).
// Category = namespace minus this prefix: VL.PROJ gives PROJ.
[assembly: ImportAsIs(Namespace = "VL")]
