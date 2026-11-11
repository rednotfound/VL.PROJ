# Builds scratch\Acceptance Delft on OSM.vl - VL.PROJ's first acceptance test (2026-11-11, passed).
# Steps:
#   1. python -I tools\acceptance\cityjson_footprints.py D:\2026_Projects\vl-cityjson\data\9-284-556.city.json scratch\delft-footprints.wkt 85065 446394 200
#   2. this script
#   3. open the patch in vvvv with these repositories: vl-proj, vl-nettopologysuite and vl-mapsui (each dist;deps)
# Pass: the red 3D BAG footprints sit exactly on OpenStreetMap's buildings (OSM NL imported BAG).
# Acceptance patch (scratch, never committed): 44 3D BAG footprints in Delft, RD New (EPSG:28992),
# through VL.PROJ's Reproject to WGS 84, drawn by VL.Mapsui over OpenStreetMap. OSM's Dutch building
# outlines were imported from the same BAG, so a right transform makes them coincide.
$ErrorActionPreference = 'Stop'
Set-Location 'D:\2026_Projects\vl-proj'
. .\tools\HelpPatchGen.ps1
$scratch = 'D:\2026_Projects\vl-proj\scratch'
$out = 'D:\2026_Projects\vl-proj\scratch\Acceptance Delft on OSM.vl'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$footprints = [IO.File]::ReadAllText("$scratch\delft-footprints.wkt")

# A node copied verbatim from a shipped VL.Mapsui patch, with fresh IDs; returns the XML and its pins by name.
function Copy-Node([string]$file, [string]$category, [string]$name, [string]$bounds, [string[]]$extraPins = @()) {
    $x = [IO.File]::ReadAllText($file)
    $m = [regex]::Matches($x, '<Node Bounds="[^"]+" Id="\w+">\s*<p:NodeReference LastCategoryFullName="' + [regex]::Escape($category) + '"(?:(?!</Node>).)*?</Node>', 'Singleline') |
        Where-Object { $_.Value -match ('Kind="\w+Flag" Name="' + [regex]::Escape($name) + '"') } | Select-Object -First 1
    if (-not $m) { throw "no $category/$name in $file" }
    $xml = $m.Value -replace '<Node Bounds="[^"]+"', "<Node Bounds=`"$bounds`""
    $pins = @{}
    $xml = [regex]::Replace($xml, 'Id="\w+"', { param($mm) 'Id="' + (New-Id) + '"' })
    foreach ($p in [regex]::Matches($xml, '<Pin Id="(\w+)" Name="([^"]+)"')) { $pins[($p.Groups[2].Value -replace '\s', '')] = $p.Groups[1].Value }
    foreach ($e in $extraPins) {
        $id = New-Id; $pins[($e -replace '\s', '')] = $id
        $xml = $xml.Replace('</Node>', "  <Pin Id=`"$id`" Name=`"$e`" Kind=`"InputPin`" />`r`n</Node>")
    }
    $xml = $xml -replace '<p:HelpFocus[^<]*</p:HelpFocus>\s*', ''   # an acceptance patch is nobody's F1 help
    [pscustomobject]@{ Xml = $xml; Pins = $pins }
}

$showMap = 'D:\2026_Projects\vl-mapsui\help\VL.Mapsui\HowTo Show a map.vl'
$drawGeo = 'D:\2026_Projects\vl-mapsui\help\VL.Mapsui\HowTo Draw a geometry.vl'

$d = New-Doc
Heading $d 60 40 'Acceptance: 3D BAG in Delft on OpenStreetMap'
Intro   $d 60 95 '44 building footprints from vl-cityjson''s 3D BAG tile, in RD New (EPSG:28992), moved to WGS 84 by Reproject and drawn over OpenStreetMap. Right means they sit exactly on the OSM buildings.' 760

$wkt = Pad $d String '60,200,400,15' $footprints ''
$fac = Node $d 'GeometryFactory' 'NetTopologySuite.Geometry' '500,200,110,19' -In 'SRID' -Out 'Result' -Defaults @{ 'SRID' = '28992|Integer32' }
$rd  = Node $d 'FromWKT' 'NetTopologySuite.IO' '60,240,90,19' -In 'WKT', 'Factory' -Out 'Result', 'Success'
Link $d $wkt $rd.WKT
Link $d $fac.Result $rd.Factory
$re  = Node $d 'Reproject' 'PROJ' '60,290,120,19' -In 'Geometry', 'To SRID' -Out 'Output', 'Success', 'Message', 'Accuracy' -Dependency 'VL.PROJ.vl' -Defaults @{ 'To SRID' = '4326|Integer32' }
Link $d $rd.Result $re.Geometry
Link $d $re.Message (OutPad $d '300,330,500,30' '')
Link $d $re.Success (OutPad $d '300,370,35,15' 'Success')

$cons1 = Copy-Node $drawGeo 'Collections.Spread' 'Cons' '60,420,39,19'
$d.Elements.Add($cons1.Xml)
Link $d $re.Output $cons1.Pins['Input']
$geo = Copy-Node $drawGeo 'Mapsui.Layers' 'Geometry' '60,470,110,19'
$d.Elements.Add($geo.Xml)
Link $d $cons1.Pins['Result'] $geo.Pins['Geometries']

$osm = Copy-Node $showMap 'Mapsui.Layers' 'OpenStreetMap' '260,470,110,19'
# Enabled defaults to False in the C# node; Show a map wires a True IOBox to it. Here a pin default.
$osm.Xml = $osm.Xml.Replace('<Choice Kind="ProcessAppFlag" Name="OpenStreetMap" />', '<Choice Kind="ProcessAppFlag" Name="OpenStreetMap" />' + "
<PinReference Kind=""InputPin"" Name=""Enabled"" />").Replace('Name="Enabled" Kind="InputPin" />', 'Name="Enabled" Kind="InputPin" DefaultValue="True" />')
$d.Elements.Add($osm.Xml)
$cons2 = Copy-Node $showMap 'Collections.Spread' 'Cons' '60,530,60,19' -extraPins @('Input 2')
$d.Elements.Add($cons2.Xml)
Link $d $osm.Pins['Result'] $cons2.Pins['Input']      # OSM first: the buildings draw on top
Link $d $geo.Pins['Result'] $cons2.Pins['Input2']

$map = Copy-Node $showMap 'Mapsui' 'Map' '60,580,110,19'
# Centre on the tile centre, RD 85065 446394 -> 4.368754, 52.001511 (PROJ), close enough to see buildings.
$map.Xml = $map.Xml -replace '(Name="Initial Center Longitude"[^>]*DefaultValue=")[^"]*', '${1}4.368754' `
                    -replace '(Name="Initial Center Latitude"[^>]*DefaultValue=")[^"]*', '${1}52.001511' `
                    -replace '(Name="Initial Zoom Level"[^>]*DefaultValue=")[^"]*', '${1}17'
$d.Elements.Add($map.Xml)
Link $d $cons2.Pins['Result'] $map.Pins['Layers']
$skia = Copy-Node $showMap 'Mapsui.Skia' 'ToSkiaLayer' '60,630,130,19'
$d.Elements.Add($skia.Xml)
Link $d $map.Pins['Result'] $skia.Pins['Map']
$ren = Copy-Node $showMap 'Graphics.Skia' 'Renderer' '20,680,185,19'
$d.Elements.Add($ren.Xml)
Link $d $skia.Pins['Result'] $ren.Pins['Input']

Save-Doc $d $out -Dependencies @('VL.PROJ', 'VL.Mapsui', 'VL.Skia')
"map Initial pins: " + (([regex]::Matches($map.Xml, 'Name="(Initial[^"]+)"[^>]*DefaultValue="([^"]*)"') | % { "$($_.Groups[1].Value)=$($_.Groups[2].Value)" }) -join ', ')
