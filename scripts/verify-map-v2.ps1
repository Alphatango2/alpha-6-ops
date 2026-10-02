param(
    [string]$DotNetRoot=(Join-Path $PSScriptRoot '../work/dotnet'),
    [string]$NodePath='node',
    [string]$ApplicationPath,
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$OutputDirectory) { $OutputDirectory=Join-Path $repo ('work/checkpoint-7.22/verification-'+(Get-Date -Format 'yyyyMMdd-HHmmss')) }
$run=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $run -Force | Out-Null
$oldRoot=$env:DOTNET_ROOT; $oldCliHome=$env:DOTNET_CLI_HOME
$env:DOTNET_ROOT=[IO.Path]::GetFullPath($DotNetRoot);$env:DOTNET_CLI_HOME=Join-Path $repo 'work/dotnet-home'
$dotnet=Join-Path $env:DOTNET_ROOT 'dotnet.exe'
Push-Location $repo
try {
    & scripts/prepare-map-v2.ps1 -VerifyOnly | Out-File (Join-Path $run 'asset-verification.log')
    & $dotnet build src/Alpha6Ops.Desktop -c Release -nodeReuse:false -p:RestoreLockedMode=true *> (Join-Path $run 'build.log')
    if($LASTEXITCODE -ne 0){throw 'Desktop Map V2 build failed.'}
    & $dotnet run --project tests/Alpha6Ops.MapBridgeChecks -c Release *> (Join-Path $run 'mailbox.log')
    if($LASTEXITCODE -ne 0){throw 'Map mailbox checks failed.'}
    & $NodePath --test tests/map-v2/*.test.mjs *> (Join-Path $run 'browser-math.log')
    if($LASTEXITCODE -ne 0){throw 'Map browser/math checks failed.'}
    if(!$ApplicationPath){$ApplicationPath=Join-Path $repo 'src/Alpha6Ops.Desktop/bin/Release/net10.0-windows10.0.17763.0/Sierra4.exe'}
    $child=Start-Process -FilePath $ApplicationPath -ArgumentList @('--map-v2-checks',('"'+$run+'"')) -WorkingDirectory $repo -WindowStyle Hidden -PassThru
    if(!$child.WaitForExit(240000)){Stop-Process -Id $child.Id;throw "Map diagnostics timed out; partial evidence: $run"}
    $child.WaitForExit()
    $report=Get-Content -LiteralPath (Join-Path $run 'summary.json') -Raw | ConvertFrom-Json
    if($child.ExitCode -ne 0 -or !$report.passed){throw "Map V2 checks failed; evidence: $run"}
    if(@(Get-ChildItem (Join-Path $run 'CrashReports') -File -ErrorAction SilentlyContinue).Count -gt 0){throw 'Unexpected crash/unobserved-task report in map diagnostics.'}
    [pscustomobject]@{passed=$report.passed;mapChecks=$report.executed;evidenceDirectory=$run} | ConvertTo-Json
} finally {Pop-Location;$env:DOTNET_ROOT=$oldRoot;$env:DOTNET_CLI_HOME=$oldCliHome}
