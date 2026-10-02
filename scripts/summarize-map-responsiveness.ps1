param(
    [Parameter(Mandatory)][string[]]$TrialDirectories,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$PolicyPath,
    [string]$FreezePath,
    [string]$LifecycleDirectory
)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'MapResponsivenessPolicy.psm1') -Force
function Read-Json($path){Get-Content -LiteralPath $path -Raw|ConvertFrom-Json}
$trials=@($TrialDirectories|ForEach-Object {
    $root=(Resolve-Path -LiteralPath $_).Path
    @{name=(Split-Path $root -Leaf);data=(Read-Json (Join-Path $root responsiveness.json));summary=(Read-Json (Join-Path $root summary.json));guard=(Read-Json (Join-Path $root unexpected-report-guard.json));disposal=(Read-Json (Join-Path $root final-resources.json))}
})
if($PolicyPath){
    if(!$FreezePath){throw 'Final acceptance requires a previously stored policy/evaluator freeze.'}
    $freeze=Read-Json $FreezePath
    foreach($file in $freeze.files){if((Get-FileHash -LiteralPath $file.path).Hash -ne $file.sha256){throw "Frozen file changed: $($file.path)"}}
    if((Get-FileHash -LiteralPath $PolicyPath).Hash -ne $freeze.policySha256){throw 'Policy does not match freeze.'}
    foreach($dir in $TrialDirectories){
        $stamp=(Get-Item -LiteralPath (Join-Path $dir application.json)).CreationTimeUtc
        if($stamp -le ([DateTimeOffset]$freeze.utc).UtcDateTime){throw "Final trial predates policy freeze: $dir"}
    }
    $policy=Read-Json $PolicyPath
    $result=Measure-MapResponsiveness -Policy $policy -Trials $trials
    if(!$LifecycleDirectory){throw 'Final acceptance also requires the bounded full-flight lifecycle/recovery run.'}
    if((Get-Item -LiteralPath (Join-Path $LifecycleDirectory application.json)).CreationTimeUtc -le ([DateTimeOffset]$freeze.utc).UtcDateTime){throw 'Lifecycle run predates policy freeze.'}
    $soak=Read-Json (Join-Path $LifecycleDirectory soak.json)
    $soakSummary=Read-Json (Join-Path $LifecycleDirectory summary.json)
    $soakGuard=Read-Json (Join-Path $LifecycleDirectory unexpected-report-guard.json)
    $soakDisposal=Read-Json (Join-Path $LifecycleDirectory final-resources.json)
    $steady=@($soak.records|Where-Object {$_.interval -eq 'steady' -and !$_.hidden})
    $lifecycleFailures=@()
    if($soakSummary.passed -ne $true -or $soakSummary.failed -ne 0 -or $soakGuard.clean -ne $true -or $soakGuard.reportCount -ne 0 -or $null -eq $soakDisposal.remainingBrowserPids -or @($soakDisposal.remainingBrowserPids).Count -ne 0){$lifecycleFailures+='Lifecycle functional/guard/disposal failure'}
    if($soak.count -ne $soak.expected -or $soak.count -lt 395 -or $soak.seconds -lt 360 -or $soak.sourceStarts -ne 1 -or $soak.hiddenRendererObservations -ne 0 -or $soak.lockedSaves -lt 1 -or $null -ne $soak.lastFailure){$lifecycleFailures+='Lifecycle input/duration/recovery/map continuity failure'}
    if(!@($soakSummary.checks|Where-Object {$_.name -like 'Soak minimize disposes map*' -and $_.passed}).Count){$lifecycleFailures+='Lifecycle run did not exercise minimize/restore'}
    if($steady.Count -lt 4){$lifecycleFailures+='Insufficient supplemental steady lifecycle windows'}
    foreach($w in $steady){
        foreach($field in @('count','p95','p99','max')){
            $value=$w.timing.heartbeatMs.$field;$n=0.0
            if($null -eq $value -or ![double]::TryParse([string]$value,[ref]$n) -or [double]::IsNaN($n) -or [double]::IsInfinity($n)){$lifecycleFailures+="Invalid lifecycle heartbeat $field"}
        }
        $pending=$w.timing.pendingHeartbeatMs;$n=0.0
        if($null -eq $pending -or ![double]::TryParse([string]$pending,[ref]$n) -or [double]::IsNaN($n) -or [double]::IsInfinity($n) -or $n -lt 0 -or $n -ge $policy.maximumHeartbeatMs){$lifecycleFailures+='Lifecycle boundary heartbeat missing or stalled'}
        if($w.timing.heartbeatMs.count -lt $policy.minimumHeartbeatSamples -or $w.timing.heartbeatMs.p95 -gt $policy.maxWindowP95Ms -or $w.timing.heartbeatMs.p99 -ge $policy.p99Ms -or $w.timing.heartbeatMs.max -ge $policy.maximumHeartbeatMs -or $w.resources.samplingErrors -ne 0){$lifecycleFailures+='Lifecycle steady tail/sampling safeguard failed'}
    }
    $result.lifecycle=@{functionalChecks=$soakSummary.executed;samples=$soak.count;seconds=$soak.seconds;steadyWindows=$steady.Count;steadyP95=@($steady.timing.heartbeatMs.p95);steadyP99=@($steady.timing.heartbeatMs.p99);failures=$lifecycleFailures}
    $result.failures=@($result.failures)+$lifecycleFailures;$result.ready=$result.failures.Count -eq 0
    $result.policySha256=$freeze.policySha256;$result.freezeUtc=$freeze.utc;$result.kind='final-policy-assessment'
}else{
    $rows=@(foreach($trial in $trials){foreach($r in $trial.data.records){
        [pscustomobject]@{trial=$trial.name;mode=if($trial.data.light){'light'}else{'full'};index=$r.index;seconds=$r.seconds;
            count=$r.timing.heartbeatMs.count;p50=$r.timing.heartbeatMs.p50;p95=$r.timing.heartbeatMs.p95;p99=$r.timing.heartbeatMs.p99;max=$r.timing.heartbeatMs.max;
            queueP95=$r.dispatcherProbeMs.p95;queueP99=$r.dispatcherProbeMs.p99;queueMax=$r.dispatcherProbeMs.max;probeSkipped=$r.probeSkipped;
            presentationP95=$r.timing.presentationMs.p95;saveP95=$r.timing.recoverySaveMs.p95;resourceP95=$r.resources.samplingMs.p95;resourceCount=$r.resources.samplingMs.count;
            resourceTotalMs=$r.resources.samplingMs.mean*$r.resources.samplingMs.count;layoutEvents=$r.timing.layoutEvents;mapUpdates=$r.mapDelta.produced;mapAcknowledgments=$r.mapDelta.acknowledged}
    }})
    $byMode=@(foreach($mode in @('full','light')){
        $group=@($rows|Where-Object mode -eq $mode);$over=@($group|Where-Object {$_.p95 -gt 100})
        @{mode=$mode;windows=$group.Count;windowP95=(Get-WindowDistribution $group.p95);over100Count=$over.Count;over100Percent=100*$over.Count/$group.Count;
          exceedancesMs=@($over|ForEach-Object {$_.p95-100});queueP95=(Get-WindowDistribution $group.queueP95);presentationP95=(Get-WindowDistribution $group.presentationP95);
          resourceP95=(Get-WindowDistribution $group.resourceP95);resourceTotalMs=($group.resourceTotalMs|Measure-Object -Sum).Sum}
    })
    $result=@{kind='characterization-not-acceptance';trials=$trials.Count;windows=$rows.Count;byMode=$byMode;rows=$rows;allFunctionalChecksPassed=@($trials|Where-Object {$_.summary.passed -ne $true}).Count -eq 0}
}
$result|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $OutputPath
$result|Select-Object kind,ready,trials,windows,over100Count,failures|ConvertTo-Json -Depth 4
if($PolicyPath -and !$result.ready){exit 1}
