[CmdletBinding()]
param(
    [string]$Registry = (Join-Path $PSScriptRoot 'replay-fixtures.json'),
    [switch]$CreateArchives
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$registryPath=[IO.Path]::GetFullPath($Registry)
$configuration=Get-Content -LiteralPath $registryPath -Raw|ConvertFrom-Json
foreach($entry in $configuration.files){
    if(-not $entry.archivePath){throw "Replay fixture has no source-controlled archive: $($entry.path)"}
    $raw=[IO.Path]::GetFullPath((Join-Path $repo $entry.path))
    $archive=[IO.Path]::GetFullPath((Join-Path $repo $entry.archivePath))
    if(-not $raw.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase) -or -not $archive.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Replay fixture path escaped the repository.'}
    if($CreateArchives){
        if(-not (Test-Path -LiteralPath $raw -PathType Leaf)){throw "Raw replay fixture unavailable: $raw"}
        if((Get-FileHash $raw -Algorithm SHA256).Hash -ne $entry.sha256){throw "Raw replay fixture hash mismatch: $raw"}
        New-Item -ItemType Directory -Path (Split-Path $archive) -Force|Out-Null
        $temporary=$archive+'.tmp'
        try{
            $input=[IO.File]::OpenRead($raw);$output=[IO.File]::Create($temporary);$gzip=[IO.Compression.GZipStream]::new($output,[IO.Compression.CompressionLevel]::SmallestSize)
            try{$input.CopyTo($gzip)}finally{$gzip.Dispose();$output.Dispose();$input.Dispose()}
            Move-Item -LiteralPath $temporary -Destination $archive -Force
        }finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
    }
    if(-not (Test-Path -LiteralPath $archive -PathType Leaf)){throw "Replay fixture archive unavailable: $archive"}
    $archiveInput=[IO.File]::OpenRead($archive);$archiveGzip=[IO.Compression.GZipStream]::new($archiveInput,[IO.Compression.CompressionMode]::Decompress)
    try{$archiveHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($archiveGzip))}finally{$archiveGzip.Dispose();$archiveInput.Dispose()}
    if($archiveHash -ne $entry.sha256){throw "Replay fixture archive content hash mismatch: $archive"}
    $valid=Test-Path -LiteralPath $raw -PathType Leaf
    if($valid){$file=Get-Item $raw;$valid=$file.Length -eq [int64]$entry.bytes -and (Get-FileHash $raw -Algorithm SHA256).Hash -eq $entry.sha256}
    if(-not $valid){
        New-Item -ItemType Directory -Path (Split-Path $raw) -Force|Out-Null
        $temporary=$raw+'.tmp'
        try{
            $input=[IO.File]::OpenRead($archive);$gzip=[IO.Compression.GZipStream]::new($input,[IO.Compression.CompressionMode]::Decompress);$output=[IO.File]::Create($temporary)
            try{$gzip.CopyTo($output);$output.Flush($true)}finally{$output.Dispose();$gzip.Dispose();$input.Dispose()}
            if((Get-Item $temporary).Length -ne [int64]$entry.bytes -or (Get-FileHash $temporary -Algorithm SHA256).Hash -ne $entry.sha256){throw "Materialized replay fixture failed verification: $($entry.path)"}
            Move-Item -LiteralPath $temporary -Destination $raw -Force
        }finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
    }
}
function Write-Manifest([string]$Path,[object[]]$Entries){
    $full=[IO.Path]::GetFullPath((Join-Path $repo $Path));New-Item -ItemType Directory -Path (Split-Path $full) -Force|Out-Null
    [string[]]$value=@($Entries.path)
    ConvertTo-Json -InputObject $value|Set-Content -LiteralPath $full -Encoding utf8
}
Write-Manifest $configuration.baselineManifest @($configuration.files|Where-Object group -eq 'baseline')
Write-Manifest $configuration.jst776Manifest @($configuration.files|Where-Object group -eq 'jst776')
Write-Manifest $configuration.jst771Manifest @($configuration.files|Where-Object group -eq 'jst771')
[pscustomobject]@{files=$configuration.files.Count;bytes=[int64](($configuration.files|Measure-Object bytes -Sum).Sum);createdArchives=[bool]$CreateArchives}|ConvertTo-Json
