# VL.PROJ

**Coordinate transformation for NetTopologySuite geometry in [vvvv gamma](https://vvvv.org), with
[PROJ](https://proj.org)** — the engine QGIS, GDAL and PostGIS use.

> **Status: `0.0.1-alpha` on nuget.org since 2026-11-14 — the first release, a prerelease, early,
> and it works.** Two nodes, `Reproject` and `CRS`, in the category `PROJ`; 30 tests, 3 help
> patches, every node opens one on **F1**. Seen working in vvvv gamma 7.4: 44 building footprints
> from the Dutch grid (EPSG:28992) moved to longitude and latitude sit exactly on OpenStreetMap's
> buildings at TU Delft ([the picture](docs/acceptance-delft-2026-11-11.png)).
>
> It is part of the [VL.NetTopologySuite](https://github.com/rednotfound/VL.NetTopologySuite)
> family: geometry comes from VL.NetTopologySuite, a map from VL.Mapsui, and this package moves
> the geometry between the two coordinate systems.

---

## Install and your first transformation

**1. Install.** In vvvv gamma 7.x: Quad menu → **Manage Nugets → Commandline**, then

```
nuget install VL.PROJ -pre
```

`-pre` is needed because every version so far is a prerelease. The install brings
VL.NetTopologySuite and NetTopologySuite along. PROJ 9.5.1 and its EPSG database come inside
the package (through [SharpProj](https://github.com/AmpScm/SharpProj)), so nothing else needs
installing. **Windows x64 only**, as vvvv gamma is, and it needs the Visual C++ 2015-2022 runtime,
which most Windows machines already have.

**2. See it work.** Press **F1** with nothing selected to open the Help Browser, go to **Learn**
and find VL.PROJ. `HowTo Move a geometry to another CRS` takes a square on the Dutch grid to
longitude and latitude and says how accurate that is; `HowTo Look up a CRS` names an EPSG code and
draws where it is meant to be used.

**3. Use it in your own patch.** In your document: **Document menu → Dependencies → NuGets**,
right-click **VL.PROJ** to reference it. The smallest chain:

```
FromWKT  →  SetSRID (28992)  →  Reproject (To SRID 4326)  →  ToWKT
```

**The CRS travels on the geometry, as its SRID.** `SetSRID` (in VL.NetTopologySuite) says what the
numbers are in; `Reproject` reads it and moves the geometry to another EPSG code. An SRID of 0
means "unset" and is refused, never guessed.

**4. Updating.** Run the same command again and restart vvvv. Within about 30 minutes of a
release NuGet may still remember the old version list; ask for the version by name
(`-version 0.0.1-alpha`) or wait.

---

## The nodes

### `PROJ`

| Node | in → out |
|---|---|
| `Reproject` | `Geometry`, To SRID → `Geometry` with the new SRID, **Success**, **Message**, **Accuracy** (metres, -1 when unknown) |
| `CRS` | SRID → **Name**, **Is Geographic**, **Units**, **Area Of Use** (a lon/lat rectangle), **Success** |

- **x is east, always.** Every coordinate system is used with its axes normalised, so EPSG:4326
  is longitude, latitude here, the way GeoJSON, Mapsui and most software write it.
- **Never silent.** PROJ gives finite nonsense outside the area a CRS is meant for (latitude 89 in
  Web Mercator is 30 million metres north), so `Message` warns when a coordinate is outside it.
  Only infinity or NaN fails.
- **The input is never touched.** `Reproject` moves a copy.
- **PROJ objects are built once and cached**, because a node runs sixty times a second.

**With VL.PROJ installed, VL.NetTopologySuite's `GeometryEditor` window and drawn Geometry IOBox
name any EPSG code** (for example `EPSG:2263 NAD83 / New York Long Island (ftUS) · US survey
foot`), not only the short table VL.NetTopologySuite carries itself.

## Help patches

| Patch | |
|---|---|
| `Explanation Overview of available nodes` | both nodes, one line each |
| `HowTo Move a geometry to another CRS` | `Reproject`: the Dutch grid to longitude and latitude |
| `HowTo Look up a CRS` | `CRS`: a name, its units and where it is meant to be used |

## What it does not do

- **Draw.** VL.NetTopologySuite's IOBoxes, VL.Mapsui and VL.Skia do that.
- **Read files.** VL.NetTopologySuite (WKT) and VL.NetTopologySuite.GeoJSON do that.
- **Guess a CRS.** A geometry with SRID 0 is refused with a message saying to set one.

## Building it

```powershell
# vvvv must be closed: it holds the staged assemblies open
dotnet test test\VL.PROJ.Tests\VL.PROJ.Tests.csproj
.\build.ps1                       # build + stage dist\
.\pack.ps1                        # dist\feed\VL.PROJ.<version>.nupkg
.\tools\Compile-HelpPatches.ps1   # vvvvc on every help patch, then reads the generated C#
.\tools\Test-Install.ps1          # install the nupkg from dist\feed and compile the installed help
```

Why SharpProj is bundled rather than a dependency (it is C++/CLI and needs `ijwhost.dll` beside
it), and the rest of the design, is in [docs/DESIGN.md](docs/DESIGN.md).

## Licence

MIT, see [LICENSE](LICENSE). What the package bundles comes under its own terms, with the full
texts in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md): SharpProj (Apache 2.0), PROJ (MIT) and
the libraries linked into it, `ijwhost.dll` (MIT), and the EPSG Geodetic Parameter Dataset, which
is owned by IOGP and used under its terms of use.
