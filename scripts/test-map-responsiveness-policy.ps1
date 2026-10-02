param([string]$OutputDirectory='work/checkpoint-7.24a.1/policy-contracts')
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'MapResponsivenessPolicy.psm1') -Force
# Deliberately synthetic policy limits exercise evaluation, not readiness policy selection.
$policy=@{minimumTrialsPerMode=1;minimumWindowsPerTrial=10;windowSecondsMin=29;windowSecondsMax=32;minimumHeartbeatSamples=500;minimumQueueSamples=100;minimumWorkSamples=20;medianP95Ms=80;windowP95Percentile=.95;windowP95BudgetMs=100;maxWindowP95Ms=120;maximumOver100Fraction=.05;maximumConsecutiveOver100=1;p99Ms=250;maximumHeartbeatMs=500;queueP95Ms=100;queueMaxMs=500;ackP95Ms=500;startupMs=5000;minimumInputHz=1;maximumProbeSkipped=2}
$policy.expectedWidthDip=3440;$policy.expectedHeightDip=1400
function Metric($n,$value){@{count=$n;p50=$value;p95=$value;p99=$value;max=$value}}
function Fixture {
    @('full','light')|ForEach-Object {
        $light=$_ -eq 'light'
        $rows=@(0..9|ForEach-Object {
            @{index=$_;seconds=30;visibleThroughout=$true;epoch=1
              timing=@{heartbeatMs=(Metric 900 40);presentationMs=(Metric 33 25);recoverySaveMs=(Metric 33 4)}
              dispatcherProbeMs=(Metric 290 20);probeSkipped=0
              boundary=@{heartbeatPendingMs=10;queuePendingMs=0}
              view=@{width=3440;height=1400;compositionHost=$true}
              resources=@{samplingErrors=0;samplingMs=(Metric 1 22)}
              bridge=@{readyMs=800;acknowledgmentMs=(Metric 30 30)}
              mapDelta=@{produced=30;acknowledged=30}
              heartbeatSamples=@(1..900|ForEach-Object {@{ms=40}})
              queueSamples=@(1..290|ForEach-Object {@{ms=20}})}
        })
        @{name=$_;summary=@{passed=$true;failed=0};guard=@{clean=$true;reportCount=0}
          disposal=@{remainingBrowserPids=@();disposalMs=100}
          data=@{schema=1;light=$light;requestedMinutes=5;targetHz=1.1;expected=347;processed=347;sourceStarts=1;visibleThroughout=$true;records=$rows}}
    }
}
function Set-Heartbeat($row,[double]$value){$row.timing.heartbeatMs=Metric 900 $value;$row.heartbeatSamples=@(1..900|ForEach-Object {@{ms=$value}})}
function Sync-Metric($raw){$v=@($raw.ms|Sort-Object);@{count=$v.Count;p50=$v[[Math]::Floor(($v.Count-1)*.5)];p95=$v[[Math]::Floor(($v.Count-1)*.95)];p99=$v[[Math]::Floor(($v.Count-1)*.99)];max=$v[-1]}}
$checks=@()
foreach($case in @('valid','isolated-tolerance','sustained','over-cap','p99','max-stall','queue-stall','missing-metric','nonfinite','too-few-windows','too-few-samples','hidden','input-loss','dirty-guard','leftover-browser','resource-errors','reconnect','missing-mode','missing-final-disposal','raw-mismatch','low-input-rate','duplicate-window','missing-policy-field','boundary-heartbeat-stall','boundary-queue-stall','missing-boundary','viewport-changed')){
    $trials=@(Fixture);$row=$trials[0].data.records[0]
    $casePolicy=$policy.Clone()
    switch($case){
        'isolated-tolerance' {Set-Heartbeat $row 110}
        'sustained' {Set-Heartbeat $row 110;Set-Heartbeat $trials[0].data.records[1] 110}
        'over-cap' {Set-Heartbeat $row 130}
        'p99' {foreach($i in 890..899){$row.heartbeatSamples[$i].ms=260};$row.timing.heartbeatMs=Sync-Metric $row.heartbeatSamples}
        'max-stall' {$row.heartbeatSamples[-1].ms=600;$row.timing.heartbeatMs=Sync-Metric $row.heartbeatSamples}
        'queue-stall' {$row.queueSamples[-1].ms=600;$row.dispatcherProbeMs=Sync-Metric $row.queueSamples}
        'missing-metric' {$row.timing.heartbeatMs.Remove('p95')}
        'nonfinite' {$row.timing.heartbeatMs.p95='NaN'}
        'too-few-windows' {$trials[0].data.records=@($row)}
        'too-few-samples' {$row.timing.heartbeatMs.count=20}
        'hidden' {$row.visibleThroughout=$false}
        'input-loss' {$trials[0].data.processed=346}
        'dirty-guard' {$trials[0].guard.clean=$false;$trials[0].guard.reportCount=1}
        'leftover-browser' {$trials[0].disposal.remainingBrowserPids=@(123)}
        'resource-errors' {$row.resources.samplingErrors=1}
        'reconnect' {$trials[0].data.sourceStarts=2}
        'missing-mode' {$trials=@($trials[0])}
        'missing-final-disposal' {$trials[0].disposal=$null}
        'raw-mismatch' {$row.heartbeatSamples[0].ms=900}
        'low-input-rate' {$trials[0].data.targetHz=.5}
        'duplicate-window' {$trials[0].data.records[1].index=0}
        'missing-policy-field' {$casePolicy.Remove('windowP95BudgetMs')}
        'boundary-heartbeat-stall' {$row.boundary.heartbeatPendingMs=600}
        'boundary-queue-stall' {$row.boundary.queuePendingMs=600}
        'missing-boundary' {$row.Remove('boundary')}
        'viewport-changed' {$row.view.width=1920}
    }
    $result=Measure-MapResponsiveness -Policy $casePolicy -Trials $trials
    $expected=$case -in @('valid','isolated-tolerance')
    $checks+=@{name=$case;passed=$result.ready -eq $expected;expectedReady=$expected;actualReady=$result.ready;failures=$result.failures}
}
New-Item -ItemType Directory -Force $OutputDirectory|Out-Null
$summary=@{executed=$checks.Count;passed=@($checks|Where-Object passed).Count;failed=@($checks|Where-Object {!$_.passed}).Count;checks=$checks}
$summary|ConvertTo-Json -Depth 8|Set-Content (Join-Path $OutputDirectory summary.json)
if($summary.failed){throw "$($summary.failed) responsiveness policy contract failures"}
$summary|Select-Object executed,passed,failed|ConvertTo-Json
