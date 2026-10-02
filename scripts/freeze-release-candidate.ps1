[CmdletBinding()]
param(
    [string]$AcceptanceDirectory = (Join-Path $PSScriptRoot '../work/checkpoint-7.26b-final-acceptance'),
    [string]$PackageContract = (Join-Path $PSScriptRoot '../work/checkpoint-7.26b-package-contract/package-contract.json'),
    [string]$CandidateId
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputs = Join-Path $repo 'outputs'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function FileRecord([string]$Root, [IO.FileInfo]$File) {
    [pscustomobject]@{
        path = [IO.Path]::GetRelativePath($Root, $File.FullName).Replace('\', '/')
        bytes = [int64]$File.Length
        sha256 = (Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256).Hash
    }
}

Push-Location $repo
try {
    Require (-not (Get-Process -Name Alpha6OPS,Sierra4 -ErrorAction SilentlyContinue)) 'Exit Sierra 4 / Alpha 6 OPS before freezing the candidate.'
    $sourceCommit = (& git rev-parse HEAD).Trim()
    Require ($LASTEXITCODE -eq 0 -and $sourceCommit -match '^[0-9a-f]{40}$') 'Unable to resolve the source commit.'
    $status = @(& git status --porcelain --untracked-files=all)
    Require ($LASTEXITCODE -eq 0) 'Unable to read Git status.'
    Require ($status.Count -eq 0) ('The source tree must be clean before freezing. Changes: ' + ($status -join '; '))

    $acceptanceRoot = [IO.Path]::GetFullPath($AcceptanceDirectory)
    $acceptanceSummaryPath = Join-Path $acceptanceRoot 'summary.json'
    Require (Test-Path -LiteralPath $acceptanceSummaryPath -PathType Leaf) "Acceptance summary is missing: $acceptanceSummaryPath"
    $acceptance = Get-Content -LiteralPath $acceptanceSummaryPath -Raw | ConvertFrom-Json
    Require ($acceptance.completed -eq $true -and [int]$acceptance.counts.failed -eq 0) 'The full 7.25A acceptance gate is not clean.'

    $packageContractPath = [IO.Path]::GetFullPath($PackageContract)
    Require (Test-Path -LiteralPath $packageContractPath -PathType Leaf) "Package contract is missing: $packageContractPath"
    $packageContractReport = Get-Content -LiteralPath $packageContractPath -Raw | ConvertFrom-Json
    Require ($packageContractReport.passed -eq $true) 'The production package contract did not pass.'

    $publish = Join-Path $outputs 'Sierra4-Desktop-0.23.0'
    $zip = Join-Path $outputs 'Sierra4-Desktop-0.23.0-win-x64.zip'
    $installer = Join-Path $outputs 'Sierra4-Setup-0.23.0.exe'
    foreach ($required in @($publish, $zip, $installer)) {
        Require (Test-Path -LiteralPath $required) "Required production artifact is missing: $required"
    }
    Require ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -eq $packageContractReport.zip.sha256) 'Portable ZIP differs from the verified package contract.'
    Require ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -eq $packageContractReport.installer.sha256) 'Installer differs from the verified package contract.'

    if (-not $CandidateId) {
        $CandidateId = 'S4-7.26B-PRE-UI-' + (Get-Date -Format 'yyyyMMdd') + '-' + $sourceCommit.Substring(0, 12)
    }
    Require ($CandidateId -match '^S4-7\.26B-PRE-UI-[A-Za-z0-9.-]+$') 'CandidateId must begin with S4-7.26B-PRE-UI- and contain only safe filename characters.'
    $candidate = Join-Path $outputs ('Sierra4-Desktop-' + $CandidateId)
    Require (-not (Test-Path -LiteralPath $candidate)) "Candidate already exists: $candidate"
    New-Item -ItemType Directory -Path $candidate | Out-Null

    $portable = Join-Path $candidate 'portable'
    Copy-Item -LiteralPath $publish -Destination $portable -Recurse
    $frozenZip = Join-Path $candidate ('Sierra4-Desktop-0.23.0-' + $CandidateId + '-win-x64.zip')
    $frozenInstaller = Join-Path $candidate ('Sierra4-Setup-0.23.0-' + $CandidateId + '.exe')
    Copy-Item -LiteralPath $zip -Destination $frozenZip
    Copy-Item -LiteralPath $installer -Destination $frozenInstaller

    $evidence = Join-Path $candidate 'evidence'
    New-Item -ItemType Directory -Path $evidence | Out-Null
    Copy-Item -LiteralPath $acceptanceSummaryPath -Destination (Join-Path $evidence 'checkpoint-7.25a-summary.json')
    $baseSummary = Join-Path $acceptanceRoot 'checkpoint-7.25/summary.json'
    if (Test-Path -LiteralPath $baseSummary -PathType Leaf) {
        Copy-Item -LiteralPath $baseSummary -Destination (Join-Path $evidence 'checkpoint-7.25-summary.json')
    }
    Copy-Item -LiteralPath $packageContractPath -Destination (Join-Path $evidence 'package-contract.json')
    foreach ($relative in @(
        'docs/checkpoint-7.26b/implementation-report.md',
        'docs/checkpoint-7.26b/pre-premium-ui-handoff.md',
        'docs/checkpoint-7.26b/known-limitations.md',
        'docs/checkpoint-7.26b/archive-verification.json',
        'docs/checkpoint-7.26b/cleanup-ledger.json'
    )) {
        Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination $evidence
    }

    $sourceSnapshot = Join-Path $candidate ('source-' + $sourceCommit.Substring(0, 12) + '.zip')
    & git archive --format=zip --output=$sourceSnapshot $sourceCommit
    Require ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $sourceSnapshot -PathType Leaf)) 'Unable to create the source snapshot.'

    $sourceRows = @(& git ls-tree -r --name-only $sourceCommit | ForEach-Object {
        $relative = $_
        $path = Join-Path $repo $relative
        Require (Test-Path -LiteralPath $path -PathType Leaf) "Tracked source file is absent from the clean checkout: $relative"
        $file = Get-Item -LiteralPath $path
        [pscustomobject]@{ path=$relative.Replace('\','/'); bytes=[int64]$file.Length; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    })
    $sourceManifest = [ordered]@{
        schema = 'sierra4.source-manifest.v1'
        commit = $sourceCommit
        sourceIsDirty = $false
        files = $sourceRows
    }
    $sourceManifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $candidate 'source-manifest.json') -Encoding utf8

    $portableRows = @(Get-ChildItem -LiteralPath $portable -File -Recurse | ForEach-Object { FileRecord $portable $_ } | Sort-Object path)
    $candidateRecord = [ordered]@{
        schema = 'sierra4.release-candidate.v1'
        candidateId = $CandidateId
        createdAt = (Get-Date).ToString('o')
        checkpoint = '7.26B'
        boundary = 'PRE-PREMIUM-UI'
        product = 'Sierra 4'
        version = '0.23.0'
        sourceCommit = $sourceCommit
        sourceIsDirty = $false
        installed = $false
        installPath = '%LOCALAPPDATA%\Programs\Sierra 4'
        installedFootprint = [ordered]@{
            files = $portableRows.Count
            bytes = [int64](($portableRows | Measure-Object bytes -Sum).Sum)
            fingerprint = $packageContractReport.publish.fingerprint
        }
        installer = [ordered]@{ file=[IO.Path]::GetFileName($frozenInstaller); sha256=(Get-FileHash -LiteralPath $frozenInstaller -Algorithm SHA256).Hash }
        portableZip = [ordered]@{ file=[IO.Path]::GetFileName($frozenZip); sha256=(Get-FileHash -LiteralPath $frozenZip -Algorithm SHA256).Hash }
        sourceSnapshot = [ordered]@{ file=[IO.Path]::GetFileName($sourceSnapshot); sha256=(Get-FileHash -LiteralPath $sourceSnapshot -Algorithm SHA256).Hash }
        acceptance = [ordered]@{ executed=[int]$acceptance.counts.executed; passed=[int]$acceptance.counts.passed; failed=[int]$acceptance.counts.failed; skipped=[int]$acceptance.counts.skipped }
    }
    $candidateRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $candidate 'candidate.json') -Encoding utf8

    $manifestPath = Join-Path $candidate 'manifest.json'
    $manifestRows = @(Get-ChildItem -LiteralPath $candidate -File -Recurse | Where-Object FullName -ne $manifestPath | ForEach-Object { FileRecord $candidate $_ } | Sort-Object path)
    $manifest = [ordered]@{
        schema = 'sierra4.frozen-manifest.v1'
        candidateId = $CandidateId
        sourceCommit = $sourceCommit
        selfExcluded = 'manifest.json'
        files = $manifestRows
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

    $verified = 0
    foreach ($row in (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).files) {
        $path = Join-Path $candidate $row.path
        Require (Test-Path -LiteralPath $path -PathType Leaf) "Frozen file is missing: $($row.path)"
        $file = Get-Item -LiteralPath $path
        Require ([int64]$file.Length -eq [int64]$row.bytes) "Frozen file size changed: $($row.path)"
        Require ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $row.sha256) "Frozen file hash changed: $($row.path)"
        $verified++
    }
    Require ($verified -eq $manifestRows.Count) 'Frozen manifest verification count mismatch.'

    [pscustomobject]@{
        candidateId = $CandidateId
        path = $candidate
        sourceCommit = $sourceCommit
        sourceIsDirty = $false
        installed = $false
        installerSha256 = $candidateRecord.installer.sha256
        portableSha256 = $candidateRecord.portableZip.sha256
        footprintFiles = $candidateRecord.installedFootprint.files
        footprintBytes = $candidateRecord.installedFootprint.bytes
        manifestVerified = $verified
        manifestTotal = $manifestRows.Count
    } | ConvertTo-Json -Depth 5
} finally {
    Pop-Location
}
