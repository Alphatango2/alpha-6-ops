# Pure acceptance evaluator: no application mutation, hidden exclusions or fallback data.
function Get-WindowDistribution($Values) {
    $v=@($Values|Sort-Object)
    if(!$v.Count){return @{count=0}}
    @{count=$v.Count;min=$v[0];median=if($v.Count%2){$v[[int][Math]::Floor($v.Count/2)]}else{($v[$v.Count/2-1]+$v[$v.Count/2])/2};p95=$v[[Math]::Ceiling($v.Count*.95)-1];max=$v[-1];values=$v}
}
function Test-Finite($Value) { $n=0.0; return $null -ne $Value -and [double]::TryParse([string]$Value,[ref]$n) -and ![double]::IsNaN($n) -and ![double]::IsInfinity($n) -and $n -ge 0 }
function Test-Metric($Metric,[int]$Minimum=1) {
    if($null -eq $Metric -or !(Test-Finite $Metric.count) -or $Metric.count -lt $Minimum){return $false}
    foreach($field in @('p50','p95','p99','max')){if(!(Test-Finite $Metric.$field)){return $false}}
    return $Metric.p50 -le $Metric.p95 -and $Metric.p95 -le $Metric.p99 -and $Metric.p99 -le $Metric.max
}
function Test-Raw($Raw,$Metric) {
    if(@($Raw).Count -ne $Metric.count){return $false}
    $v=@($Raw|ForEach-Object {$_.ms}|Sort-Object)
    if(@($v|Where-Object {!(Test-Finite $_)}).Count){return $false}
    foreach($pair in @(@('p50',.5),@('p95',.95),@('p99',.99),@('max',1))){
        if([Math]::Abs($Metric.($pair[0])-$v[[Math]::Floor(($v.Count-1)*$pair[1])]) -gt .000001){return $false}
    }
    return $true
}
function Measure-MapResponsiveness {
    param($Policy,$Trials)
    $errors=[Collections.Generic.List[string]]::new();$rows=@();$modes=@{}
    foreach($key in @('minimumTrialsPerMode','minimumWindowsPerTrial','windowSecondsMin','windowSecondsMax','minimumHeartbeatSamples','minimumQueueSamples','minimumWorkSamples','medianP95Ms','windowP95Percentile','windowP95BudgetMs','maxWindowP95Ms','maximumOver100Fraction','maximumConsecutiveOver100','p99Ms','maximumHeartbeatMs','queueP95Ms','queueMaxMs','ackP95Ms','startupMs','minimumInputHz','maximumProbeSkipped','expectedWidthDip','expectedHeightDip')){
        if(!(Test-Finite $Policy.$key)){$errors.Add("Invalid or missing policy field $key")}
    }
    if($errors.Count){return @{ready=$false;failures=@($errors);windows=0}}
    if($Policy.windowP95Percentile -le 0 -or $Policy.windowP95Percentile -gt 1 -or $Policy.maximumOver100Fraction -gt 1){return @{ready=$false;failures=@('Invalid policy fractions');windows=0}}
    foreach($trial in $Trials){
        $d=$trial.data;$name=$trial.name
        if($null -eq $d){$errors.Add("$name missing trial data");continue}
        $mode=if($d.light -eq $true){'light'}else{'full'};$modes[$mode]=1+$modes[$mode]
        if($trial.summary.passed -ne $true -or $trial.summary.failed -ne 0){$errors.Add("$name functional checks failed or missing")}
        if($trial.guard.clean -ne $true -or $trial.guard.reportCount -ne 0){$errors.Add("$name unexpected-report guard failed or missing")}
        if($null -eq $trial.disposal -or $null -eq $trial.disposal.remainingBrowserPids -or @($trial.disposal.remainingBrowserPids).Count -ne 0 -or !(Test-Finite $trial.disposal.disposalMs) -or $trial.disposal.disposalMs -gt 10000){$errors.Add("$name browser disposal failed or missing")}
        if($d.expected -le 0 -or $d.processed -ne $d.expected -or $d.sourceStarts -ne 1 -or $d.visibleThroughout -ne $true -or !(Test-Finite $d.targetHz) -or $d.targetHz -lt $Policy.minimumInputHz){$errors.Add("$name input/visibility/source validity failed")}
        $records=@($d.records)
        if($records.Count -lt $Policy.minimumWindowsPerTrial -or $records.Count -ne $d.requestedMinutes*2){$errors.Add("$name insufficient or incomplete windows")}
        $run=0;$lastEpoch=$null;$expectedIndex=0
        foreach($r in $records){
            $label="$name window $($r.index)";$h=$r.timing.heartbeatMs;$q=$r.dispatcherProbeMs
            if($r.index -ne $expectedIndex){$errors.Add("$label duplicate/missing/out-of-order window")};$expectedIndex++
            if($r.view.width -ne $Policy.expectedWidthDip -or $r.view.height -ne $Policy.expectedHeightDip -or $r.view.compositionHost -ne $true){$errors.Add("$label viewport/production host changed or missing")}
            if(!(Test-Finite $r.seconds) -or $r.seconds -lt $Policy.windowSecondsMin -or $r.seconds -gt $Policy.windowSecondsMax -or $r.visibleThroughout -ne $true){$errors.Add("$label duration/visibility invalid")}
            if($null -eq $r.epoch -or ($null -ne $lastEpoch -and $lastEpoch -ne $r.epoch)){$errors.Add("$label renderer epoch changed")};$lastEpoch=$r.epoch
            if(!(Test-Metric $h $Policy.minimumHeartbeatSamples) -or !(Test-Metric $q $Policy.minimumQueueSamples)){$errors.Add("$label missing/invalid/insufficient heartbeat or queue data");continue}
            if(!(Test-Raw $r.heartbeatSamples $h) -or !(Test-Raw $r.queueSamples $q)){$errors.Add("$label raw timing/summary mismatch")}
            if(!(Test-Finite $r.boundary.heartbeatPendingMs) -or !(Test-Finite $r.boundary.queuePendingMs) -or $r.boundary.heartbeatPendingMs -ge $Policy.maximumHeartbeatMs -or $r.boundary.queuePendingMs -ge $Policy.queueMaxMs){$errors.Add("$label unfinished boundary callback missing or stalled")}
            if(!(Test-Metric $r.timing.presentationMs $Policy.minimumWorkSamples) -or !(Test-Metric $r.timing.recoverySaveMs $Policy.minimumWorkSamples) -or !(Test-Metric $r.resources.samplingMs) -or $r.resources.samplingErrors -ne 0){$errors.Add("$label workload/sampling evidence invalid")}
            if(!(Test-Metric $r.bridge.acknowledgmentMs) -or $r.bridge.acknowledgmentMs.p95 -ge $Policy.ackP95Ms -or !(Test-Finite $r.bridge.readyMs) -or $r.bridge.readyMs -ge $Policy.startupMs){$errors.Add("$label bridge budget/validity failed")}
            if($r.mapDelta.produced -le 0 -or $r.mapDelta.acknowledged -le 0){$errors.Add("$label map did not update")}
            if($null -eq $r.probeSkipped -or $r.probeSkipped -gt $Policy.maximumProbeSkipped){$errors.Add("$label bounded queue probe repeatedly stalled")}
            if($h.p95 -gt 100){$run++}else{$run=0}
            if($run -gt $Policy.maximumConsecutiveOver100){$errors.Add("$label consecutive p95 exceedances")}
            if($h.p95 -gt $Policy.maxWindowP95Ms -or $h.p99 -ge $Policy.p99Ms -or $h.max -ge $Policy.maximumHeartbeatMs){$errors.Add("$label tail/stall budget failed")}
            if($q.p95 -ge $Policy.queueP95Ms -or $q.max -ge $Policy.queueMaxMs){$errors.Add("$label Input queue budget failed")}
            $rows+=@{trial=$name;mode=$mode;index=$r.index;p50=$h.p50;p95=$h.p95;p99=$h.p99;max=$h.max;count=$h.count;queueP95=$q.p95;queueMax=$q.max;presentationP95=$r.timing.presentationMs.p95;saveP95=$r.timing.recoverySaveMs.p95;resourceP95=$r.resources.samplingMs.p95}
        }
    }
    foreach($mode in @('full','light')){
        if($modes[$mode] -lt $Policy.minimumTrialsPerMode){$errors.Add("Insufficient fresh $mode trials")}
        $dist=Get-WindowDistribution @($rows|Where-Object mode -eq $mode|ForEach-Object p95)
        if(!$dist.count -or $dist.median -ge $Policy.medianP95Ms){$errors.Add("$mode median-window p95 budget failed")}
    }
    $p95=@($rows|ForEach-Object p95|Sort-Object);$distribution=Get-WindowDistribution $p95
    $over=@($p95|Where-Object {$_ -gt 100})
    if(!$p95.Count){$errors.Add('No valid windows')}
    else{
        if($p95[[Math]::Ceiling($p95.Count*$Policy.windowP95Percentile)-1] -ge $Policy.windowP95BudgetMs){$errors.Add('Percentile-of-windows budget failed')}
        if($over.Count/$p95.Count -gt $Policy.maximumOver100Fraction){$errors.Add('Too many windows above 100 ms')}
    }
    @{ready=$errors.Count -eq 0;failures=@($errors);trials=@($Trials).Count;windows=$rows.Count;windowP95=$distribution;over100Count=$over.Count;over100Percent=if($p95.Count){100*$over.Count/$p95.Count}else{0};exceedancesMs=@($over|ForEach-Object {$_-100});rows=$rows}
}
Export-ModuleMember -Function Measure-MapResponsiveness,Get-WindowDistribution
