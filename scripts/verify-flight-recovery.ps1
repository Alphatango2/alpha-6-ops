param(
    [string]$DotNetRoot=(Join-Path $PSScriptRoot '../work/dotnet'),
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$OutputDirectory) { $OutputDirectory=Join-Path $repo ('work/checkpoint-7.23/recovery-'+(Get-Date -Format 'yyyyMMdd-HHmmss')) }
$run=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $run) { throw 'Choose a new isolated recovery evidence directory.' }
New-Item -ItemType Directory -Path $run | Out-Null
$oldRoot=$env:DOTNET_ROOT; $oldCliHome=$env:DOTNET_CLI_HOME
$env:DOTNET_ROOT=[IO.Path]::GetFullPath($DotNetRoot); $env:DOTNET_CLI_HOME=Join-Path $repo 'work/dotnet-home'
Push-Location $repo
try {
    & (Join-Path $env:DOTNET_ROOT 'dotnet.exe') run --project tests/Alpha6Ops.RecoveryChecks -c Release -- --checks $run *> (Join-Path $run 'checks.log')
    if ($LASTEXITCODE -ne 0) { throw "Recovery checks failed; evidence: $run" }
    $report=Get-Content -LiteralPath (Join-Path $run 'summary.json') -Raw | ConvertFrom-Json
    if ($report.failed -ne 0 -or $report.executed -lt 21 -or $report.metrics.sequential.replacements -lt 5000) { throw 'Recovery coverage incomplete.' }
    # Only these explicitly injected negative cases may emit reports. This never
    # applies to classic/Map V2 runtime verification or their unexpected-report guard.
    $expected=@{'incomplete'=@('flight_recovery_load',1);'corrupt'=@('flight_recovery_load',6);'invalid-monitor'=@('flight_recovery_load',2);'stage-lock'=@('flight_recovery_cleanup',1);'intentional-failure/CrashReports'=@('flight_recovery_save',2)}
    $seen=@{}
    foreach ($file in Get-ChildItem -LiteralPath $run -Recurse -Filter 'Alpha6OPS-crash-*.json') {
        $relative=[IO.Path]::GetRelativePath($run,$file.DirectoryName).Replace('\','/')
        $data=Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if (!$expected.ContainsKey($relative) -or $data.source -ne $expected[$relative][0]) { throw "Unexpected diagnostic report: $($file.FullName)" }
        $seen[$relative]=1+$seen[$relative]
    }
    foreach ($key in $expected.Keys) { if ($seen[$key] -ne $expected[$key][1]) { throw "Missing/extra negative-case evidence: $key" } }
    [pscustomobject]@{executed=$report.executed;passed=$report.passed;failed=$report.failed;unexpectedReports=0;sequentialReplacements=$report.metrics.sequential.replacements;evidenceDirectory=$run} | ConvertTo-Json
} finally {Pop-Location; $env:DOTNET_ROOT=$oldRoot; $env:DOTNET_CLI_HOME=$oldCliHome}
