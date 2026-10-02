param(
    [string]$DotNetRoot = (Join-Path $PSScriptRoot '../work/dotnet'),
    [string]$ReplayRegistry = (Join-Path $PSScriptRoot 'replay-fixtures.json'),
    [string]$Dal40Journal,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$registry = Get-Content -LiteralPath $ReplayRegistry -Raw | ConvertFrom-Json
& (Join-Path $PSScriptRoot 'materialize-replay-fixtures.ps1') -Registry $ReplayRegistry | Out-Null
if($LASTEXITCODE){throw 'Replay fixture materialization failed.'}
if (!$Dal40Journal) { $Dal40Journal = Join-Path $repo $registry.dal40Journal }
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $repo ('work/checkpoint-7.25a/acceptance-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
}
$run = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $run) { throw "Use a fresh evidence directory: $run" }
if (-not (Test-Path -LiteralPath $Dal40Journal -PathType Leaf)) { throw "DAL40 journal unavailable: $Dal40Journal" }
if (Get-Process -Name Alpha6OPS,Sierra4 -ErrorAction SilentlyContinue) { throw 'Exit Sierra 4 / Alpha 6 OPS before checkpoint verification.' }

New-Item -ItemType Directory -Path $run | Out-Null
$dal40HashBefore = (Get-FileHash -LiteralPath $Dal40Journal -Algorithm SHA256).Hash

function Invoke-App([string]$Executable,[string[]]$Arguments,[string]$Name,[int]$TimeoutSeconds = 300) {
    $quoted = @($Arguments | ForEach-Object { '"' + $_ + '"' })
    $child = Start-Process -FilePath $Executable -ArgumentList $quoted -WorkingDirectory $repo -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $run ($Name + '.stdout.txt')) -RedirectStandardError (Join-Path $run ($Name + '.stderr.txt'))
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not $child.WaitForExit(1000)) {
        if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) { Stop-Process -Id $child.Id; throw "$Name timed out; evidence: $run" }
    }
    $child.WaitForExit()
    if ($child.ExitCode -ne 0) { throw "$Name failed with exit $($child.ExitCode); evidence: $run" }
}

Push-Location $repo
try {
    $base = Join-Path $run 'checkpoint-7.25'
    $powershell = Join-Path $PSHOME 'pwsh.exe'
    $baseProcess = Start-Process -FilePath $powershell -ArgumentList @(
        '-NoProfile','-File',(Join-Path $repo 'scripts/verify-checkpoint-7.25.ps1'),
        '-DotNetRoot',[IO.Path]::GetFullPath($DotNetRoot),'-OutputDirectory',$base
    ) -WorkingDirectory $repo -WindowStyle Hidden -PassThru -Wait `
        -RedirectStandardOutput (Join-Path $run 'checkpoint-7.25.stdout.txt') `
        -RedirectStandardError (Join-Path $run 'checkpoint-7.25.stderr.txt')
    if ($baseProcess.ExitCode -ne 0) { throw "Checkpoint 7.25 base verification failed with exit $($baseProcess.ExitCode)." }
    $baseSummary = Get-Content -LiteralPath (Join-Path $base 'summary.json') -Raw | ConvertFrom-Json
    if ($baseSummary.completed -ne $true -or [int]$baseSummary.counts.failed -ne 0) { throw 'Checkpoint 7.25 base verification did not report a clean result.' }

    $app = Join-Path $repo 'src/Alpha6Ops.Desktop/bin/Release/net10.0-windows10.0.17763.0/Sierra4.exe'
    $observed = Join-Path $run 'observed-cruise-native'
    New-Item -ItemType Directory -Path $observed | Out-Null
    Invoke-App $app @('--observed-cruise-native-checks',$observed) 'observed-cruise-native' 60
    $observedReport = Get-Content -LiteralPath (Join-Path $observed 'observed-cruise-native.json') -Raw | ConvertFrom-Json
    if ($observedReport.passed -ne $true -or [int]$observedReport.failed -ne 0 -or [int]$observedReport.executed -lt 19) {
        throw 'Observed-cruise native profile verification failed or omitted required coverage.'
    }

    $input = Join-Path $run 'dal40-input'
    New-Item -ItemType Directory -Path $input | Out-Null
    $manifest = Join-Path $input 'replay-manifest.json'
    ConvertTo-Json -InputObject @([IO.Path]::GetFullPath($Dal40Journal)) | Set-Content -LiteralPath $manifest -Encoding utf8
    $dal40 = Join-Path $run 'dal40-replay'
    Invoke-App $app @('--replay-journals',$manifest,$dal40) 'dal40-replay' 300
    $dal40Report = @(Get-Content -LiteralPath (Join-Path $dal40 'summary.json') -Raw | ConvertFrom-Json)
    if ($dal40Report.Count -ne 1 -or $dal40Report[0].passed -ne $true -or [int]$dal40Report[0].samples -ne 54061) {
        throw 'DAL40 authoritative replay did not report the required 54,061-sample pass.'
    }

    $dal40HashAfter = (Get-FileHash -LiteralPath $Dal40Journal -Algorithm SHA256).Hash
    if ($dal40HashAfter -ne $dal40HashBefore) { throw 'DAL40 source journal changed during verification.' }
    $counts = [pscustomobject]@{
        executed = [int]$baseSummary.counts.executed + [int]$observedReport.executed + 1
        passed = [int]$baseSummary.counts.passed + [int]$observedReport.executed + 1
        failed = 0
        skipped = [int]$baseSummary.counts.skipped
    }
    $summary = [pscustomobject]@{
        checkpoint='7.25A';completed=$true;counts=$counts
        historicalReplayCount=[int]$baseSummary.replayCount
        historicalTelemetrySamples=[int]$baseSummary.telemetrySamples
        dal40AuthoritativeReplayCount=1
        dal40TelemetrySamples=54061
        totalRecordedTelemetrySamples=[int]$baseSummary.telemetrySamples + 54061
        observedCruiseNativeChecks=[int]$observedReport.executed
        dal40Journal=[IO.Path]::GetFullPath($Dal40Journal)
        dal40JournalSha256=$dal40HashBefore
        baseEvidence=$base
        evidenceDirectory=$run
        packageCreated=$false
        nativeGate='One realistic shallow A350 cruise-level transition plus greater-than-2,048-point track, hidden-workspace, renderer-recreation and TOD verification; the three DAL40 step profiles remain authoritative automated regression evidence.'
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8
    $summary | ConvertTo-Json -Depth 8
} catch {
    [pscustomobject]@{checkpoint='7.25A';completed=$false;error=$_.ToString();evidenceDirectory=$run;packageCreated=$false} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'incomplete.json') -Encoding utf8
    Write-Error "Checkpoint 7.25A verification incomplete. $($_.Exception.Message) Evidence: $run"
    exit 2
} finally {
    Pop-Location
}
