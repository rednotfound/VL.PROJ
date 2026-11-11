# CLAUDE.md

Guidance for Claude Code working in this repository.

## Project overview

**VL.PROJ** puts [PROJ](https://proj.org) behind vvvv gamma nodes: it moves NetTopologySuite
geometry from one coordinate reference system to another and says how well that went. Nothing
about drawing, maps or file formats. It is a member of the VL.NetTopologySuite family
(`D:\2026_Projects\vl-nettopologysuite` is the centre; the family's memory lives there).

**Read first:**
- [docs/DESIGN.md](docs/DESIGN.md): the engine, the packaging, the model and the proposed nodes.
- VL.NetTopologySuite's `docs/CRS-RESEARCH.md`: the concepts, libraries and measurements behind it.
- VL.NetTopologySuite's `docs/RULES.md`: the family's packaging and runtime rules, each with its
  cost. Rule 1 (`[assembly: ImportAsIs]`) and rule 2 (an upstream library must be a package in a
  package repository) apply here exactly as there.

**State (2026-11-11): started.**
- Two nodes, `Reproject` and `CRS`, with 23 tests (`dotnet test`).
- Not packaged yet: no `.vl`, no nuspec, no build script, no help patch.
- The node names and the version-one scope are proposals the maintainer has not confirmed
  (DESIGN, "Open questions").

## What is different from the siblings: SharpProj is bundled, not a dependency

PROJ comes through SharpProj (C++/CLI). The siblings take their libraries as NuGet dependencies;
this one cannot (measured 2026-11-11, inside vvvv 7.4):

- **`ijwhost.dll` must sit beside `SharpProj.dll`.** Neither SharpProj's packages nor vvvv ship it.
  Without it: `FileNotFoundException … SharpProj.dll. 找不到指定的模块` (negative-tested in vvvv).
  The csproj takes it from Microsoft's apphost pack by `PackageDownload` (MIT).
- **Build with `RuntimeIdentifier win-x64`**, src and tests both. Without it the output holds
  SharpProj's 5.7 MB reference stub, which cannot run, instead of the 7 MB real build.
- **`proj.db`** (EPSG database, from the `SharpProj` package) sits beside the assembly. PROJ
  finds it there with no `PROJ_DATA`.
- So all of them go into *our* `lib\net8.0`, and **no SharpProj type may appear on a pin.** Pins
  carry NTS `Geometry`, `int`, `double`, `string` and `bool`.

## Rules carried from the family that bite here

- **A static node runs 60 times a second.** PROJ objects are built once and cached (`Engine`),
  failures too, under one lock (PROJ is not thread-safe).
- **Coordinates are copied.** `Reproject` transforms a copy made by the target factory and never
  touches its input. That also makes the result's SRID survive later NTS operations.
- **x = east, always.** Every CRS is axis-normalised, so EPSG:4326 is lon/lat here.
- **The CRS travels on the geometry** (its SRID). It is never a node setting and never global.
  SRID 0 or -1 is refused, not guessed.
- **Never silent.** PROJ returns finite nonsense outside a CRS's area of use: latitude 89 to Web
  Mercator gives y = 30 million metres. So the message warns. Only infinity or NaN fails.
- **Tooltips are short** (`NodeDocsTests`, copied verbatim from the siblings); reasoning goes in
  `//` comments.
- **Commit, do not push.** The maintainer decides when anything reaches a remote.

## Commands

```powershell
dotnet build src\VL.PROJ\VL.PROJ.csproj -c Release
dotnet test test\VL.PROJ.Tests\VL.PROJ.Tests.csproj
```

## Layout

```
vl-proj/
├── docs/DESIGN.md
├── src/VL.PROJ/
│   ├── ProjNodes.cs     # PROJ - Reproject, CRS (the public nodes)
│   ├── Engine.cs        # everything that touches PROJ: context, caches, lock, messages
│   ├── NtsBridge.cs     # TransformFilter: a PROJ transform over an NTS geometry's coordinates
│   ├── Wgs84.cs         # the SRID 4326 factory for Area Of Use
│   └── AssemblyInfo.cs  # ImportAsIs
└── test/VL.PROJ.Tests/  # ReprojectTests, CrsTests, NodeDocsTests
```
