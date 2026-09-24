param([switch]$VerifyOnly)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$web=Join-Path $repo 'src/Alpha6Ops.Desktop/MapV2/Web'
$manifest=Join-Path $web 'assets-manifest.json'
if (-not $VerifyOnly) {
    $staging=Join-Path $repo 'work/checkpoint-7.22/dependencies'
    New-Item -ItemType Directory -Force -Path $staging,(Join-Path $web 'vendor') | Out-Null
    $archive=Join-Path $staging 'maplibre-gl-6.11.2.tgz'
    if (!(Test-Path -LiteralPath $archive)) { Invoke-WebRequest 'https://registry.npmjs.org/maplibre-gl/-/maplibre-gl-6.11.2.tgz' -OutFile $archive }
    $actual=[Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($archive)))
    if ($actual -ne 'Xh06pxoipjX/Ad1sUPGhiNh9go9naCNGZZ1IJm3sIeqK1kYGuMaMDSTIGDQ2igOLfVyukJU7kvEOA089VO7Z7g==') { throw 'MapLibre archive integrity mismatch.' }
    & tar -xf $archive -C $staging
    if ($LASTEXITCODE -ne 0) { throw 'MapLibre extraction failed.' }
    foreach($name in @('maplibre-gl.mjs','maplibre-gl-shared.mjs','maplibre-gl-worker.mjs')) {
        Copy-Item -LiteralPath (Join-Path $staging "package/dist/$name") -Destination (Join-Path $web "vendor/$name")
    }
    Copy-Item -LiteralPath (Join-Path $staging 'package/LICENSE.txt') -Destination (Join-Path $web 'vendor/LICENSE.maplibre.txt')
    Copy-Item -LiteralPath (Join-Path $repo 'src/Alpha6Ops.Desktop/Assets/Map/ne_50m_land.geojson') -Destination (Join-Path $web 'land.geojson')
    [xml]$tokens=Get-Content -LiteralPath (Join-Path $repo 'src/Alpha6Ops.Desktop/SierraDesignTokens.xaml') -Raw
    $palette=[ordered]@{}
    foreach($brush in $tokens.ResourceDictionary.SolidColorBrush) { $palette[$brush.GetAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml')]=$brush.Color }
    $palette | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $web 'theme.json') -Encoding utf8
    $names=@('vendor/maplibre-gl.mjs','vendor/maplibre-gl-shared.mjs','vendor/maplibre-gl-worker.mjs','vendor/LICENSE.maplibre.txt','land.geojson','theme.json')
    $files=@($names | ForEach-Object { [ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $web $_)).Hash} })
    [ordered]@{maplibre='6.11.2';archiveSha512=$actual;webview2='1.0.4191.47';geography='Natural Earth 50m land';files=$files} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding utf8
}
$expected=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
foreach($file in $expected.files) {
    if((Get-FileHash -LiteralPath (Join-Path $web $file.path)).Hash -ne $file.sha256) { throw "Bundled asset differs: $($file.path)" }
}
Write-Output 'Pinned local map assets verified. No runtime network provider or API key.'
