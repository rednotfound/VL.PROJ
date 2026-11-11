# Third-party notices

VL.PROJ is MIT-licensed (see LICENSE). It ships the following inside its package, in
`lib\net8.0`, each under its own licence.

| file | what | licence | source |
|---|---|---|---|
| `SharpProj.dll` | SharpProj 9.5001.295, a C++/CLI binding of PROJ 9.5.1, statically containing PROJ | Apache-2.0 (SharpProj); MIT (PROJ) | https://github.com/AmpScm/SharpProj, https://proj.org |
| `proj.db`, `proj.ini` | PROJ's database, including the EPSG Geodetic Parameter Dataset (EPSG 11.22) | MIT (PROJ); the EPSG dataset is © IOGP and used under its terms of use, which allow redistribution with this notice | https://proj.org, https://epsg.org/terms-of-use.html |
| `ijwhost.dll` | the .NET host shim that loads C++/CLI assemblies, from Microsoft.NETCore.App.Host.win-x64 8.0.34 | MIT | https://github.com/dotnet/runtime |

The full licence texts are at the sources above. Before a public release, these should be checked
against the exact versions shipped, and the full texts included here.
