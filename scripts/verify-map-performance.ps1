param(
    [ValidateSet('verify','soak','contracts','responsiveness-full','responsiveness-light','responsiveness-passive','responsiveness-boundary')][string]$Mode='verify',
    [ValidateRange(0,90)][double]$Minutes=60,
    [string]$ApplicationPath,
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!$ApplicationPath){$ApplicationPath=Join-Path $repo 'src/Alpha6Ops.Desktop/bin/Release/net10.0-windows10.0.17763.0/Sierra4.exe'}
if(!$OutputDirectory){$OutputDirectory=Join-Path $repo ('work/checkpoint-7.24a/'+$Mode+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$run=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $run){throw "Use a fresh evidence directory: $run"}
New-Item -ItemType Directory -Path $run | Out-Null
$oldRoot=$env:DOTNET_ROOT
try {
    $env:DOTNET_ROOT=Join-Path $repo 'work/dotnet'
    $app=(Resolve-Path -LiteralPath $ApplicationPath).Path
    Get-FileHash -LiteralPath $app | ConvertTo-Json | Set-Content (Join-Path $run 'application.json')
    $p=Start-Process -FilePath $app -ArgumentList @('--map-performance-checks',('"'+$run+'"'),$Mode,$Minutes.ToString([Globalization.CultureInfo]::InvariantCulture)) -WorkingDirectory $repo -WindowStyle Hidden -PassThru
    $p.Id | Set-Content (Join-Path $run 'process-id.txt')
    $deadline=(Get-Date).AddMinutes($(if($Mode -eq 'soak' -or $Mode.StartsWith('responsiveness-')){$Minutes+6}else{18}))
    while(!$p.WaitForExit(1000)){
        if((Get-Date) -gt $deadline){Stop-Process -Id $p.Id;throw "Performance diagnostics exceeded deadline: $run"}
    }
    $p.WaitForExit()
    $reports=@(Get-ChildItem -LiteralPath $run -Recurse -File -Filter 'Alpha6OPS-crash-*')
    $summary=Get-Content -LiteralPath (Join-Path $run 'summary.json') -Raw | ConvertFrom-Json
    $guard=[pscustomobject]@{clean=$reports.Count -eq 0;reportCount=$reports.Count;paths=@($reports.FullName)}
    $guard | ConvertTo-Json | Set-Content (Join-Path $run 'unexpected-report-guard.json')
    if($p.ExitCode -ne 0 -or !$summary.passed -or !$guard.clean){throw "Performance diagnostics failed: $run"}
    [pscustomobject]@{passed=$true;checks=$summary.executed;mode=$Mode;evidence=$run} | ConvertTo-Json
} finally {$env:DOTNET_ROOT=$oldRoot}
