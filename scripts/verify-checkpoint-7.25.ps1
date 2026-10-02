param(
    [string]$DotNetRoot = (Join-Path $PSScriptRoot '../work/dotnet'),
    [string]$ReplayRegistry = (Join-Path $PSScriptRoot 'replay-fixtures.json'),
    [string]$BaseReplayManifest,
    [string]$Jst776ReplayManifest,
    [string]$Jst771ReplayManifest,
    [string]$FinalHistoryDatabase,
    [string]$ProfileDirectory = (Join-Path $env:LOCALAPPDATA 'Alpha6Designs/Alpha6OPS'),
    [string]$OutputDirectory,
    [string]$PythonPath = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'),
    [string]$NodePath = (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$registry = Get-Content -LiteralPath $ReplayRegistry -Raw | ConvertFrom-Json
& (Join-Path $PSScriptRoot 'materialize-replay-fixtures.ps1') -Registry $ReplayRegistry | Out-Null
if($LASTEXITCODE){throw 'Replay fixture materialization failed.'}
if (!$BaseReplayManifest) { $BaseReplayManifest = Join-Path $repo $registry.baselineManifest }
if (!$Jst776ReplayManifest) { $Jst776ReplayManifest = Join-Path $repo $registry.jst776Manifest }
if (!$Jst771ReplayManifest) { $Jst771ReplayManifest = Join-Path $repo $registry.jst771Manifest }
if (!$FinalHistoryDatabase) { $FinalHistoryDatabase = Join-Path $repo $registry.finalHistoryDatabase }
$dotnet = Join-Path ([IO.Path]::GetFullPath($DotNetRoot)) 'dotnet.exe'
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo ('work/checkpoint-7.25/acceptance-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)) }
$run = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $run) { throw "Use a fresh evidence directory: $run" }
foreach ($required in @($dotnet,$BaseReplayManifest,$Jst776ReplayManifest,$Jst771ReplayManifest,$FinalHistoryDatabase,$PythonPath,$NodePath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required verification input unavailable: $required" }
}
foreach ($existing in @(Get-Process -Name Alpha6OPS,Sierra4 -ErrorAction SilentlyContinue)) {
    $terminated = $false
    try {
        $entry = Get-CimInstance Win32_Process -Filter "ProcessId=$($existing.Id)" -ErrorAction Stop
        $terminated = $null -ne $entry -and $entry.ThreadCount -eq 0 -and [int]$entry.HandleCount -eq 0
    } catch { $terminated = $false }
    if (-not $terminated) { throw 'Exit the running Sierra 4 / Alpha 6 OPS application before checkpoint verification.' }
}
New-Item -ItemType Directory -Path $run | Out-Null
$groups = [Collections.Generic.List[object]]::new()
$builds = [Collections.Generic.List[object]]::new()
$oldRoot = $env:DOTNET_ROOT
$oldCliHome = $env:DOTNET_CLI_HOME
$env:DOTNET_ROOT = [IO.Path]::GetFullPath($DotNetRoot)
$env:DOTNET_CLI_HOME = Join-Path $repo 'work/dotnet-home'

function Invoke-Console([string]$Executable,[string[]]$Arguments,[string]$Log) {
    $saved = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $Executable @Arguments 2>&1 | Out-File -LiteralPath $Log -Encoding utf8
        return $LASTEXITCODE
    } finally { $ErrorActionPreference = $saved }
}
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
function Read-Report([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required report missing: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}
function Add-Group([string]$Name,[int]$Executed,[int]$Passed,[int]$Failed = 0,[int]$Skipped = 0,[string]$Evidence = '') {
    if ($Executed -lt 0 -or $Passed -lt 0 -or $Failed -lt 0 -or $Skipped -lt 0 -or $Passed + $Failed -ne $Executed) { throw "Invalid count contract for $Name" }
    $groups.Add([pscustomobject]@{name=$Name;executed=$Executed;passed=$Passed;failed=$Failed;skipped=$Skipped;evidence=$Evidence})
    if ($Failed -gt 0) { throw "$Name reported $Failed failure(s)." }
    Write-Host "$Name`: $Passed/$Executed passed$(if($Skipped){", $Skipped skipped"})"
}
function Add-JsonChecks([string]$Name,[string]$Path,[string]$Collection = 'checks') {
    $report = Read-Report $Path
    $items = @($report.$Collection)
    $itemResults = @($items | Where-Object { $_ -isnot [string] -and $_.PSObject.Properties.Name -contains 'passed' })
    $failed = if ($itemResults.Count -eq $items.Count) { @($itemResults | Where-Object { $_.passed -ne $true }).Count }
        elseif ($report.PSObject.Properties.Name -contains 'passed') { if ($report.passed -eq $true) { 0 } else { [Math]::Max(1,$items.Count) } }
        else { throw "$Name has no machine-readable pass result." }
    Add-Group $Name $items.Count ($items.Count-$failed) $failed 0 $Path
}

Push-Location $repo
try {
    (& git rev-parse HEAD) | Set-Content -LiteralPath (Join-Path $run 'source-commit.txt')
    git status --short | Set-Content -LiteralPath (Join-Path $run 'entry-status.txt')
    git diff --binary | Set-Content -LiteralPath (Join-Path $run 'entry.diff')
    $sourceFiles = @(foreach ($top in @('src','tests','scripts','packaging')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo $top) -Recurse -File) {
            $relative = [IO.Path]::GetRelativePath($repo,$file.FullName).Replace('\','/')
            if ($relative -match '/(bin|obj|node_modules|__pycache__)/') { continue }
            [pscustomobject]@{path=$relative;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;bytes=$file.Length}
        }
    }) | Sort-Object path
    $sourceFiles | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'source-manifest-before.json') -Encoding utf8

    $baseInputs = @(Get-Content -LiteralPath $BaseReplayManifest -Raw | ConvertFrom-Json)
    $jst776Inputs = @(Get-Content -LiteralPath $Jst776ReplayManifest -Raw | ConvertFrom-Json)
    $jst771Inputs = @(Get-Content -LiteralPath $Jst771ReplayManifest -Raw | ConvertFrom-Json)
    if ($baseInputs.Count -ne 9 -or $jst776Inputs.Count -ne 1 -or $jst771Inputs.Count -ne 1) { throw 'Replay manifests must resolve to the approved 9 + 1 + 1 set.' }
    $replayInputs = @($baseInputs + $jst776Inputs + $jst771Inputs)
    foreach ($input in $replayInputs) { if (-not (Test-Path -LiteralPath $input -PathType Leaf)) { throw "Replay unavailable: $input" } }
    $inputHashes = @($replayInputs | ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 | Select-Object Path,Hash })
    $inputHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'replay-input-hashes-before.json') -Encoding utf8

    $code = Invoke-Console $dotnet @('build','Alpha6Ops.slnx','-c','Release','--configfile','NuGet.Config','-nodeReuse:false') (Join-Path $run 'build.log')
    $builds.Add([pscustomobject]@{name='production solution Release';exitCode=$code})
    if ($code -ne 0) { throw 'Production solution Release build failed.' }
    $app = Join-Path $repo 'src/Alpha6Ops.Desktop/bin/Release/net10.0-windows10.0.17763.0/Sierra4.exe'
    if (-not (Test-Path -LiteralPath $app -PathType Leaf)) { throw 'Release desktop executable missing after build.' }

    $code = Invoke-Console $dotnet @('run','--project','tests/Alpha6Ops.Tests','-c','Release','--no-build') (Join-Path $run 'core.log')
    $coreText = Get-Content -LiteralPath (Join-Path $run 'core.log') -Raw
    $coreCount = ([regex]::Matches($coreText,'(?m)^PASS ')).Count
    if ($code -ne 0 -or $coreCount -le 0 -or -not [regex]::IsMatch($coreText,"$coreCount checks passed\.")) { throw 'Core regression execution/report mismatch.' }
    Add-Group 'Core' $coreCount $coreCount 0 0 (Join-Path $run 'core.log')

    $desktop = Join-Path $run 'desktop'
    Invoke-App $app @('--smoke-test',$desktop) 'desktop' 180
    if (Test-Path -LiteralPath (Join-Path $desktop 'desktop-smoke-error.txt')) { throw 'Desktop smoke emitted a failure report.' }
    foreach ($name in @('desktop-smoke','dashboard-smoke','refresh-smoke','live-identity-smoke','diagnostic-index-smoke','monitor-lifecycle-smoke','tracking-integration')) {
        $report = Read-Report (Join-Path $desktop ($name + '.json'))
        $count = if ($name -eq 'dashboard-smoke') {[int]$report.count} else {@($report.checks).Count}
        if ($report.passed -ne $true -or $count -le 0) { throw "$name failed or reported no checks." }
        Add-Group $name $count $count 0 0 (Join-Path $desktop ($name + '.json'))
    }
    Invoke-App $app @('--activation-smoke',$desktop) 'activation' 30
    $activation = Read-Report (Join-Path $desktop 'activation-smoke.json')
    if ($activation.passed -ne $true) { throw 'Single-instance activation failed.' }
    Add-Group 'single-instance activation' 1 1 0 0 (Join-Path $desktop 'activation-smoke.json')

    $baselineReplay = Join-Path $run 'replay-baseline-nine'
    Invoke-App $app @('--replay-journals',(Resolve-Path $BaseReplayManifest).Path,$baselineReplay) 'replay-baseline-nine' 300
    $baselineResults = @(Read-Report (Join-Path $baselineReplay 'summary.json'))
    $baselineFailed = @($baselineResults | Where-Object { $_.passed -ne $true }).Count
    if ($baselineResults.Count -ne 9 -or $baselineFailed) { throw 'Nine-flight baseline replay failed.' }
    Add-Group 'recorded flights: baseline nine' 9 9 0 0 (Join-Path $baselineReplay 'summary.json')

    $code = Invoke-Console $dotnet @('build','tests/Alpha6Ops.MigrationChecks','-c','Release','--configfile','NuGet.Config','-nodeReuse:false') (Join-Path $run 'migration-build.log')
    $builds.Add([pscustomobject]@{name='migration harness';exitCode=$code})
    if ($code -ne 0) { throw 'Migration harness build failed.' }
    $migration = Join-Path $run 'migration'
    $migrationApp = Join-Path $repo 'tests/Alpha6Ops.MigrationChecks/bin/Release/net10.0-windows10.0.17763.0/Alpha6Ops.MigrationChecks.exe'
    Invoke-App $migrationApp @($migration,(Resolve-Path $BaseReplayManifest).Path,$baselineReplay,$ProfileDirectory) 'migration' 300
    $migrationReport = Read-Report (Join-Path $migration 'migration-checks.json')
    Add-Group 'migration contracts and compatibility' ([int]$migrationReport.counts.executed) ([int]$migrationReport.counts.passed) ([int]$migrationReport.counts.failed) ([int]$migrationReport.counts.skipped) (Join-Path $migration 'migration-checks.json')
    Add-JsonChecks 'explicit large diagnostic index' (Join-Path $migration 'large-index/diagnostic-index-smoke.json')
    $coverage = Read-Report (Join-Path $migration 'replay-coverage.json')
    if ($coverage.processedSamples -ne 91366 -or @($coverage.flights).Count -ne 9) { throw 'Baseline telemetry coverage differs from 91,366 samples / 9 flights.' }

    $code = Invoke-Console $PythonPath @('-m','unittest','discover','-s','tests','-p','test_*.py') (Join-Path $run 'python-regressions.log')
    $pythonText = Get-Content -LiteralPath (Join-Path $run 'python-regressions.log') -Raw
    $pythonMatch = [regex]::Match($pythonText,'Ran (\d+) tests')
    if ($code -ne 0 -or -not $pythonMatch.Success) { throw 'Python regression suite failed.' }
    $pythonCount = [int]$pythonMatch.Groups[1].Value
    Add-Group 'Python regression tests' $pythonCount $pythonCount 0 0 (Join-Path $run 'python-regressions.log')

    foreach ($suite in @(
        @{Name='terminal and Fenix capability';Argument='--terminal';Directory='terminal'},
        @{Name='active-flight recovery';Argument='--active-restart';Directory='active-restart'},
        @{Name='recovery persistence';Argument='--verify';Directory='recovery'},
        @{Name='Logbook authoritative times';Argument='--logbook';Directory='logbook'})) {
        $directory = Join-Path $run $suite.Directory
        $suiteArguments = @('run','--project','tests/Alpha6Ops.RecoveryChecks','-c','Release','--no-build','--',$suite.Argument,$directory)
        if ($suite.Argument -eq '--logbook') { $suiteArguments += (Resolve-Path $FinalHistoryDatabase).Path }
        $code = Invoke-Console $dotnet $suiteArguments (Join-Path $run ($suite.Directory + '.log'))
        if ($code -ne 0) { throw "$($suite.Name) failed." }
        $reportPath = if ($suite.Argument -eq '--logbook') { Join-Path $directory 'logbook-checks.json' } else { Join-Path $directory 'summary.json' }
        $report = Read-Report $reportPath
        if ($suite.Argument -eq '--logbook' -and [int]$report.executed -lt 40) { throw 'Logbook verification omitted preserved JST500/JST771 history coverage.' }
        Add-Group $suite.Name ([int]$report.executed) ([int]$report.passed) ([int]$report.failed) 0 $reportPath
    }

    $mapFull = Join-Path $run 'map-v2-full'
    & (Join-Path $repo 'scripts/verify-map-v2.ps1') -DotNetRoot $DotNetRoot -NodePath $NodePath -ApplicationPath $app -OutputDirectory $mapFull *> (Join-Path $run 'map-v2-full.log')
    $mapReport = Read-Report (Join-Path $mapFull 'summary.json')
    Add-Group 'Map V2 browser/input/lifecycle' ([int]$mapReport.executed) ([int]$mapReport.executed-[int]$mapReport.failed) ([int]$mapReport.failed) 0 (Join-Path $mapFull 'summary.json')

    foreach ($mode in @('direct-contracts','contracts','interaction-contract','restoration-verify')) {
        $directory = Join-Path $run ('map-' + $mode)
        $minutes = if ($mode -eq 'interaction-contract') {'0.2'} else {'0'}
        Invoke-App $app @('--map-performance-checks',$directory,$mode,$minutes) ('map-' + $mode) 300
        $report = Read-Report (Join-Path $directory 'summary.json')
        Add-Group ('Map ' + $mode) ([int]$report.executed) ([int]$report.executed-[int]$report.failed) ([int]$report.failed) 0 (Join-Path $directory 'summary.json')
    }
    foreach ($arm in @('baseline','contract')) {
        $directory = Join-Path $run ('scheduling-' + $arm)
        & (Join-Path $repo 'scripts/verify-map-scheduling.ps1') -Arm $arm -Seconds 30 -OutputDirectory $directory *> (Join-Path $run ('scheduling-' + $arm + '.log'))
        $report = Read-Report (Join-Path $directory 'summary.json')
        Add-Group ('Map scheduling ' + $arm) ([int]$report.executed) ([int]$report.executed-[int]$report.failed) ([int]$report.failed) 0 (Join-Path $directory 'summary.json')
    }

    $extraSamples = 0
    foreach ($extra in @(
        @{Name='JST776';Manifest=$Jst776ReplayManifest;Expected=6145},
        @{Name='JST771';Manifest=$Jst771ReplayManifest;Expected=3121})) {
        $directory = Join-Path $run ('replay-' + $extra.Name.ToLowerInvariant())
        Invoke-App $app @('--replay-journals',(Resolve-Path $extra.Manifest).Path,$directory) ('replay-' + $extra.Name.ToLowerInvariant()) 180
        $result = @(Read-Report (Join-Path $directory 'summary.json'))
        if ($result.Count -ne 1 -or $result[0].passed -ne $true -or [int]$result[0].samples -ne [int]$extra.Expected) { throw "$($extra.Name) replay mismatch." }
        $extraSamples += [int]$result[0].samples
        Add-Group ('recorded flight: ' + $extra.Name) 1 1 0 0 (Join-Path $directory 'summary.json')
    }
    $historicalSamples = [int]$coverage.processedSamples + $extraSamples
    if ($historicalSamples -ne 100632) { throw "Historical replay total is $historicalSamples, expected 100,632." }

    $jst771Evidence = Join-Path $run 'jst771-evidence.json'
    $supplementalJournals = @($registry.jst771Evidence.journals | ForEach-Object { Join-Path $repo $_ })
    $supplementalHistory = Join-Path $repo $registry.jst771Evidence.historyDatabase
    $code = Invoke-Console $PythonPath @('scripts/test-jst771-evidence.py','--supplemental-journal',$supplementalJournals[0],'--supplemental-journal',$supplementalJournals[1],'--history-db',$supplementalHistory,'--output',$jst771Evidence) (Join-Path $run 'jst771-evidence.log')
    $jstEvidence = Read-Report $jst771Evidence
    if ($code -ne 0 -or $jstEvidence.result -ne 'PASS') { throw 'JST771 historical evidence assertions failed.' }
    Add-Group 'JST771 historical evidence assertions' ([int]$jstEvidence.checkCounts.total) ([int]$jstEvidence.checkCounts.passed) ([int]$jstEvidence.checkCounts.failed) 0 $jst771Evidence

    foreach ($before in $inputHashes) {
        if ((Get-FileHash -LiteralPath $before.Path -Algorithm SHA256).Hash -ne $before.Hash) { throw "Replay input changed during verification: $($before.Path)" }
    }
    $inputHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'replay-input-hashes-after.json') -Encoding utf8
    $sourceAfter = @(foreach ($item in $sourceFiles) {
        $path = Join-Path $repo $item.path
        [pscustomobject]@{path=$item.path;exists=(Test-Path -LiteralPath $path -PathType Leaf);sha256=$(if(Test-Path -LiteralPath $path -PathType Leaf){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}else{$null})}
    })
    $changedDuringRun = @($sourceAfter | Where-Object { -not $_.exists -or $_.sha256 -ne ($sourceFiles | Where-Object path -eq $_.path).sha256 })
    if ($changedDuringRun.Count) { throw 'Source files changed while verification was running.' }
    $sourceAfter | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'source-manifest-after.json') -Encoding utf8

    $totals = [pscustomobject]@{
        executed = [int](($groups | Measure-Object executed -Sum).Sum)
        passed = [int](($groups | Measure-Object passed -Sum).Sum)
        failed = [int](($groups | Measure-Object failed -Sum).Sum)
        skipped = [int](($groups | Measure-Object skipped -Sum).Sum)
    }
    $summary = [pscustomobject]@{
        checkpoint='7.25';completed=$true;builds=$builds;counts=$totals;groups=$groups
        replayCount=11;telemetrySamples=$historicalSamples;baselineTelemetrySamples=[int]$coverage.processedSamples
        nativeSamplesExcluded=6197;sourceChangedDuringRun=$false;packageCreated=$false;evidenceDirectory=$run
        countConvention='Synthetic assertion executions and flight validations are counted separately from recorded telemetry samples. Two migration native-only skips are retained.'
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8
    $summary | ConvertTo-Json -Depth 8
    if ($totals.failed -gt 0) { exit 1 }
} catch {
    [pscustomobject]@{checkpoint='7.25';completed=$false;error=$_.ToString();builds=$builds;completedGroups=$groups;evidenceDirectory=$run;packageCreated=$false} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'incomplete.json') -Encoding utf8
    Write-Error "Checkpoint 7.25 verification incomplete. $($_.Exception.Message) Evidence: $run"
    exit 2
} finally {
    Pop-Location
    $env:DOTNET_ROOT = $oldRoot
    $env:DOTNET_CLI_HOME = $oldCliHome
}
