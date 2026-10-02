$ErrorActionPreference='Stop'
$repo=(Get-Location).Path
$root=Join-Path $repo 'work/checkpoint-7.24c/heartbeat-review-20260929'
$env:DOTNET_ROOT=Join-Path $repo 'work/dotnet'
$env:S4_HEARTBEAT_CONTEXTS='1'
$exe=Join-Path $repo 'src/Alpha6Ops.Desktop/bin/Release/net10.0-windows10.0.17763.0/Alpha6OPS.exe'
$traceOrder=@(0,1,1,0,0,1,1,0,0,1,1,0)
$rows=@()
for($index=0;$index -lt $traceOrder.Count;$index++){
 $dir=Join-Path $root ('run-{0:D2}' -f $index)
 if(Test-Path $dir){throw "Existing evidence: $dir"}
 $env:S4_CONTENTION_TRACE=[string]$traceOrder[$index]
 $env:S4_HEARTBEAT_NATIVE_TRACE=[string]$traceOrder[$index]
 $env:S4_HEARTBEAT_CONTEXT_ORDER=if($index%2){'normal-first'}else{'idle-first'}
 $started=[DateTimeOffset]::UtcNow
 $p=Start-Process -FilePath $exe -ArgumentList @('--map-contention-checks',('"'+$dir+'"')) -WindowStyle Hidden -PassThru -WorkingDirectory $repo
 @{index=$index;trace=$traceOrder[$index];startedUtc=$started;pid=$p.Id;directory=$dir}|ConvertTo-Json|Set-Content "$root/current-run.json"
 if(!$p.WaitForExit(240000)){
  @{index=$index;pid=$p.Id;timedOut=$true}|ConvertTo-Json|Set-Content "$root/series-stopped.json"
  throw "Unchanged 240 second deadline exceeded; partial evidence retained, no rerun. PID $($p.Id)"
 }
 $p.WaitForExit()
 $summary=Get-Content "$dir/summary.json" -Raw|ConvertFrom-Json
 $contention=Get-Content "$dir/contention.json" -Raw|ConvertFrom-Json
 $row=[pscustomobject]@{index=$index;trace=$traceOrder[$index];contextOrder=$env:S4_HEARTBEAT_CONTEXT_ORDER;startedUtc=$started;endedUtc=[DateTimeOffset]::UtcNow;exitCode=$p.ExitCode;executed=$summary.executed;failed=$summary.failed;passed=$summary.passed;baselineMs=$contention.baseline.heartbeatMaxMs;stressMs=$contention.stressed.heartbeatMaxMs;limitMs=[Math]::Max(150,$contention.baseline.heartbeatMaxMs+100)}
 $rows+=@($row)
 $rows|ConvertTo-Json -Depth 6|Set-Content "$root/series-results.json"
 $row|ConvertTo-Json -Compress|Write-Output
}
@{complete=$true;runs=$rows.Count;endedUtc=[DateTimeOffset]::UtcNow}|ConvertTo-Json|Set-Content "$root/series-complete.json"
