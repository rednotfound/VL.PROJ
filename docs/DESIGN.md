# VL.PROJ: design

**VL.PROJ** puts [PROJ](https://proj.org) (the coordinate transformation engine every GIS uses)
behind vvvv gamma nodes. Its one job is to **move NetTopologySuite geometry from one coordinate
reference system to another, and say how well that went.** Nothing about drawing, maps or file
formats: those belong to the siblings.

Started 2026-11-11. Everything below the research section is a **proposal** until the
maintainer confirms it.

---

## Why this package exists, and why it is separate

- **The research is in VL.NetTopologySuite's `docs/CRS-RESEARCH.md`:** the concepts, the
  libraries compared, the precedent, and the measurements. This document does not repeat it.
- **VL.NetTopologySuite decided long ago that an SRID is a label there.** Setting one moves
  nothing, and transformation lives in a focused package. This is that package.
- **The first concrete need:** vl-cityjson reads 3D BAG buildings in EPSG:7415 (RD New + NAP).
  They cannot reach VL.Mapsui's map or VL.Globe, which speak WGS 84 lon/lat and Web Mercator only.

## The engine: PROJ through SharpProj, bundled

Chosen on 2026-11-11 after two measurements:
1. **Console:** one RD New point to WGS 84. PROJ (via SharpProj) and DotSpatial agree to 5 mm.
   ProjNet was **177 m off without a word**, because the WKT it was given had no TOWGS84.
2. **Inside vvvv 7.4:** SharpProj loads and transforms from a package's `lib\net8.0`. Without our
   own `ijwhost.dll` beside it, it fails with `FileNotFoundException … 找不到指定的模块`.

What that fixes about packaging:

| file in `lib\net8.0` | why it is ours to ship |
|---|---|
| `VL.PROJ.dll` | the nodes |
| `SharpProj.dll` (the **win-x64 runtime** build, 7 MB) | C++/CLI. The `ref` build is a 5.7 MB stub that cannot run, so the project builds with `RuntimeIdentifier win-x64` |
| `ijwhost.dll` | the shim .NET needs to load C++/CLI. SharpProj's package lacks it, and so does vvvv. Taken from `Microsoft.NETCore.App.Host.win-x64` (MIT) |
| `proj.db` (9 MB) + `proj.ini` | the EPSG database. PROJ finds it beside the assembly, with no `PROJ_DATA` needed (measured) |

**SharpProj is therefore not a NuGet dependency of this package.** NuGet would install it into a
folder we do not control, where `ijwhost.dll` cannot sit beside it. It is bundled instead, so
**no SharpProj type may appear on a pin.** Pins carry NTS `Geometry`, `int`, `double`,
`string` and `bool` only. This also keeps family rule 2 ("an upstream library must be a package
in a package repository") to the NTS packages it already covers.

Licences to carry in `THIRD-PARTY-NOTICES.md`:
- SharpProj: Apache-2.0
- PROJ: MIT
- the EPSG dataset in `proj.db`: IOGP's terms of use, which allow redistribution with notice
- `ijwhost.dll`: MIT

Known risk: the VC++ 2015–2022 runtime is taken from `System32`. A machine without it is untested.

**Family dependencies:**
- `NetTopologySuite` 2.6.0, the same version as every sibling;
- `VL.NetTopologySuite`, for its drawn Geometry IOBoxes and its nodes in the help patches. The
  same as VL.NetTopologySuite.GeoJSON.

## The model: the CRS travels on the geometry

Every NTS geometry carries an `SRID`, and VL.NetTopologySuite keeps it with care (tested):
- factories stamp it;
- operations keep it;
- `FromWKT` takes it from the factory pin;
- VL.NetTopologySuite.GeoJSON reads 4326.

So the source CRS is **read from the geometry**, never chosen on the node. The result carries the
target SRID. There is no global "current CRS" anywhere, which is Heron's mistake (see the research).

- **SRID 0 or -1 means "unknown", and a transform refuses it** with a message. It does not guess.
- **An SRID is an EPSG code.** Other authorities (ESRI:…) and custom WKT are out of scope for
  now: an SRID is an integer, and the family has no registry for anything else.

## Nodes: a proposal, category `PROJ`

Named after what the user wants, with pins in VL's shape: three inputs is the target, and extra
outputs are free. **Lon/lat axis order everywhere (x = east)**, and every tooltip says so.

### Version one

| node | in | out | does |
|---|---|---|---|
| **`Reproject`** | Geometry, To SRID | Result, Success, Message, Accuracy | the transform. Reads the source SRID from the geometry. On failure (SRID unknown, code unknown, point outside the CRS) it returns the input unchanged, `Success` false and a reason, as `FromWKT` does. `Accuracy` is PROJ's stated accuracy in metres, -1 when unknown |
| **`CRS`** | SRID | Name, Is Geographic, Units, Area Of Use, Success | what an EPSG code is. `Area Of Use` is a lon/lat rectangle as a Geometry (SRID 4326), which can be drawn |

Plus one node that belongs in **VL.NetTopologySuite, not here**:
- **`SetSRID`** (Geometry, SRID → Result): a copy with a new *label*. It is the other verb and must
  be impossible to confuse with `Reproject`, which is PostGIS's `ST_SetSRID` / `ST_Transform`
  split. It moves nothing, so it belongs in the geometry package.

### Later, each when something needs it

- **`Length` / `Area` / `Distance (Geodesic)`**: metres on lon/lat data, on the ellipsoid
  (PROJ's geodesic). This answers the warning VL.NetTopologySuite's `Area` can only give today.
- **`UTMZone`** (Geometry → SRID): the right UTM zone for where the geometry is, so nobody picks
  `326xx` by hand. Plain arithmetic.
- **A local ENU frame** (origin lon/lat/height → metres east/north/up), for putting the world
  into a Stride scene at the right scale. This is the Cesium and VL.Geo model.

### Not here

- **China's GCJ-02 / BD-09**: out of scope for now (the maintainer, 2026-11-11). If they come,
  they come as separate, approximate nodes, never as SRIDs.
- Grid shifts from the network (`cdn.proj.org`): **off**. An offline patch must give the same
  answer every time. A pin to opt in can come later.

## Implementation notes

- **Static nodes, with transforms cached** per `(from, to)`. A node runs 60 times a second,
  and building a PROJ transform each frame would be absurd. PROJ objects are not thread-safe, so
  the cache is used under a lock.
- **Both CRSs are axis-normalised** (`WithAxisNormalized`): lon/lat and easting/northing in, and
  the same out, whatever EPSG says officially.
- **Coordinates are copied** (VL.NetTopologySuite's rule): `Reproject` transforms a copy and never
  touches the input.
- **Z:** transformed when present (a 3D point to PROJ), left NaN when not. M passes through.
- **Our own NTS bridge**, an `ICoordinateSequenceFilter` of about 30 lines, instead of
  `SharpProj.NetTopologySuite`. That package's `lib\netcore` folder is not a valid target
  framework, so NuGet falls back to its net47 build, and it requires a global SRID registry.

## First acceptance target

A 3D BAG building from vl-cityjson, in EPSG:7415 / 28992:
1. `Reproject` to 4326 and then 3857: it lands on its address on VL.Mapsui's map.
2. Checked against the address by eye and against a published coordinate.

This is concrete, it is ours, and it fails visibly when wrong.

## Open questions for the maintainer

1. Node names: `Reproject` (the GIS word) or something else? `CRS` for the information node?
2. `SetSRID` in VL.NetTopologySuite: agreed?
3. Version one as above (two nodes), or geodesic measures in it too?
