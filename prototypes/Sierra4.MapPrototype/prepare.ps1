$ErrorActionPreference = 'Stop'
$prototypeRoot = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $prototypeRoot '../..')).Path
$assetRoot = Join-Path $prototypeRoot 'web'
$stagingRoot = Join-Path $repoRoot 'work/checkpoint-7.21/dependencies'
New-Item -ItemType Directory -Force -Path $assetRoot,(Join-Path $assetRoot 'vendor'),$stagingRoot | Out-Null
$archive = Join-Path $stagingRoot 'maplibre-gl-6.11.2.tgz'
if (!(Test-Path $archive)) { Invoke-WebRequest 'https://registry.npmjs.org/maplibre-gl/-/maplibre-gl-6.11.2.tgz' -OutFile $archive }
$integrity = 'Xh06pxoipjX/Ad1sUPGhiNh9go9naCNGZZ1IJm3sIeqK1kYGuMaMDSTIGDQ2igOLfVyukJU7kvEOA089VO7Z7g=='
$actual = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($archive)))
if ($actual -ne $integrity) { throw 'MapLibre archive integrity mismatch.' }
& tar -xf $archive -C $stagingRoot
if ($LASTEXITCODE -ne 0) { throw 'Archive extraction failed.' }
foreach($name in @('maplibre-gl.mjs','maplibre-gl-shared.mjs','maplibre-gl-worker.mjs','maplibre-gl.css')) {
  Copy-Item -LiteralPath (Join-Path $stagingRoot "package/dist/$name") -Destination (Join-Path $assetRoot "vendor/$name")
}
Copy-Item -LiteralPath (Join-Path $stagingRoot 'package/LICENSE.txt') -Destination (Join-Path $assetRoot 'vendor/LICENSE.maplibre.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'src/Alpha6Ops.Desktop/Assets/Map/ne_110m_land.geojson') -Destination (Join-Path $assetRoot 'land.geojson')
[xml]$tokens=Get-Content -Raw (Join-Path $repoRoot 'src/Alpha6Ops.Desktop/SierraDesignTokens.xaml')
$palette=@{}
foreach($brush in $tokens.ResourceDictionary.SolidColorBrush) { $palette[$brush.GetAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml')]=$brush.Color }
$palette | ConvertTo-Json | Set-Content (Join-Path $assetRoot 'theme.json') -Encoding utf8
@{maplibre='6.11.2';archiveSha512=$actual;webview2='1.0.4191.47';geography='Existing Natural Earth 110m land';palette='SierraDesignTokens.xaml'} | ConvertTo-Json | Set-Content (Join-Path $assetRoot 'dependency-evidence.json') -Encoding utf8
Write-Output 'Pinned local assets staged. Runtime needs no internet or provider token.'
