param(
    [string]$PlanPath = (Join-Path $PSScriptRoot '../docs/checkpoint-7.26b/archive-plan.json'),
    [string]$VerificationPath = (Join-Path $PSScriptRoot '../docs/checkpoint-7.26b/archive-verification.json')
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
$archiveRoot = [IO.Path]::GetFullPath($plan.archiveRoot)
if (-not $archiveRoot.StartsWith('F:\Sierra4-Archive\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Archive root is outside the approved boundary: $archiveRoot"
}
New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
$logRoot = Join-Path $archiveRoot 'copy-logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function Get-Files([string]$Root) {
    if (Test-Path -LiteralPath $Root -PathType Leaf) { return ,(Get-Item -LiteralPath $Root) }
    return @(Get-ChildItem -LiteralPath $Root -File -Recurse -Force | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0 })
}

function Relative([string]$Root, [string]$Path) {
    if (Test-Path -LiteralPath $Root -PathType Leaf) { return [IO.Path]::GetFileName($Path) }
    return [IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/')
}

function Fingerprint([object[]]$Rows) {
    $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        foreach ($row in $Rows | Sort-Object relative) {
            $bytes = [Text.Encoding]::UTF8.GetBytes("$($row.relative)|$($row.bytes)`n")
            $hash.AppendData($bytes)
        }
        return [Convert]::ToHexString($hash.GetHashAndReset())
    } finally { $hash.Dispose() }
}

function Critical([IO.FileInfo]$File) {
    if ($File.Length -ge 16MB) { return $true }
    if ($File.Extension -in @('.jsonl','.sqlite','.db','.zip','.exe','.bundle')) { return $true }
    return $File.Name -match '(?i)(manifest|summary|report|verification|sha256)'
}

$verified = [Collections.Generic.List[object]]::new()
foreach ($unit in $plan.archiveUnits) {
    $source = [IO.Path]::GetFullPath((Join-Path $repo $unit.source))
    $destination = [IO.Path]::GetFullPath((Join-Path $archiveRoot $unit.destination))
    if (-not $source.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Archive source is outside the repository: $source"
    }
    if (-not $destination.StartsWith($archiveRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Archive destination is outside the archive root: $destination"
    }
    if (-not (Test-Path -LiteralPath $source)) { throw "Archive source is missing: $source" }

    if (Test-Path -LiteralPath $source -PathType Leaf) {
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force
    } else {
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        $log = Join-Path $logRoot (([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($unit.source))).Substring(0,16)) + '.log')
        & robocopy $source $destination /E /COPY:DAT /DCOPY:DAT /R:2 /W:2 /MT:16 /XJ /NFL /NDL /NJH /NJS /NP /LOG:$log | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Archive copy failed for $($unit.source) with robocopy exit $LASTEXITCODE. See $log" }
    }

    $sourceFiles = @(Get-Files $source)
    $destinationFiles = @(Get-Files $destination)
    $sourceRows = @($sourceFiles | ForEach-Object { [pscustomobject]@{ relative = Relative $source $_.FullName; bytes = [int64]$_.Length } })
    $destinationRows = @($destinationFiles | ForEach-Object { [pscustomobject]@{ relative = Relative $destination $_.FullName; bytes = [int64]$_.Length } })
    $sourceBytes = [int64](($sourceRows | Measure-Object bytes -Sum).Sum)
    $destinationBytes = [int64](($destinationRows | Measure-Object bytes -Sum).Sum)
    $sourceFingerprint = Fingerprint $sourceRows
    $destinationFingerprint = Fingerprint $destinationRows
    if ($sourceRows.Count -ne $destinationRows.Count -or $sourceBytes -ne $destinationBytes -or $sourceFingerprint -ne $destinationFingerprint) {
        throw "Archive inventory mismatch for $($unit.source)"
    }

    $hashes = [Collections.Generic.List[object]]::new()
    foreach ($file in $sourceFiles | Where-Object { Critical $_ }) {
        $relative = Relative $source $file.FullName
        $copy = if (Test-Path -LiteralPath $destination -PathType Leaf) { $destination } else { Join-Path $destination ($relative.Replace('/','\')) }
        $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) { throw "Archive hash mismatch for $($unit.source)/$relative" }
        $hashes.Add([ordered]@{ path=$relative; bytes=[int64]$file.Length; sha256=$sourceHash })
    }
    $verified.Add([ordered]@{
        source=$unit.source; destination=$unit.destination; auditClass=$unit.auditClass; disposition=$unit.disposition
        files=$sourceRows.Count; bytes=$sourceBytes; inventorySha256=$sourceFingerprint; criticalHashes=$hashes.Count; hashes=$hashes
    })
}

$result = [ordered]@{
    schema='sierra4.checkpoint-7.26b.archive-verification.v1'
    verifiedAt=(Get-Date).ToString('o')
    archiveRoot=$archiveRoot
    passed=$true
    units=$verified
    totals=[ordered]@{
        units=$verified.Count
        files=[int](($verified | ForEach-Object { [int]$_['files'] } | Measure-Object -Sum).Sum)
        bytes=[int64](($verified | ForEach-Object { [int64]$_['bytes'] } | Measure-Object -Sum).Sum)
        criticalHashes=[int](($verified | ForEach-Object { [int]$_['criticalHashes'] } | Measure-Object -Sum).Sum)
    }
}
$externalManifest = Join-Path $archiveRoot 'archive-manifest.json'
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $externalManifest -Encoding utf8
$summary = [ordered]@{
    schema=$result.schema; verifiedAt=$result.verifiedAt; archiveRoot=$archiveRoot; passed=$true
    manifestPath=$externalManifest; manifestSha256=(Get-FileHash -LiteralPath $externalManifest -Algorithm SHA256).Hash
    totals=$result.totals
    units=@($verified | ForEach-Object { [ordered]@{
        source=$_.source; destination=$_.destination; auditClass=$_.auditClass; disposition=$_.disposition
        files=$_.files; bytes=$_.bytes; inventorySha256=$_.inventorySha256; criticalHashes=$_.criticalHashes
    }})
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $VerificationPath -Encoding utf8
$summary | ConvertTo-Json -Depth 4
