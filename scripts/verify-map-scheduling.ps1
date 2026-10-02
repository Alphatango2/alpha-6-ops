param([Parameter(Mandatory)][string]$Arm,[double]$Seconds=30,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $run){throw 'Choose a fresh scheduling evidence directory'}
New-Item -ItemType Directory -Path $run|Out-Null
$app=Join-Path $repo 'src/Alpha6Ops.Desktop/bin/Release/net10.0-windows10.0.17763.0/Sierra4.exe'
$oldRoot=$env:DOTNET_ROOT
try{
 $env:DOTNET_ROOT=Join-Path $repo 'work/dotnet'
 Get-FileHash -LiteralPath $app|ConvertTo-Json|Set-Content (Join-Path $run application.json)
 Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($app,'dll'))|ConvertTo-Json|Set-Content (Join-Path $run assembly.json)
 $p=Start-Process -FilePath $app -ArgumentList @('--map-performance-checks',('"'+$run+'"'),('scheduling-'+$Arm),($Seconds/60).ToString([Globalization.CultureInfo]::InvariantCulture)) -WorkingDirectory $repo -WindowStyle Hidden -PassThru
 $p.Id|Set-Content (Join-Path $run process-id.txt)
 # Legacy mode runs the original fixed performance sequence, not the short arm duration.
 $timeoutSeconds=if($Arm -eq 'legacy'){1200}else{$Seconds+120}
 @{arm=$Arm;requestedSeconds=$Seconds;timeoutSeconds=$timeoutSeconds}|ConvertTo-Json|Set-Content (Join-Path $run runner-settings.json)
 if(!$p.WaitForExit([int]($timeoutSeconds*1000))){Stop-Process -Id $p.Id;throw 'Scheduling diagnostic exceeded deadline'}
 $p.WaitForExit()
 $reports=@(Get-ChildItem -LiteralPath $run -Recurse -File -Filter 'Alpha6OPS-crash-*')
 $summary=Get-Content (Join-Path $run summary.json) -Raw|ConvertFrom-Json
 @{clean=$reports.Count -eq 0;reportCount=$reports.Count;exitCode=$p.ExitCode}|ConvertTo-Json|Set-Content (Join-Path $run unexpected-report-guard.json)
 if($p.ExitCode -ne 0 -or !$summary.passed -or $reports.Count){throw "Scheduling diagnostics failed: $run"}
 @{arm=$Arm;checks=$summary.executed;passed=$true;directory=$run}|ConvertTo-Json
}finally{$env:DOTNET_ROOT=$oldRoot}
