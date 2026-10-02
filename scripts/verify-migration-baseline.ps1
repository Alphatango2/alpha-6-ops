param(
    [Parameter(Mandatory = $true)][string]$ReplayManifest,
    [string]$DotNetRoot = (Join-Path $PSScriptRoot '../work/dotnet'),
    [string]$ProfileDirectory = (Join-Path $env:LOCALAPPDATA 'Alpha6Designs/Alpha6OPS'),
    [string]$PythonPath = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnet = Join-Path ([IO.Path]::GetFullPath($DotNetRoot)) 'dotnet.exe'
$manifest = (Resolve-Path -LiteralPath $ReplayManifest).Path
$inputs = @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)
if ($inputs.Count -ne 9) { throw 'The baseline requires all nine checkpoint 7.17 journals.' }
foreach ($inputPath in $inputs) { if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) { throw "Journal unavailable: $inputPath" } }
foreach ($existing in @(Get-Process -Name Alpha6OPS,Sierra4 -ErrorAction SilentlyContinue)) {
    # Windows can retain a terminated process entry after a diagnostic crash.
    # Fail closed unless both independent liveness counts confirm it cannot run.
    $terminated = $false
    try {
        $entry = Get-CimInstance Win32_Process -Filter "ProcessId=$($existing.Id)" -ErrorAction Stop
        $terminated = $null -ne $entry -and $null -ne $entry.ThreadCount -and $null -ne $entry.HandleCount -and
            $entry.ThreadCount -eq 0 -and [int]$entry.HandleCount -eq 0
    } catch { $terminated = $false }
    if (-not $terminated) { throw 'Exit the running OPS application before activation checks and profile-copy validation.' }
    Write-Warning "Ignoring terminated OPS process entry $($existing.Id) (zero threads and handles)."
}
$run = Join-Path $repo ('work/checkpoint-7.18a/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $run | Out-Null
$builds = [Collections.Generic.List[object]]::new()
$groups = [Collections.Generic.List[object]]::new()
$oldRoot = $env:DOTNET_ROOT
$oldCliHome = $env:DOTNET_CLI_HOME
$env:DOTNET_ROOT = [IO.Path]::GetFullPath($DotNetRoot)
$env:DOTNET_CLI_HOME = Join-Path $repo 'work/dotnet-home'
Push-Location $repo

function Invoke-Console([string]$Executable, [string[]]$Arguments, [string]$Log) {
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $Executable @Arguments 2>&1 | Out-File -LiteralPath $Log -Encoding utf8
        return $LASTEXITCODE
    } finally { $ErrorActionPreference = $savedPreference }
}
function Invoke-App([string]$Executable, [string[]]$Arguments, [string]$Name, [int]$TimeoutSeconds = 180) {
    $quoted = @($Arguments | ForEach-Object { '"' + $_ + '"' })
    $child = Start-Process -FilePath $Executable -ArgumentList $quoted -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run ($Name + '.stdout.txt')) -RedirectStandardError (Join-Path $run ($Name + '.stderr.txt'))
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not $child.WaitForExit(1000)) {
        if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) { Stop-Process -Id $child.Id; throw "$Name timed out; partial evidence: $run" }
    }
    $child.WaitForExit()
    return $child.ExitCode
}
function Read-Report([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required report missing: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}
function Add-Checks([string]$Name, $Report, [int]$Count) {
    if ($Report.passed -ne $true) { throw "$Name did not pass; see $run" }
    if ($Count -le 0) { throw "$Name reported no checks." }
    $groups.Add([pscustomobject]@{ name = $Name; executed = $Count; passed = $Count; failed = 0; skipped = 0 })
    Write-Host "$Name`: $Count passed"
}

try {
    Copy-Item -LiteralPath $manifest -Destination (Join-Path $run 'replay-manifest.json')
    $beforeHashes = @($inputs | ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 })
    $beforeHashes | Select-Object Path, Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'input-hashes.json') -Encoding utf8
    $code = Invoke-Console $dotnet @('build', 'Alpha6Ops.slnx', '-c', 'Release', '--configfile', 'NuGet.Config') (Join-Path $run 'build.log')
    $builds.Add([pscustomobject]@{ name = 'production solution Release'; exitCode = $code })
    if ($code -ne 0) { throw 'Production solution build failed; no automatic repair attempted.' }
    Write-Host 'Production solution: built'
    $code = Invoke-Console $dotnet @('run', '--project', 'tests/Alpha6Ops.Tests', '-c', 'Release', '--no-build') (Join-Path $run 'core.log')
    $coreLog = Get-Content -LiteralPath (Join-Path $run 'core.log') -Raw
    $coreCount = ([regex]::Matches($coreLog, '(?m)^PASS ')).Count
    $coreSummary = [regex]::Match($coreLog, '(\d+) checks passed\.')
    if ($code -ne 0 -or -not $coreSummary.Success -or [int]$coreSummary.Groups[1].Value -ne $coreCount) { throw 'Core regression execution/report mismatch.' }
    Add-Checks 'Core' ([pscustomobject]@{ passed = $true }) $coreCount

    $app = Join-Path $repo 'src/Alpha6Ops.Desktop/bin/Release/net10.0-windows10.0.17763.0/Sierra4.exe'
    & (Join-Path $repo 'packaging/build-desktop.ps1') -DotNetRoot $DotNetRoot *>&1 | Out-File -LiteralPath (Join-Path $run 'package-build.log') -Encoding utf8
    $builds.Add([pscustomobject]@{ name = 'normal production packaging'; exitCode = 0 })
    Get-FileHash -LiteralPath (Join-Path $repo 'outputs/Sierra4-Desktop-0.23.0/Sierra4.dll'), (Join-Path $repo 'outputs/Sierra4-Desktop-0.23.0/Alpha6Ops.Core.dll'), (Join-Path $repo 'outputs/Sierra4-Setup-0.23.0.exe'), (Join-Path $repo 'outputs/Sierra4-Desktop-0.23.0-win-x64.zip') | Select-Object Path, Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'build-hashes.json') -Encoding utf8
    Write-Host 'Normal production packaging: built (not installed)'
    $code = Invoke-Console $dotnet @('build','src/Alpha6Ops.Desktop','-c','Release','--no-restore','-p:IncludeDiagnosticHarnesses=true') (Join-Path $run 'development-desktop-rebuild.log')
    if($code -ne 0){throw 'Development desktop rebuild failed after production packaging.'}

    $desktopDirectory = Join-Path $run 'desktop'
    $code = Invoke-App $app @('--smoke-test', $desktopDirectory) 'desktop'
    if ($code -ne 0 -or (Test-Path -LiteralPath (Join-Path $desktopDirectory 'desktop-smoke-error.txt'))) { throw 'Desktop smoke failed; no automatic repair attempted.' }
    foreach ($name in @('desktop-smoke', 'dashboard-smoke', 'refresh-smoke', 'live-identity-smoke', 'diagnostic-index-smoke', 'monitor-lifecycle-smoke', 'tracking-integration')) {
        $report = Read-Report (Join-Path $desktopDirectory ($name + '.json'))
        Add-Checks $name $report @($report.checks).Count
    }
    $code = Invoke-App $app @('--activation-smoke', $desktopDirectory) 'activation' 30
    if ($code -ne 0) { throw 'Activation process failed.' }
    Add-Checks 'single-instance activation' (Read-Report (Join-Path $desktopDirectory 'activation-smoke.json')) 1

    $replayDirectory = Join-Path $run 'replays'
    $code = Invoke-App $app @('--replay-journals', $manifest, $replayDirectory) 'replay'
    $replays = @(Read-Report (Join-Path $replayDirectory 'summary.json'))
    if ($code -ne 0 -or $replays.Count -ne 9 -or @($replays | Where-Object { $_.passed -ne $true }).Count -gt 0) { throw 'Recorded-flight replay validation failed.' }
    foreach ($before in $beforeHashes) { if ((Get-FileHash -LiteralPath $before.Path -Algorithm SHA256).Hash -ne $before.Hash) { throw "Journal changed during replay: $($before.Path)" } }
    Add-Checks 'recorded-flight validations' ([pscustomobject]@{ passed = $true }) $replays.Count

    $code = Invoke-Console $dotnet @('build', 'tests/Alpha6Ops.MigrationChecks', '-c', 'Release', '--configfile', 'NuGet.Config') (Join-Path $run 'migration-build.log')
    $builds.Add([pscustomobject]@{ name = 'test-only migration harness'; exitCode = $code })
    if ($code -ne 0) { throw 'Migration harness build failed.' }
    $migrationDirectory = Join-Path $run 'migration'
    $migrationExecutable = Join-Path $repo 'tests/Alpha6Ops.MigrationChecks/bin/Release/net10.0-windows10.0.17763.0/Alpha6Ops.MigrationChecks.exe'
    $migrationExit = Invoke-App $migrationExecutable @($migrationDirectory, $manifest, $replayDirectory, $ProfileDirectory) 'migration'
    $migration = Read-Report (Join-Path $migrationDirectory 'migration-checks.json')
    if ($migrationExit -notin @(0, 1)) { throw "Migration harness terminated abnormally: $migrationExit" }
    if (@($migration.checks | Where-Object { $_.Id -like 'HARNESS*' }).Count -gt 0) { throw 'Migration harness encountered an infrastructure error; inspect migration-checks.json.' }
    $counts = $migration.counts
    $groups.Add([pscustomobject]@{ name = 'migration contracts and compatibility'; executed = $counts.executed; passed = $counts.passed; failed = $counts.failed; skipped = $counts.skipped })
    $largeIndex = Read-Report (Join-Path $migrationDirectory 'large-index/diagnostic-index-smoke.json')
    Add-Checks 'explicit large diagnostic-index suite' $largeIndex @($largeIndex.checks).Count
    $coverage = Read-Report (Join-Path $migrationDirectory 'replay-coverage.json')
    if ($coverage.processedSamples -ne 91366 -or @($coverage.flights).Count -ne 9) { throw 'Telemetry coverage differs from checkpoint 7.17.' }

    if (Test-Path -LiteralPath $PythonPath -PathType Leaf) {
        $code = Invoke-Console $PythonPath @('-m', 'unittest', 'discover', '-s', 'tests', '-p', 'test_*.py') (Join-Path $run 'fleet-import.log')
        $pythonLog = Get-Content -LiteralPath (Join-Path $run 'fleet-import.log') -Raw
        $pythonSummary = [regex]::Match($pythonLog, 'Ran (\d+) tests')
        if ($code -ne 0 -or -not $pythonSummary.Success) { throw 'Existing fleet-import regression failed.' }
        Add-Checks 'fleet import' ([pscustomobject]@{ passed = $true }) ([int]$pythonSummary.Groups[1].Value)
    } else {
        $groups.Add([pscustomobject]@{ name = 'fleet import: Python unavailable'; executed = 0; passed = 0; failed = 0; skipped = 6 })
    }

    $totals = [pscustomobject]@{
        executed = [int](($groups | Measure-Object executed -Sum).Sum)
        passed = [int](($groups | Measure-Object passed -Sum).Sum)
        failed = [int](($groups | Measure-Object failed -Sum).Sum)
        skipped = [int](($groups | Measure-Object skipped -Sum).Sum)
    }
    $summary = [pscustomobject]@{
        baselineCompleted = $true; allMigrationContractsPassed = ($totals.failed -eq 0)
        sourceCommit = (& git rev-parse HEAD); builds = $builds; counts = $totals; groups = $groups
        replayCount = $replays.Count; telemetrySamples = $coverage.processedSamples; rawTelemetrySamples = $coverage.rawSamples
        migrationFailures = @($migration.checks | Where-Object { $_.Status -eq 'Failed' })
        skippedChecks = @($migration.checks | Where-Object { $_.Status -eq 'Skipped' }); evidenceDirectory = $run
        countConvention = 'Reported assertion executions, including the explicit large-index suite; one validation per recorded flight. Builds and preliminary development/rerun attempts are excluded.'
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8
    $summary | ConvertTo-Json -Depth 8
    if ($totals.failed -gt 0) { exit 1 }
    if ($totals.skipped -gt 0) { Write-Warning 'Automated checks passed, but unavailable/manual checks remain listed.' }
} catch {
    [pscustomobject]@{ baselineCompleted = $false; error = $_.ToString(); builds = $builds; completedGroups = $groups; evidenceDirectory = $run } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'incomplete.json') -Encoding utf8
    Write-Error "Baseline incomplete. $($_.Exception.Message) Evidence: $run"
    exit 2
} finally {
    Pop-Location
    $env:DOTNET_ROOT = $oldRoot
    $env:DOTNET_CLI_HOME = $oldCliHome
}
