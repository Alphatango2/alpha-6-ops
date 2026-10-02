[CmdletBinding()]
param(
    [string]$PlanPath = 'docs/checkpoint-7.26b/archive-plan.json',
    [string]$VerificationPath = 'docs/checkpoint-7.26b/archive-verification.json',
    [string]$PreMutationLedgerPath = 'docs/checkpoint-7.26b/pre-mutation-ledger.json',
    [string]$CleanupLedgerPath = 'docs/checkpoint-7.26b/cleanup-ledger.json'
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
$verification = Get-Content -LiteralPath $VerificationPath -Raw | ConvertFrom-Json
$preMutation = Get-Content -LiteralPath $PreMutationLedgerPath -Raw | ConvertFrom-Json

function Resolve-RepositoryPath([string]$RelativePath) {
    $full = [IO.Path]::GetFullPath((Join-Path $repo $RelativePath))
    if (-not $full.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target is outside the repository: $full"
    }
    return $full
}

function Get-Files([string]$Root) {
    if (Test-Path -LiteralPath $Root -PathType Leaf) { return @((Get-Item -LiteralPath $Root -Force)) }
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

function Inventory([string]$Path) {
    $files = @(Get-Files $Path)
    $rows = @($files | ForEach-Object { [pscustomobject]@{ relative = Relative $Path $_.FullName; bytes = [int64]$_.Length } })
    return [pscustomobject]@{
        files = $rows.Count
        bytes = [int64](($rows | Measure-Object bytes -Sum).Sum)
        inventorySha256 = Fingerprint $rows
    }
}

function Remove-VerifiedTarget([string]$RelativePath) {
    $target = Resolve-RepositoryPath $RelativePath
    if (-not (Test-Path -LiteralPath $target)) { return $false }
    Remove-Item -LiteralPath $target -Recurse -Force
    if (Test-Path -LiteralPath $target) { throw "Cleanup target still exists after removal: $target" }
    return $true
}

if (-not $verification.passed) { throw 'Archive verification did not pass; cleanup is forbidden.' }
$manifestPath = [IO.Path]::GetFullPath($verification.manifestPath)
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "External archive manifest is missing: $manifestPath" }
$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
if ($manifestHash -ne $verification.manifestSha256) { throw 'External archive manifest hash changed after verification.' }

$trackedFailures = [Collections.Generic.List[string]]::new()
foreach ($entry in $preMutation.git.trackedModifications) {
    $path = Resolve-RepositoryPath $entry.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $trackedFailures.Add("missing:$($entry.path)")
        continue
    }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($hash -ne $entry.workingSha256) { $trackedFailures.Add("changed:$($entry.path)") }
}
if ($trackedFailures.Count -gt 0) {
    throw "Pre-existing tracked changes no longer match the pre-mutation ledger: $($trackedFailures -join ', ')"
}

$verifiedUnits = @{}
foreach ($unit in $verification.units) { $verifiedUnits[$unit.source] = $unit }
$archiveChecks = [Collections.Generic.List[object]]::new()
foreach ($unit in $plan.archiveUnits | Where-Object disposition -EQ 'archive-then-remove') {
    $recorded = $verifiedUnits[$unit.source]
    if ($null -eq $recorded) { throw "Archive verification has no unit for $($unit.source)" }
    $source = Resolve-RepositoryPath $unit.source
    if (-not (Test-Path -LiteralPath $source)) { throw "Archive source disappeared before cleanup: $($unit.source)" }
    $actual = Inventory $source
    if ($actual.files -ne $recorded.files -or $actual.bytes -ne $recorded.bytes -or $actual.inventorySha256 -ne $recorded.inventorySha256) {
        throw "Archive source changed after verification: $($unit.source)"
    }
    $archiveChecks.Add([ordered]@{
        path=$unit.source; destination=$unit.destination; auditClass=$unit.auditClass
        files=$actual.files; bytes=$actual.bytes; inventorySha256=$actual.inventorySha256
    })
}

$removed = [Collections.Generic.List[object]]::new()
foreach ($unit in $plan.archiveUnits | Where-Object disposition -EQ 'archive-then-remove') {
    $recorded = $verifiedUnits[$unit.source]
    if (Remove-VerifiedTarget $unit.source) {
        $removed.Add([ordered]@{
            path=$unit.source; auditClass=$unit.auditClass; disposition='archived-and-removed'
            reason='Explicit archive unit verified against the immutable external manifest.'
            files=[int]$recorded.files; bytes=[int64]$recorded.bytes
        })
    }
}

foreach ($candidate in $plan.cleanupCandidates) {
    $path = Resolve-RepositoryPath $candidate.path
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $actual = Inventory $path
    if (Remove-VerifiedTarget $candidate.path) {
        $removed.Add([ordered]@{
            path=$candidate.path; auditClass=$candidate.auditClass; disposition='removed-reproducible-or-temporary'
            reason=$candidate.reason; files=$actual.files; bytes=$actual.bytes
            requiredArchiveVerification=$candidate.requiresArchiveVerification
        })
    }
}

$candidateRoot = Resolve-RepositoryPath 'outputs/Alpha6OPS-Desktop-S4-7.25A-A350-20261001-6020896a402b'
$freezeManifestPath = Join-Path $candidateRoot 'final-freeze-manifest.json'
$freezeManifest = Get-Content -LiteralPath $freezeManifestPath -Raw | ConvertFrom-Json
$candidateFailures = [Collections.Generic.List[string]]::new()
foreach ($entry in $freezeManifest.entries) {
    $path = [IO.Path]::GetFullPath((Join-Path $candidateRoot $entry.path))
    if (-not $path.StartsWith($candidateRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        $candidateFailures.Add("outside:$($entry.path)")
        continue
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $candidateFailures.Add("missing:$($entry.path)")
        continue
    }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) {
        $candidateFailures.Add("hash:$($entry.path)")
    }
}

$fixtureRegistry = Get-Content -LiteralPath 'scripts/replay-fixtures.json' -Raw | ConvertFrom-Json
$fixtureFailures = [Collections.Generic.List[string]]::new()
foreach ($entry in $fixtureRegistry.files) {
    $path = Resolve-RepositoryPath $entry.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $fixtureFailures.Add("missing:$($entry.path)")
        continue
    }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) {
        $fixtureFailures.Add("hash:$($entry.path)")
    }
}

if ($candidateFailures.Count -gt 0) { throw "Frozen 7.25A candidate validation failed: $($candidateFailures -join ', ')" }
if ($fixtureFailures.Count -gt 0) { throw "Regression fixture validation failed: $($fixtureFailures -join ', ')" }

$ledger = [ordered]@{
    schema='sierra4.checkpoint-7.26b.cleanup-ledger.v1'
    completedAt=(Get-Date).ToString('o')
    repository=$repo.Replace('\','/')
    archiveManifest=[ordered]@{path=$manifestPath; sha256=$manifestHash; verified=$true}
    preExistingTrackedChanges=[ordered]@{expected=$preMutation.git.trackedModificationCount; matched=$preMutation.git.trackedModificationCount; failures=@()}
    archiveUnitsChecked=$archiveChecks.Count
    removed=$removed
    totals=[ordered]@{
        targets=$removed.Count
        files=[int](($removed | ForEach-Object { [int]$_['files'] } | Measure-Object -Sum).Sum)
        bytes=[int64](($removed | ForEach-Object { [int64]$_['bytes'] } | Measure-Object -Sum).Sum)
    }
    frozenCandidate=[ordered]@{identity=$preMutation.frozenCandidate.identity; declared=$freezeManifest.files; verified=$freezeManifest.entries.Count; failures=@()}
    replayFixtures=[ordered]@{declared=$fixtureRegistry.files.Count; verified=$fixtureRegistry.files.Count; failures=@()}
}
$ledger | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $CleanupLedgerPath -Encoding utf8
([pscustomobject]$ledger) | Select-Object completedAt,archiveUnitsChecked,totals,frozenCandidate,replayFixtures | ConvertTo-Json -Depth 5
