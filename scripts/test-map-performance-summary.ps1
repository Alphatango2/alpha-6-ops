param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot ('../work/checkpoint-7.24a/summary-contracts-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$root=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $root){throw 'Use a fresh summary-contract directory.'}
New-Item -ItemType Directory -Path (Join-Path $root 'fixture') | Out-Null
function Write-Json($name,$value){$value|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $root ('fixture/'+$name))}
Write-Json 'summary.json' @{passed=$true;executed=1;failed=0}
Write-Json 'unexpected-report-guard.json' @{clean=$true}
Write-Json 'final-resources.json' @{remainingBrowserPids=@();disposalMs=100}
Write-Json 'resource-samples.json' @(@{utc='2026-09-24T00:00:00Z';stage='visible';browserProcesses=5;totalMiB=750})
Write-Json 'soak-feed.json' @{targetHz=1;emissions=@(@{at='2026-09-24T00:00:00Z';dueMs=0;emittedMs=1});consumed=@(@{at='2026-09-24T00:00:00Z';processedMs=20})}
$cases=@('valid','empty-counts','missing-values','nonfinite-values','no-windows')
$checks=@()
foreach($case in $cases){
    $heartbeat=@{count=20;p95=40;p99=60}
    $ack=@{count=20;p95=30}
    $bridge=@{readyMs=500;acknowledgmentMs=$ack}
    if($case -eq 'empty-counts'){$heartbeat.count=0;$ack.count=0}
    if($case -eq 'missing-values'){$heartbeat.Remove('p95');$heartbeat.Remove('p99');$ack.Remove('p95');$bridge.Remove('readyMs')}
    if($case -eq 'nonfinite-values'){$heartbeat.p95='NaN';$heartbeat.p99='Infinity';$ack.p95='NaN';$bridge.readyMs='Infinity'}
    $records=@(@{interval='steady';hidden=$false;timing=@{heartbeatMs=$heartbeat};bridge=$bridge;resources=@{samplingErrors=0}})
    if($case -eq 'no-windows'){$records=@()}
    Write-Json 'soak.json' @{seconds=3601;count=1;expected=1;records=$records}
    & (Join-Path $PSScriptRoot 'summarize-map-performance.ps1') -EvidenceRoot $root -SoakDirectory fixture | Out-Null
    $r=Get-Content -LiteralPath (Join-Path $root 'readiness-metrics.json') -Raw|ConvertFrom-Json
    $expected=if($case -eq 'valid'){0}elseif($case -eq 'empty-counts'){3}else{4}
    $checks+=@{name=$case;passed=$r.gatesFailed -eq $expected;expectedFailedPerformanceGates=$expected;actualFailedPerformanceGates=$r.gatesFailed}
}
$failed=@($checks|Where-Object {!$_.passed}).Count
@{passed=$failed -eq 0;executed=$checks.Count;failed=$failed;checks=$checks}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $root 'summary.json')
if($failed){throw "$failed summary contract checks failed: $root"}
Get-Content -LiteralPath (Join-Path $root 'summary.json') -Raw
