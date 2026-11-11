<#
.SYNOPSIS
    Fails unless every node of VL.PROJ opens a help patch on F1.

.DESCRIPTION
    F1 on a node opens the help patch in which a node of that name carries a High help flag
    (<p:HelpFocus>High</p:HelpFocus>, VL.NetTopologySuite's docs/HELP-PATCH-STYLE.md). Nothing
    else links a node to its help, and a missing flag fails silently: F1 simply opens nothing.

    VL.NetTopologySuite's Test-VLPatch has this audit; vl-geojson's, which this repository's
    Test-VLPatch is, does not - hence this script (2026-11-11).

    The nodes are read from the BUILT assembly, not the source: the public static methods of its
    public types, which is what [ImportAsIs] turns into nodes. Engine and Wgs84 have public static
    methods too, but are internal classes, and a source grep would have counted them.
    Not yet handled, because VL.PROJ has none: [Name("...")] renames and [ProcessNode] classes.

.EXAMPLE
    .\build.ps1 ; .\tools\Test-HelpFlags.ps1
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path $PSScriptRoot -Parent
$dll = Join-Path $RepoRoot 'src\VL.PROJ\bin\Release\net8.0\VL.PROJ.dll'
if (-not (Test-Path $dll)) { throw "not built: $dll - run .\build.ps1 first" }

Add-Type -AssemblyName System.Reflection.Metadata
$stream = [IO.File]::OpenRead($dll)
try {
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
    $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    $nodes = foreach ($h in $md.TypeDefinitions) {
        $t = $md.GetTypeDefinition($h)
        if (($t.Attributes -band [Reflection.TypeAttributes]::VisibilityMask) -ne [Reflection.TypeAttributes]::Public) { continue }
        foreach ($mh in $t.GetMethods()) {
            $m = $md.GetMethodDefinition($mh)
            $public = ($m.Attributes -band [Reflection.MethodAttributes]::MemberAccessMask) -eq [Reflection.MethodAttributes]::Public
            $static = ($m.Attributes -band [Reflection.MethodAttributes]::Static) -ne 0
            $special = ($m.Attributes -band [Reflection.MethodAttributes]::SpecialName) -ne 0
            if ($public -and $static -and -not $special) { $md.GetString($m.Name) }
        }
    }
}
finally { $stream.Close() }
$nodes = @($nodes | Sort-Object -Unique)
if ($nodes.Count -eq 0) { throw "no node found in $dll - the check would pass vacuously" }

# Every node of ours that carries a High flag, in any help patch of this package.
$flagged = @{}
# The node's name is its <Choice Kind="...Flag">, not the last Name= in the reference: a pin with
# a default adds <PinReference Name="To SRID"/> after it, and reading that instead reported
# Reproject as unflagged on the first run (2026-11-11).
$pattern = '<p:NodeReference[^>]*LastDependency="VL\.PROJ\.vl"[^>]*>(?:(?!</p:NodeReference>).)*?<Choice Kind="(?:OperationCallFlag|ProcessAppFlag|ProcessNodeFlag)" Name="(?<name>[^"]+)"\s*/>(?:(?!</p:NodeReference>).)*</p:NodeReference>\s*<p:HelpFocus[^>]*>High</p:HelpFocus>'
foreach ($patch in Get-ChildItem (Join-Path $RepoRoot 'help\VL.PROJ') -Filter '*.vl') {
    $xml = [IO.File]::ReadAllText($patch.FullName)
    foreach ($m in [regex]::Matches($xml, $pattern, 'Singleline')) { $flagged[$m.Groups['name'].Value] = $patch.Name }
}

$missing = @($nodes | Where-Object { -not $flagged.ContainsKey($_) })
foreach ($n in $nodes) {
    if ($flagged.ContainsKey($n)) { Write-Host "  ok    $n -> $($flagged[$n])" -ForegroundColor DarkGray }
    else { Write-Host "  FAIL  $n has no High help flag in any help patch: F1 on it opens nothing" -ForegroundColor Red }
}
if ($missing.Count) { Write-Host "`nFAIL - $($missing.Count) of $($nodes.Count) nodes without F1 help." -ForegroundColor Red; exit 1 }
Write-Host "`nPASS - $($nodes.Count) of $($nodes.Count) nodes open a help patch on F1." -ForegroundColor Green
