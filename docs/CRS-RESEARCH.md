# Coordinate reference systems: research before a package

2026-11-11. The maintainer asked for this after the GeometryEditor: real data comes in a
coordinate reference system (CRS), the editor's grid is plain numbers, and coordinate systems are
"very, very confusing". This document has two jobs. It explains the concepts plainly enough to
learn from, and it gathers what the libraries and the community offer, so that the package that
follows is designed rather than guessed.

**Status: the research that led to VL.PROJ.** It was written in VL.NetTopologySuite's
repository and moved here the same day (2026-11-11, the maintainer: that repository need not
maintain it). It is maintained here now. Paths like `docs/ARCHITECTURE.md` below mean
VL.NetTopologySuite's.

**Decisions taken since it was written** (DESIGN.md has the details):
- the library is SharpProj, bundled;
- the package is VL.PROJ, with category PROJ;
- version one has two nodes, `Reproject` and `CRS`;
- `SetSRID` goes into VL.NetTopologySuite;
- GCJ-02 / BD-09 are out of scope for now.

Method. There were three parallel surveys:
- the .NET libraries, read on nuget.org (APIs and nupkg contents) and GitHub;
- the vvvv community and Mapsui;
- the concepts and the UX precedent, read on EPSG, PROJ, QGIS, PostGIS, RFC 7946 and Esri.

The family's own repositories were also grepped locally. Sources are linked inline. Statements
that were inferred rather than read say so.

---

## 1. The concepts, in the order they nest

```
ellipsoid            the Earth's shape: a and 1/f              GRS80, WGS 84
  └ datum            that ellipsoid fixed to the Earth          WGS 84, CGCS2000, JGD2011, ETRS89
      └ geographic CRS   datum + latitude/longitude, degrees    EPSG:4326, 4490, 6668, 4258
          └ projected CRS  geographic CRS + a map projection   EPSG:3857, 326xx (UTM), 6669..6687,
                           (method + parameters), metres        28992 (RD New), 4547 (CGCS2000 GK)
vertical CRS         heights only                               NAP height, EGM2008
compound CRS         horizontal + vertical                      EPSG:7415 = RD New + NAP
```

- **A projected CRS contains its geographic CRS.** epsg.io shows it directly: EPSG:6669 is base
  CRS 6668 plus a Transverse Mercator with lat₀ 33°, lon₀ 129.5° and k 0.9999
  (https://epsg.io/6669). So "first project, then choose a coordinate system" is backwards: you
  never project into a void. The only two questions are always:
  1. *what CRS are these numbers in?*
  2. *what CRS do I want them in?*
- **An EPSG code names a whole object**: a CRS, a datum, or a transformation between two CRSs.
  "Converting between coordinate systems" means going from one code to another.
- **Even 2D needs a projection.** Drawing lon/lat straight as x/y is itself a projection, the
  "plate carrée". It stretches shapes east–west by 1/cos(latitude). Our GeometryEditor does
  exactly that today.
- **Conversion and transformation are different things.** Changing projection inside one datum
  is exact maths, a *conversion*. Changing datum is a *transformation*: its parameters are
  estimated and it carries an accuracy. PROJ: "Operations that do exert a change in reference
  frame are called transformations" (https://proj.org/en/stable/operations/index.html).
- **The text formats:**
  - **WKT2** (ISO 19162) is the full description.
  - **PROJJSON** is the same model in JSON.
  - **PROJ strings** (`+proj=tmerc +lon_0=…`) lose information. PROJ's FAQ says converting WKT
    to a PROJ string "will in most cases cause a loss of information".

### The traps

| trap | what happens | defence |
|---|---|---|
| **Axis order** | EPSG:4326 is officially *latitude, longitude* (epsg.io/4326). GeoJSON (RFC 7946), NTS, Mapsui and nearly all GIS code use *longitude, latitude*. GDAL 3 honours lat-first by default; PROJ offers `proj_normalize_for_visualization` | x = east, always, said in every tooltip |
| **SRID 0 / unset** | NTS mixes SRIDs silently: 4326 ∩ 0 returns 4326 (measured, CLAUDE.md) | a transform refuses an unset source SRID |
| **Label vs transform** | PostGIS separates them. `ST_SetSRID` "simply sets the meta data", `ST_Transform` changes coordinates (https://postgis.net/docs/ST_SetSRID.html). QGIS: changing a layer's CRS "does not alter the underlying data source" | two nodes that cannot be confused; never one node with a mode |
| **Measuring in degrees** | `Area` on lon/lat is in square degrees; NTS is planar only | transform to a metric CRS, or measure geodesically |
| **Web Mercator (3857)** | EPSG: "Not a recognised geodetic system", scope "Web mapping and visualisation", valid to ±85.06° (epsg.io/3857). Areas grow about 1/cos²(lat), roughly 4× at 60° | display only, never measure in it |
| **Datum differences are small** | WGS 84 vs ETRS89, CGCS2000 or JGD2011 differs by centimetres to about 1–2 m | irrelevant for a map or a city-scale piece; relevant for survey or cadastre. Say which a transform did |

### China and Japan

- **CGCS2000** (EPSG:4490) is China's official system since 2008 (epsg.io/4490), within
  centimetres to decimetres of WGS 84. Its metric grids are 3° Gauss–Krüger, e.g. EPSG:4547.
- **GCJ-02** ("Mars coordinates") is WGS 84 with a deliberate, non-linear obfuscation required by
  regulation. Gaode/AutoNavi and Apple use it in China. Mixing it with WGS 84 errs by
  **100–700 m**. **BD-09** (Baidu) adds a second obfuscation on top
  (https://en.wikipedia.org/wiki/Restrictions_on_geographic_data_in_China).
  - Neither is in EPSG or PROJ (inferred, consistent with every source read).
  - Open-source tools use reverse-engineered formulas: eviltransform gives about 1–2 m, under
    0.5 m in its "exact" variant; coordtransform covers wgs84↔gcj02↔bd09.
  - **They must be separate, explicitly approximate nodes, never SRIDs.**
- **Japan.** EPSG:6668 was renamed from JGD2011 to **JGD2024** on 2025-04-01, with "no change of
  definition or of coordinate values" (epsg.io/6668). The Plane Rectangular CS zones I–XIX are
  EPSG:6669–6687. Teach both names.

### The CRSs a user here will meet

| EPSG | name | units | use |
|---|---|---|---|
| 4326 | WGS 84 | degrees | GPS, GeoJSON, exchange |
| 3857 | WGS 84 / Pseudo-Mercator | "metres", true only at the equator | web tiles, Mapsui, VL.Globe. Display only |
| 326xx / 327xx | WGS 84 / UTM zone N / S | m | metric work anywhere |
| 4490 / 4547… | CGCS2000 / Gauss–Krüger zones | deg / m | China |
| 6668 / 6669–6687 | JGD2011 → JGD2024 / Plane Rectangular I–XIX | deg / m | Japan |
| 4258 / 3035 | ETRS89 / LAEA Europe | deg / m | Europe; 3035 is equal-area for statistics |
| 28992 / 7415 | Amersfoort / RD New (+ NAP height) | m | the Netherlands: **3D BAG, what vl-cityjson reads** |
| 4269 | NAD83 | deg | North America |

---

## 2. What the family does today (measured locally)

- **This package**: an SRID is a label. Setting it reprojects nothing, and that is intended
  (ARCHITECTURE, "The factory, and why SRID is 0"; Never: "CRS transformation … a focused
  package").
- **VL.Mapsui** converts lon/lat ↔ Web Mercator at the door. It uses Mapsui's
  `SphericalMercator` through an `ICoordinateSequenceFilter` (`GeometryLayerNode.ToMercator` /
  `ToLonLat`; `GraticuleLayer`, `NavigateNodes` and `MapNode` call it too). Only 4326 and 3857.
- **VL.Globe** has its own Web Mercator formulas (`TileGeometry.LatitudeOf` / `MercatorY`) on a
  sphere of WGS 84's equatorial radius.
- **vl-cityjson** reads 3D BAG in **EPSG:7415** (RD New + NAP). Its DESIGN.md says: "multiple
  files must share one CRS, **no coordinate transformation**". vl-citygml looks up 3D BAG tiles by
  an RD (EPSG:28992) point.

So two packages do Mercator by hand, and one has data in a national CRS **that cannot reach the
map or the globe at all**. That last gap is a concrete first target, and more useful than "a
general CRS toolkit".

---

## 3. The libraries (nuget.org and GitHub, 2026-11-11)

| | ProjNet | SharpProj | MaxRev.Gdal.Core | DotSpatial.Projections |
|---|---|---|---|---|
| what | NTS org's managed port | C++/CLI binding of **PROJ 9.5.1** | GDAL 3.13 (OSR = PROJ) | managed PROJ.4 port |
| latest | 2.1.0, 2025-08-18 | 9.5001.295, 2025-03-04 | 3.13.3.611, 2026-10-31 | 4.0.656, 2022-04-25 |
| licence | LGPL-2.1+ | Apache-2.0 | MIT | MIT |
| natives | none (127 KB) | **Windows only**: about 7 MB dll + 9.3 MB `proj.db` | about **114 MB** (Windows runtime) | none |
| EPSG by number | no (WKT in; `ProjNet.SRID` is third party, 2020, net462) | yes (`proj.db`) | yes | yes (embedded table, 11k PROJ.4 strings) |
| datum shift | TOWGS84 Helmert only | full PROJ: Helmert, grids (opt-in network, `cdn.proj.org`), time-dependent | full PROJ | towgs84; NTv2 reader, few grids bundled. CGCS2000 / JGD2011 as GRS80 with no shift |
| geodesic length / area | no | **yes** (`MeterLength`, `MeterArea`, `MeterDistance`) | via OSR | no |
| NTS helper | a wiki snippet (`ICoordinateSequenceFilter`) | **`SharpProj.NetTopologySuite`**: `Reproject<T>(geometry, toSRID)`, an `SridRegister` | none (WKB/WKT bridge) | Mapsui's `DotSpatialProjection` adapter |
| health | README: the team "doesn't have the resources to dedicate to supporting this project", points to SharpProj, DotSpatial, GDAL | single maintainer, last commit 2025-05 | very active | repo active (2025-10 fix), no release since 2022 |

What these numbers mean (inferred):
- **Only the PROJ-based options are correct for national grids.** Those are SharpProj and GDAL:
  they have the full EPSG database, the datum transformations, and real axis metadata. The
  managed two are PROJ.4-era and treat CGCS2000 or JGD2011 as equal to WGS 84. That is about a
  metre off, which is fine on a map and wrong for survey.
- **SharpProj fits vvvv's platform exactly** (Windows x64) and carries NTS helpers and geodesic
  measures. Its risks:
  - C++/CLI mixed mode: it must load inside vvvv's .NET 8 host. *Not yet measured.*
  - One maintainer, and the last release was 2025-03.
- **GDAL's 114 MB** is out of proportion for a reprojection package.
- **ProjNet** is the fallback: managed and from the NTS organisation, but without an EPSG
  database, without grids, and short of maintainers by its own admission.

---

## 4. Precedent: how others hand a CRS to a user

- **VL.Geo** (smakhtin, MIT, 0.0.1–0.0.10 in Aug–Sep 2021, dormant) is the only VL precedent. It
  has float Web Mercator formulas (`LatitudeToY (WebMercator Spherical)`), hand-written
  LLA↔ECEF↔ENU (`GeodedicToENU (ENU)` [sic]), and ProjNet exposed as raw reflected .NET nodes.
  It has no EPSG node and does not use the SRID. Nothing else exists on nuget.org or GitHub; the
  beta era had hand-written Mercator and UTM plugins.
  Read in full on 2026-11-11 (`gh api`, the repository's `.vl` and C#):
  - **Who:** Vadim Smakhtin, for **Mathrioshka**, a Moscow studio of interactive design, art and
    architecture. He wrote all 23 commits.
  - **When:** 2021-08-10 to 2021-09-24, six weeks. Not archived, but untouched since. 5 stars,
    1 fork. NuGet shows 5,485 downloads of 0.0.10.
  - **Dependencies:** NetTopologySuite 2.3.0 *and* ProjNET4GeoAPI 1.4.1 + GeoAPI 1.7.5. That
    GeoAPI line is the NTS 1.x interface layer that NTS 2.0 removed, so the package mixes two NTS
    eras. VL.CoreLib 2021.3.3. One category, `Geo`. The help patches are not in the nuspec.
  - **About 30 nodes** in four groups. The overlap with the family:
    - **Geometry creation and conversion:** `Point`, `LineString`, `LinearRing`,
      `MultiLineString`, `ToCoordinate (2D/3D, Float32/64)`. This duplicates this package's
      creation nodes and `Coordinate (FromVector2)`, without the defensive copying.
    - **Measuring:** `DistanceOperation (2D/3D)` with `NearestPoints`. This duplicates our
      `Distance` / `Nearest Points`; 3D distance is new.
    - **Linear referencing:** `LengthIndexedLine`, `ExtractPoint`, `LocationIndexOfPoint`,
      `LinearLocation`. **Not in this package yet; it is a tier-2 item** in ROADMAP. VL.Geo's help
      `HowTo work with linear coordinates` is a useful reference for it.
    - **Projection:**
      - `CoordinateTransformation` offers ProjNet's built-in `WGS84`, `WebMercator` and
        `WGS84 (UTM)` only, with no EPSG and no datum choice.
      - `LatitudeToY` / `YToLatitude (WebMercator Spherical)` are in **single precision**, so
        about a metre of rounding at city scale. This duplicates VL.Mapsui's and VL.Globe's
        Mercator.
      - `GeodedicToENU` / `ENUToGeodedic (ENU)` [sic] are hand-written WGS84 LLA↔ECEF↔ENU
        (inverse flattening rounded to 298.257224). **Nothing in the family has this yet**;
        it is the Cesium-style local frame a 3D scene needs.
  - **What it means for us:** VL.Geo shows that the need exists. It was built for a project and
    then left. Its CRS part is the thin end: ProjNet's three built-ins, which §6 measured to be
    unsafe once a datum changes. Nothing to depend on. Two ideas are worth taking: the ENU frame
    and the linear-referencing set.
- **Mapsui** puts a **CRS string on the data provider and on the map** (`"EPSG:3857"`) and
  reprojects vector data at the provider (`ProjectingProvider`) through one global, swappable
  `IProjection`. The core knows 4326, 3857 and 3395; anything else comes from the
  **DotSpatial** extension. It never reads the NTS SRID. Raster tiles are never reprojected.
- **QGIS** gives every layer its own CRS, detected on load. The project has one CRS and layers
  are reprojected on the fly. The CRS picker searches by code or name and shows the area of use.
  A layer with no CRS is flagged "unknown", with a warning that a wrong choice misplaces it.
  When several datum transformations are possible, a dialog asks which one to use.
- **Heron (Grasshopper)** has a global `SetSRS` (an EPSG code) and an Earth Anchor Point. The SRS
  is not saved with the document, so users are confused: a cautionary example.
- **Cesium for Unity/Unreal** offers no CRS choice. A `CesiumGeoreference` origin (lon, lat,
  height on WGS 84) defines a local east-north-up frame. This is the model for putting the world
  into a 3D scene.

**The gap (inferred).** Nobody carries the CRS on the geometry. NTS already has `Geometry.SRID`,
and this family already copies it through every node. A package that reads the source CRS from
the geometry and writes the target CRS onto the result makes a Heron-style global state
unnecessary, and makes "what CRS is this?" answerable at every pin.

---

## 5. A first sketch of the package (for discussion, nothing decided)

- **A sibling repository**, as ARCHITECTURE decided. This package keeps "an SRID is a label".
- **The name follows the library it wraps, unabbreviated**
  ([memory: never abbreviate a library name]). Wrapping SharpProj makes the category the library's
  name; which name (`SharpProj`, or the `PROJ` it binds) is the maintainer's call.
- **Two verbs that cannot be confused:**
  - `SetSRID` says what the numbers *already are*. It exists in spirit: the factory's SRID.
  - A transform, perhaps `ToSRID (Geometry, SRID)`, *changes the numbers*. It reads the source
    SRID from the geometry and refuses 0.
- **Name nodes after what the user wants** (VL-shape rule, 2026-11-08):
  - a UTM zone found from a lon/lat, so nobody picks `326xx` by hand;
  - `Length` / `Area` / `Distance (Geodesic)` in metres on lon/lat data;
  - CRS information (name, units, area of use) for a code, for the UI.
- **Lon/lat axis order everywhere**, said in every tooltip.
- **A held transform per (from, to) pair**, which makes it a `[ProcessNode]` or a cache: a static
  node runs 60 times a second ([memory: VL evaluates every frame]).
- **Network grids off by default**, behind an explicit pin. An offline patch must give the same
  answer every time.
- **GCJ-02 / BD-09 as separate, named, approximate nodes**, never SRIDs, and only if Chinese web
  map data is a real need.
- **For 3D scenes:** a local ENU origin in the Cesium / VL.Geo way, because Stride wants metres
  near the origin. Later.

**First acceptance target:** a 3D BAG building from vl-cityjson (EPSG:7415 / 28992) placed
correctly:
- on VL.Mapsui's map (→ 4326 → 3857);
- on VL.Globe;
- checked against a known address.

It is concrete, it is ours, and it fails visibly when wrong.

**After that, the GeometryEditor learns units:**
- the grid in degrees or metres according to the SRID;
- lon/lat drawn at the right aspect (1/cos φ);
- possibly a basemap through VL.Mapsui.

## 6. Measured: a console probe, 2026-11-11

A throwaway .NET 8 console app (scratch, not kept) took one RD New point (EPSG:28992,
`155000, 463000`, the grid's origin at the Onze Lieve Vrouwetoren in Amersfoort, published at
about 52.1552° N, 5.3872° E) to WGS 84 lon/lat with each library.

| library | lon | lat | off from PROJ |
|---|---|---|---|
| **SharpProj** (PROJ 9.5.1, EPSG db 11.22), network grids on or off | 5.387203508 | 52.155172301 | reference; stated accuracy 1 m, no grid used |
| **DotSpatial** (embedded table, `+towgs84=565.2369,50.0087,465.658,…`) | 5.387203581 | 52.155172303 | **0.005 m** |
| **ProjNet**, given the WKT1 that PROJ exports for 28992 | 5.387638889 | 52.156740165 | **177 m** |

**ProjNet was 177 m off, without a word.** PROJ's WKT1 export of 28992 carries no `TOWGS84`, and
ProjNet then skips the datum change entirely. This is exactly the "confident wrong answer" trap.
ProjNet can only be as right as the WKT it is handed, and nothing ships the right WKT with it.

**SharpProj works, but its packages have two defects** that a wrapping package must absorb:
1. **`ijwhost.dll` is not shipped.** SharpProj.dll is C++/CLI and imports `ijwhost.dll`, the
   shim that lets .NET Core load mixed-mode assemblies. The `SharpProj.Core` package does not
   contain it, the SDK does not copy it for a NuGet reference, and **vvvv 7.4's install has none
   either**. The first run failed with `FileNotFoundException … SharpProj.dll. 找不到指定的模块`.
   Copying `ijwhost.dll` from `dotnet\packs\Microsoft.NETCore.App.Host.win-x64\8.0.x\runtimes\win-x64\native`
   beside it fixed it. Our package will have to carry that file; it is MIT, part of the .NET
   runtime.
2. **`SharpProj.NetTopologySuite` puts its .NET Core build in `lib\netcore`**, not a valid TFM
   folder. NuGet falls back to its net47 build (NU1701). That build works on .NET 8, but our
   package should probably carry our own small NTS bridge instead: it is about 30 lines (an
   `ICoordinateSequenceFilter`).
3. **`proj.db` is in the `SharpProj` package, not in `SharpProj.Core`.** Both are needed.

Also measured with SharpProj:
- The first transform is ready **73 ms** after start, context and database included.
- `Reproject(geometry, 4326)` reads the source SRID from the geometry, but only after each SRID is
  registered (`SridRegister.Register(crs, srid)`). Unregistered, it throws
  `Unregistered SRID 4326 used`. The result carries SRID 4326 and lon-first axes (with
  `WithAxisNormalized()`).
- A 100 m × 100 m RD square: `MeterArea` on the lon/lat result is **10,001.79 m²**, and the plain
  `Area` is 1.3e-6 "square degrees". The 1.79 m² is RD New's own scale distortion: the
  geodesic figure is the true area.

### Inside vvvv 7.4, 2026-11-11

A scratch package (`scratch\`, never committed) had two static nodes, `SharpProj` and
`DotSpatial`, both taking the same RD point. Their pins carry only `double`, `int` and `string`, so
neither library's types reach vvvv. Its `lib\net8.0` held:
- `VL.CrsProbe.dll`
- the **win-x64 runtime** `SharpProj.dll` (7.0 MB; *not* the 5.7 MB reference stub, so the
  project builds with `RuntimeIdentifier win-x64`)
- `proj.db` and `proj.ini`
- `DotSpatial.Projections.dll`
- our own `ijwhost.dll`

`vvvvc` resolved both nodes. In vvvv:

- **SharpProj loads and transforms.** Its Info pin read: `proj.db there: True; PROJ_DATA unset;
  PROJ 9.5.1, EPSG 11.22; Amersfoort / RD New -> WGS 84 (with axis order normalized for
  visualization); accuracy 1 m`. Lon/lat showed 5.39 / 52.16, the same as DotSpatial. **No
  `PROJ_DATA` is needed**: PROJ found `proj.db` beside the assembly.
- vvvv's process modules showed `ijwhost.dll`, `SharpProj.dll` and `DotSpatial.Projections.dll`
  loaded **from the package's `lib` folder**. The VC++ runtime (`VCRUNTIME140`, `_1`,
  `MSVCP140`) came from `System32`.
- **Negative test:** with `ijwhost.dll` removed, the SharpProj node reported
  `FileNotFoundException: Could not load file or assembly '…\SharpProj.dll'. 找不到指定的模块`
  and output NaN, while DotSpatial carried on. **A package wrapping SharpProj must ship
  `ijwhost.dll`.** Nothing else in vvvv provides it.
- **Remaining risk, not tested:** a machine without the VC++ 2015–2022 runtime in `System32`.
  Most Windows machines have it, but nothing guarantees it. The package's docs should say so, or
  the package could carry `vcruntime140*.dll` / `msvcp140.dll` app-locally.

**Decided by the maintainer, 2026-11-11:** China's GCJ-02 / BD-09 are **out of scope for now**.

## 7. Open questions for the maintainer

1. **Library.** The console probe (§6) supports SharpProj: it was right, and its two packaging
   defects can be absorbed. DotSpatial was also right (5 mm) and is pure managed, so it is the
   real alternative if SharpProj will not load inside vvvv. ProjNet is out: 177 m, silently.
   **SharpProj loads inside vvvv** (§6), with our own `ijwhost.dll` in the package; the decision
   is now the maintainer's.
2. **Name and category** of the new package and repository.
3. **Scope of version one:** the transform and its two verbs only, or geodesic measures and UTM
   helpers from the start.
4. ~~GCJ-02 / BD-09~~: out of scope for now (the maintainer, 2026-11-11).
5. Should VL.Mapsui and VL.Globe later replace their hand-written Mercator with the new package?
   That would make them depend on it. Not urgent.
