$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($projectRoot -ne 'F:\UnityProjects\Skybreak') { throw "Unexpected project: $projectRoot" }
$reportRoot = Join-Path $projectRoot 'Docs\Maintenance\2026-10-11-cleanup'
$targets = @(
    'Tools\SceneSnapshots', 'Tools\AssetSnapshots',
    'resources\DungeonAssetLibrary\Archives', 'resources\DungeonAssetLibrary\Packs',
    'resources\MapDecorationKit\Archives', 'resources\MapDecorationKit\Packs',
    'Docs\Skybreak_Team_Guides_2026-10-03.zip'
)
# Retain third-party attribution before removing duplicate downloaded/extracted payloads.
foreach ($library in @('DungeonAssetLibrary', 'MapDecorationKit')) {
    $packRoot = Join-Path $projectRoot "resources\$library\Packs"
    if (Test-Path -LiteralPath $packRoot) {
        Get-ChildItem -LiteralPath $packRoot -Recurse -File | Where-Object Name -Match '(?i)licen[cs]e|copyright|credits' | ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($packRoot, $_.FullName)
            $destination = Join-Path $projectRoot "resources\$library\Licences\$relative"
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destination
            if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Licence copy mismatch' }
        }
    }
}
$targets += Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ArtSource') -Recurse -File -Filter '*.blend1' | ForEach-Object {
    $source = $_.FullName.Substring(0, $_.FullName.Length - 1)
    if (!(Test-Path -LiteralPath $source -PathType Leaf) -or (Get-Item -LiteralPath $source).Length -eq 0) { throw "Missing current Blender source: $source" }
    [IO.Path]::GetRelativePath($projectRoot, $_.FullName)
}
$rows = foreach ($relative in $targets) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
    if (!$absolute.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Outside project: $absolute" }
    if (Test-Path -LiteralPath $absolute) {
        $item = Get-Item -LiteralPath $absolute -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing linked path: $absolute" }
        $files = if ($item.PSIsContainer) { @(Get-ChildItem -LiteralPath $absolute -Recurse -File -Force) } else { @($item) }
        $bytes = ($files | Measure-Object Length -Sum).Sum
        Remove-Item -LiteralPath $absolute -Recurse -Force
        [pscustomobject]@{ Path = $relative; Bytes = $bytes; Files = $files.Count }
    }
}
$rows | Export-Csv -LiteralPath (Join-Path $reportRoot 'deleted-local-content.csv') -NoTypeInformation
$rows | ConvertTo-Json
