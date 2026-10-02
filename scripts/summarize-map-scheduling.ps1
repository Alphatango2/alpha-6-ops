param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'SchedulingTraceAnalysis.psm1') -Force
$results=@(foreach($file in Get-ChildItem -LiteralPath $Directory -Recurse -File -Filter 'scheduling-trace*.json'){
 $raw=Get-Content -LiteralPath $file.FullName -Raw|ConvertFrom-Json
 $trace=$raw.trace;$analysis=Measure-SchedulingTrace $trace
 $analysisPath=Join-Path $file.DirectoryName ($file.BaseName.Replace('scheduling-trace','scheduling-analysis')+'.json')
 $analysis|ConvertTo-Json -Depth 25|Set-Content -LiteralPath $analysisPath
 $windowPath=Join-Path $file.DirectoryName 'scheduling-window.json'
 $window=if(Test-Path -LiteralPath $windowPath){Get-Content -LiteralPath $windowPath -Raw|ConvertFrom-Json}else{$null}
 $integrityPath=Join-Path $file.DirectoryName 'scheduling-integrity.json'
 $integrity=if(Test-Path -LiteralPath $integrityPath){Get-Content -LiteralPath $integrityPath -Raw|ConvertFrom-Json}else{$null}
 $os=@($trace.os);$duration=if($os.Count -gt 1){$os[-1].t-$os[0].t}else{0}
 $cpu=if($duration -gt 0){@{hostOneCorePercent=100*($os[-1].hostCpuMs-$os[0].hostCpuMs)/$duration;uiOneCorePercent=100*($os[-1].uiCpuMs-$os[0].uiCpuMs)/$duration;allocationMiBPerSecond=($os[-1].allocatedBytes-$os[0].allocatedBytes)*1000/1048576/$duration;gen0=$os[-1].gen0-$os[0].gen0;gen1=$os[-1].gen1-$os[0].gen1;gen2=$os[-1].gen2-$os[0].gen2;maximumObservedGcPauseMs=($os.gcPausesMs|Measure-Object -Maximum).Maximum;maximumObservedLohMiB=($os.lohBytes|Measure-Object -Maximum).Maximum/1048576;samplerTotalMs=($os.samplerMs|Measure-Object -Sum).Sum;uiStates=@($os|Group-Object uiState,waitReason|Select-Object Name,Count)}}else{$null}
 $gaps=@($analysis.gaps|Where-Object fullyObserved)
 @{run=$file.Directory.Name;traceFile=$file.Name;conditions=$raw.conditions;traceRevision=if($raw.conditions.traceRevision){$raw.conditions.traceRevision}else{1};renderObserver=if($null -ne $trace.renderObserver){$trace.renderObserver}else{$true};
   traceValid=$analysis.valid;events=$analysis.eventCount;dropped=$trace.dropped;hookCostMs=$trace.hookCostMs;heartbeat=$analysis.heartbeat;measured=$window.timing;bridge=$window.bridge;cpu=$cpu;integrity=$integrity;
   observedGaps=@($gaps|Select-Object startMs,endMs,gapMs,kind,phase,busyMs,notInTracedOperationMs);incompleteGaps=@($analysis.gaps|Where-Object {!$_.fullyObserved}|Select-Object startMs,endMs,gapMs,incompleteReason);pendingOperations=$analysis.pendingOperations;unclosedOperations=$analysis.unclosedOperations;abortedOperationCount=$analysis.abortedOperations.Count;topCallbacks=@($analysis.operations|Select-Object -First 12);longestReadyQueue=$analysis.longestQueue;
   limitations='Instrumented characterization, not final acceptance. Bridge statistics include warmup. CPU/allocation includes tracing; GC event marks are listener delivery times and OS samples can miss intermediate collections. Callback envelopes include nested work and exclude unhooked Win32/COM execution. No CPU descheduling proof.'}
})
@{utc=[DateTimeOffset]::UtcNow;traceCount=$results.Count;runs=$results}|ConvertTo-Json -Depth 25|Set-Content -LiteralPath $OutputPath
$results|ForEach-Object {[pscustomobject]@{run=$_.run;trace=$_.traceFile;p95=$_.measured.heartbeatMs.p95;max=$_.measured.heartbeatMs.max;observedGaps=$_.observedGaps.Count;uiCpu=$_.cpu.uiOneCorePercent;gcPause=$_.cpu.maximumObservedGcPauseMs}}|Format-Table
