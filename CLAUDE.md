# CLAUDE.md

Guidance for Claude Code working in this repository.

## Project overview

**VL.PROJ** puts [PROJ](https://proj.org) behind vvvv gamma nodes: it moves NetTopologySuite
geometry from one coordinate reference system to another and says how well that went. Nothing
about drawing, maps or file formats. It is a member of the VL.NetTopologySuite family
(`D:\2026_Projects\vl-nettopologysuite` is the centre; the family's memory lives there).

**Read first:**
- [docs/DESIGN.md](docs/DESIGN.md): the engine, the packaging, the model and the proposed nodes.
- [docs/CRS-RESEARCH.md](docs/CRS-RESEARCH.md): the concepts, libraries and measurements behind it
  (written in VL.NetTopologySuite on 2026-11-11, moved here the same day; it is maintained here).
- VL.NetTopologySuite's `docs/RULES.md`: the family's packaging and runtime rules, each with its
  cost. Rule 1 (`[assembly: ImportAsIs]`) and rule 2 (an upstream library must be a package in a
  package repository) apply here exactly as there.

**State (2026-11-11): version one built and seen working in vvvv 7.4.** Nothing is published, and
the repository has no remote.
- Two nodes, `Reproject` and `CRS`, in category `PROJ`.
- 23 tests.
- Packaged: `VL.PROJ.vl`, nuspec, `build.ps1` / `pack.ps1`, and a 4.7 MB nupkg.
- Three help patches: the Explanation, `HowTo Move a geometry to another CRS` and `HowTo Look up a CRS`.

The maintainer confirmed on 2026-11-11: the names `Reproject` and `CRS`; version one is those
two; `SetSRID` goes into VL.NetTopologySuite.

| check | state |
|---|---|
| `dotnet test` | ✅ 23 |
| `tools\Test-VLPackage.ps1`, `tools\Test-VLPatch.ps1` | ✅ |
| `tools\Compile-HelpPatches.ps1` (`vvvvc`, generated C# read) | ✅ 3 of 3; `Reproject`, `CRS` and the VL.NetTopologySuite nodes resolve; `To SRID` is wired; no NU1101 |
| vvvv GUI | ✅ both HowTos computed, captured on 2026-11-11: RD square to lon/lat with accuracy 1 m; SRID 0 refused; CRS 28992 = Amersfoort / RD New, metre, area 4.3° × 3.0° |
| F1 help flags | set by the generator (High on each HowTo's node), not audited: this repository's `Test-VLPatch` is vl-geojson's, which has no F1 audit. F1 not pressed yet |
| `Test-Install` | not run yet |

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

The tools are vl-geojson's, carried with their package names changed. `HelpPatchGen.ps1` is
VL.NetTopologySuite's. The launcher adds vl-nettopologysuite's `dist` and `deps`, because the help
patches use its nodes and drawn IOBoxes.

```powershell
# vvvv must be CLOSED for build/pack/compile - they refuse otherwise
dotnet test test\VL.PROJ.Tests\VL.PROJ.Tests.csproj
.\build.ps1                       # build + stage dist\ (our dll + the four bundled PROJ files + help)
.\tools\Test-VLPackage.ps1        # static package checks
.\tools\Test-VLPatch.ps1          # structural checks on every .vl and Help.xml
.\pack.ps1                        # dist\feed\VL.PROJ.<version>.nupkg
.\tools\Compile-HelpPatches.ps1   # vvvvc on every help patch, then READS the generated C#
.\tools\Open-HelpPatch.ps1 "Move" # open one in vvvv; pid in %TEMP%\vl-proj-vvvv.pid, close only that pid
```

Help patches are scaffolded once with `tools\HelpPatchGen.ps1`. After the GUI check the `.vl` is
the truth. The family's help style applies: a 20pt heading, short `<` notes, cells and not hubs
(VL.NetTopologySuite's `docs/HELP-PATCH-STYLE.md`).

## Layout

```
vl-proj/
├── VL.PROJ.vl / .nuspec   # the package; the nuspec ships the four bundled PROJ files
├── THIRD-PARTY-NOTICES.md # SharpProj, PROJ, EPSG, ijwhost
├── build.ps1, pack.ps1, Open-HelpPatch.cmd, tools/
├── help/VL.PROJ/          # 3 help patches + Help.xml
├── docs/DESIGN.md, docs/CRS-RESEARCH.md
├── src/VL.PROJ/
│   ├── ProjNodes.cs     # PROJ - Reproject, CRS (the public nodes)
│   ├── Engine.cs        # everything that touches PROJ: context, caches, lock, messages
│   ├── NtsBridge.cs     # TransformFilter: a PROJ transform over an NTS geometry's coordinates
│   ├── Wgs84.cs         # the SRID 4326 factory for Area Of Use
│   └── AssemblyInfo.cs  # ImportAsIs
└── test/VL.PROJ.Tests/  # ReprojectTests, CrsTests, NodeDocsTests
```
