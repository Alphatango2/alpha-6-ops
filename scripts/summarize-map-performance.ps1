param([string]$EvidenceRoot=(Join-Path $PSScriptRoot '../work/checkpoint-7.24a'),[string]$SoakDirectory='soak-verified')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($EvidenceRoot)
function Read-Json([string]$relative){Get-Content -LiteralPath (Join-Path $root $relative) -Raw|ConvertFrom-Json}
function Distribution($values){
    $a=@($values|Sort-Object)
    if(!$a.Count){return $null}
    [pscustomobject]@{count=$a.Count;min=$a[0];median=$a[[int][math]::Floor(($a.Count-1)*.5)];p95=$a[[int][math]::Floor(($a.Count-1)*.95)];max=$a[-1]}
}
function Has-Metric($measurement,[string]$field){
    $v=$measurement.$field
    return $null -ne $measurement -and $measurement.count -gt 0 -and $null -ne $v -and [double]::IsFinite([double]$v) -and [double]$v -ge 0
}
function Has-Startup($bridge){return $null -ne $bridge.readyMs -and [double]::IsFinite([double]$bridge.readyMs) -and $bridge.readyMs -gt 0}
$soak=Read-Json "$SoakDirectory/soak.json"
$summary=Read-Json "$SoakDirectory/summary.json"
$guard=Read-Json "$SoakDirectory/unexpected-report-guard.json"
$finalPath=Join-Path $root "$SoakDirectory/final-resources.json"
$finalResources=if(Test-Path -LiteralPath $finalPath){Read-Json "$SoakDirectory/final-resources.json"}else{$null}
$resources=@(Read-Json "$SoakDirectory/resource-samples.json")
$steady=@($soak.records|Where-Object {$_.interval -eq 'steady' -and !$_.hidden})
$renderers=@($soak.records|Where-Object {$null -ne $_.bridge})
$gates=@(
    [pscustomobject]@{name='Sustained run at least 60 minutes';passed=$soak.seconds -ge 3600;actual=$soak.seconds;limit=3600;unit='seconds minimum'}
    [pscustomobject]@{name='All independently produced samples retained';passed=$soak.count -eq $soak.expected;actual=$soak.count;limit=$soak.expected;unit='samples'}
    [pscustomobject]@{name='Every steady visible interval Input heartbeat p95 below 100ms';passed=$steady.Count -gt 0 -and @($steady|Where-Object {!(Has-Metric $_.timing.heartbeatMs 'p95') -or $_.timing.heartbeatMs.p95 -ge 100}).Count -eq 0;actual=($steady.timing.heartbeatMs.p95|Measure-Object -Maximum).Maximum;limit=100;unit='ms maximum interval p95'}
    [pscustomobject]@{name='Every steady visible interval Input heartbeat p99 below 250ms';passed=$steady.Count -gt 0 -and @($steady|Where-Object {!(Has-Metric $_.timing.heartbeatMs 'p99') -or $_.timing.heartbeatMs.p99 -ge 250}).Count -eq 0;actual=($steady.timing.heartbeatMs.p99|Measure-Object -Maximum).Maximum;limit=250;unit='ms maximum interval p99'}
    [pscustomobject]@{name='Steady visible bridge end-to-end ACK p95 below 500ms';passed=$steady.Count -gt 0 -and @($steady|Where-Object {!(Has-Metric $_.bridge.acknowledgmentMs 'p95') -or $_.bridge.acknowledgmentMs.p95 -ge 500}).Count -eq 0;actual=($steady.bridge.acknowledgmentMs.p95|Measure-Object -Maximum).Maximum;limit=500;unit='ms maximum reported renderer p95'}
    [pscustomobject]@{name='Renderer startup below five seconds';passed=$renderers.Count -gt 0 -and @($renderers|Where-Object {!(Has-Startup $_.bridge) -or $_.bridge.readyMs -ge 5000}).Count -eq 0;actual=($soak.records.bridge.readyMs|Measure-Object -Maximum).Maximum;limit=5000;unit='ms maximum'}
)
$feed=Read-Json "$SoakDirectory/soak-feed.json"
$emitted=@{};foreach($e in $feed.emissions){$emitted[$e.at.ToString()]=$e.emittedMs}
$delivery=@($feed.consumed|ForEach-Object {$_.processedMs-$emitted[$_.at.ToString()]})
$first=[DateTimeOffset]$resources[0].utc
$early=@($resources|Where-Object {$_.stage -eq 'visible' -and ([DateTimeOffset]$_.utc-$first).TotalMinutes -ge 5 -and ([DateTimeOffset]$_.utc-$first).TotalMinutes -lt 15})
$late=@($resources|Where-Object {$_.stage -eq 'visible' -and ([DateTimeOffset]$_.utc-$first).TotalMinutes -ge 45})
$result=[pscustomobject]@{
    sourceDirectory=$SoakDirectory
    completion=[pscustomobject]@{allSoakChecksPassed=$summary.passed;executed=$summary.executed;failed=$summary.failed;unexpectedReportGuardClean=$guard.clean;finalDisposalRecorded=$null -ne $finalResources;noOwnedProcessesRemain=$null -ne $finalResources -and $finalResources.remainingBrowserPids.Count -eq 0;disposalMs=$finalResources.disposalMs}
    resourceSampling=[pscustomobject]@{reportedErrors=($soak.records.resources.samplingErrors|Measure-Object -Maximum).Maximum;coverage='Cumulative diagnostic counter at each 30-second record; absent in older runs. Individual rows do not identify errors: totals from an affected run are incomplete.'}
    gates=$gates;gatesPassed=@($gates|Where-Object passed).Count;gatesFailed=@($gates|Where-Object {!$_.passed}).Count
    seconds=$soak.seconds;samples=$soak.count;targetInputHz=$feed.targetHz;deliveryMs=(Distribution $delivery)
    emissionLatenessMs=(Distribution @($feed.emissions|ForEach-Object {$_.emittedMs-$_.dueMs}))
    steadyWindowP95Ms=(Distribution $steady.timing.heartbeatMs.p95);steadyWindowP99Ms=(Distribution $steady.timing.heartbeatMs.p99)
    visibleMiB=(Distribution @($resources|Where-Object stage -eq 'visible'|ForEach-Object totalMiB))
    earlyVisibleMiB=(Distribution $early.totalMiB);lateVisibleMiB=(Distribution $late.totalMiB)
    hiddenBrowserCounts=(Distribution @($resources|Where-Object stage -eq 'hidden'|ForEach-Object browserProcesses))
    limitations='The six performance gates are not the full readiness decision: inspect completion, guards, resource validity and other suites separately. Window-percentile distributions are not pooled heartbeat percentiles. ACK includes Dispatcher queue time. Summed working sets may double-count shared pages. Observed resource trend is bounded evidence, not leak freedom.'
}
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $root 'readiness-metrics.json')
$result|ConvertTo-Json -Depth 8
