# VL.PROJ

Coordinate transformation for NetTopologySuite geometry in vvvv gamma, with [PROJ](https://proj.org), the engine QGIS, GDAL and PostGIS use:

* `Reproject`: moves a geometry from the coordinate system its SRID names to another EPSG code, and says how accurate that is
* `CRS`: an EPSG code's name, units, and the area it is meant for

For use with vvvv, the visual live-programming environment for .NET: http://vvvv.org

## Getting started
- Install as [described here](https://thegraybook.vvvv.org/reference/hde/managing-nugets.html) via commandline:

    `nuget install VL.PROJ -pre`

- Usage examples and more information are included in the pack and can be found via the [Help Browser](https://thegraybook.vvvv.org/reference/hde/findinghelp.html). Start with *Explanation Overview of available nodes*, then *HowTo Move a geometry to another CRS*.
- What changed in each version: [Releases](https://github.com/rednotfound/VL.PROJ/releases)

## Before you use it
- The CRS travels on the geometry as its SRID: set it with VL.NetTopologySuite's `SetSRID`, then `Reproject`. An SRID of 0 is refused, never guessed.
- x is always east, so EPSG:4326 is longitude, latitude. The input geometry is never changed; `Reproject` moves a copy.
- PROJ and its EPSG database come inside the package. Windows x64 only, and it needs the Visual C++ 2015-2022 runtime.

## Related packages
- [VL.NetTopologySuite](https://github.com/rednotfound/VL.NetTopologySuite): the geometry this package moves
- [VL.NetTopologySuite.GeoJSON](https://github.com/rednotfound/VL.NetTopologySuite.GeoJSON): read and write GeoJSON
- [VL.Mapsui](https://github.com/rednotfound/VL.Mapsui): draw geometry on a map

## Contributing
- Report issues on [GitHub](https://github.com/rednotfound/VL.PROJ/issues)
- How the package is built, tested and released, and why it is shaped the way it is: [the guide](https://github.com/rednotfound/VL.PROJ/blob/main/docs/GUIDE.md)
- When making a pull-request, please make sure to read the general [guidelines on contributing to vvvv libraries](https://thegraybook.vvvv.org/reference/extending/contributing.html)

## Credits
Based on [PROJ](https://proj.org) (MIT) through [SharpProj](https://github.com/AmpScm/SharpProj) (Apache-2.0), both bundled, with the EPSG Geodetic Parameter Dataset, owned by IOGP and used under its [terms of use](https://epsg.org/terms-of-use.html). Full texts in [THIRD-PARTY-NOTICES](https://github.com/rednotfound/VL.PROJ/blob/main/THIRD-PARTY-NOTICES.md).

## License
[MIT](https://github.com/rednotfound/VL.PROJ/blob/main/LICENSE)
