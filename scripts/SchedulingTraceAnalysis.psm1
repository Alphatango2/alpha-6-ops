function Measure-SchedulingTrace($Trace){
 if($null -eq $Trace.dropped -or $Trace.dropped -ne 0 -or !$Trace.events){return @{valid=$false;gaps=@();reason='missing or dropped evidence'}}
 $events=@($Trace.events|Sort-Object t);$starts=@{};$posted=@{};$ready=@{};$priorities=@{};$closed=@{};$ops=[Collections.Generic.List[object]]::new();$aborted=[Collections.Generic.List[object]]::new()
 $measurementStart=@($events|Where-Object kind -eq 'measurement-begin'|Select-Object -First 1).t
 $measurementEnd=@($events|Where-Object kind -eq 'measurement-end'|Select-Object -First 1).t
 foreach($e in $events){
  if($e.kind -in @('posted','priority','begin')){$priorities[[string]$e.id]=$e.priority}
  if($e.kind -eq 'posted'){$posted[[string]$e.id]=$e;if($e.priority -gt 0){$ready[[string]$e.id]=$e.t}}
  if($e.kind -eq 'priority'){if($e.priority -le 0){$ready.Remove([string]$e.id)}elseif(!$ready.ContainsKey([string]$e.id)){$ready[[string]$e.id]=$e.t}}
  if($e.kind -eq 'begin'){$starts[[string]$e.id]=$e}
  if($e.kind -eq 'aborted'){$closed[[string]$e.id]=$true;$p=$posted[[string]$e.id];$r=$ready[[string]$e.id];$aborted.Add([pscustomobject]@{id=$e.id;name=$e.name;priority=$e.priority;posted=$p.t;readyAt=$r;abortedAt=$e.t;readyAgeMs=if($null -ne $r){$e.t-$r}else{$null}})}
  if($e.kind -eq 'end'){$closed[[string]$e.id]=$true}
  if($e.kind -eq 'end' -and $starts.ContainsKey([string]$e.id)){
   $b=$starts[[string]$e.id];$p=$posted[[string]$e.id]
   $r=$ready[[string]$e.id]
   $ops.Add([pscustomobject]@{id=$e.id;name=$b.name;priority=$b.priority;start=$b.t;end=$e.t;ms=$e.t-$b.t;posted=$p.t;readyAt=$r;readyWaitMs=if($null -ne $r){$b.t-$r}else{$null};waitMs=if($null -ne $p){$b.t-$p.t}else{$null}})
   $starts.Remove([string]$e.id)
  }
 }
 $keys=[Collections.Generic.HashSet[string]]::new([string[]]@($posted.Keys));$keys.UnionWith([string[]]@($starts.Keys))
 $pending=@(foreach($key in $keys){if(!$closed.ContainsKey($key)){
  $p=$posted[$key];$b=$starts[$key];$r=$ready[$key];$identity=if($null -ne $b){$b}else{$p}
  [pscustomobject]@{id=$identity.id;name=$identity.name;priority=$priorities[$key];posted=$p.t;start=$b.t;readyAt=$r;state=if($null -ne $b){'begun-unclosed'}elseif($null -ne $r){'ready-unstarted'}else{'inactive-unstarted'};readyAgeMs=if($null -ne $r){$events[-1].t-$r}else{$null};executionAgeMs=if($null -ne $b){$events[-1].t-$b.t}else{$null}}
 }})
 # Filter sparse correlation data once, not once per gap in a long trace.
 $interesting=@($events|Where-Object {$_.kind -in @('gc','activity-begin','activity-end','resource-begin','resource-end','drain-request','drain-executed','drain-resumed','producer-complete','probe-producer-awaited','tail-begin','termination-drain-request','termination-drain-executed','termination-drain-resumed','injection-begin','injection-end')})
 $waited=@($ops|Where-Object {$null -ne $_.readyAt -and $_.readyWaitMs -ge 100})
 $gaps=@(foreach($h in $events|Where-Object {$_.kind -in @('heartbeat','pending-heartbeat') -and $_.value -ge 500}){
  $from=[double]$h.t-[double]$h.value;$to=[double]$h.t
  $overlap=@($ops|Where-Object {$_.start -lt $to -and $_.end -gt $from}|Sort-Object start)
  $busy=0.0;$cursor=$from
  foreach($op in $overlap){$a=[Math]::Max($from,$op.start);$b=[Math]::Min($to,$op.end);if($b -gt $cursor){$busy+=$b-[Math]::Max($cursor,$a);$cursor=$b}}
  $unclosed=@($pending|Where-Object {$_.state -eq 'begun-unclosed' -and $_.start -lt $to})
  @{startMs=$from;endMs=$to;gapMs=$h.value;kind=$h.kind;fullyObserved=($from -ge 0 -and $unclosed.Count -eq 0);incompleteReason=if($from -lt 0){'starts before trace'}elseif($unclosed.Count){'operation began but completion was not observed'}else{$null};phase=if($null -eq $measurementStart){'unmarked'}elseif($to -le $measurementStart){'warmup'}elseif($from -ge $measurementEnd){'tail/termination'}else{'measurement/boundary'};busyMs=$busy;notInTracedOperationMs=$h.value-$busy;
    pendingOperations=@($pending|Where-Object {$_.posted -lt $to});abortedOperations=@($aborted|Where-Object {$_.posted -lt $to -and $_.abortedAt -gt $from});
    waitingOperations=@($waited|Where-Object {$_.readyAt -lt $to -and $_.start -gt $from}|Sort-Object readyWaitMs -Descending|Select-Object -First 12);
    os=@($Trace.os|Where-Object {$_.t -ge $from-250 -and $_.t -le $to+250});
    operations=@($overlap|Sort-Object ms -Descending|Select-Object -First 12);priorities=@($overlap|Group-Object priority|ForEach-Object {@{priority=$_.Name;count=$_.Count;inclusiveMs=($_.Group.ms|Measure-Object -Sum).Sum}});
    nearEvents=@($interesting|Where-Object {$_.t -ge $from-10 -and $_.t -le $to+10})}
 })
 $hb=@($events|Where-Object kind -eq heartbeat|ForEach-Object value|Sort-Object)
 $distribution=if($hb.Count){@{count=$hb.Count;p50=$hb[[Math]::Floor(($hb.Count-1)*.5)];p95=$hb[[Math]::Floor(($hb.Count-1)*.95)];p99=$hb[[Math]::Floor(($hb.Count-1)*.99)];max=$hb[-1]}}else{$null}
 $grouped=@($ops|Group-Object name|ForEach-Object {$v=@($_.Group.ms|Sort-Object);[pscustomobject]@{name=$_.Name;count=$_.Count;totalMs=($v|Measure-Object -Sum).Sum;p95=$v[[Math]::Floor(($v.Count-1)*.95)];max=$v[-1]}}|Sort-Object totalMs -Descending)
 return @{valid=$true;eventCount=$events.Count;gaps=$gaps;heartbeat=$distribution;operations=$grouped;longestQueue=@($ops|Where-Object {$_.priority -ge 4 -and $null -ne $_.readyWaitMs}|Sort-Object readyWaitMs -Descending|Select-Object -First 20);pendingOperations=$pending;abortedOperations=@($aborted);unclosedOperations=$starts.Count;limitations='Busy duration unions completed Dispatcher-operation intervals, not CPU time. Pending/unclosed/aborted work is retained; unclosed execution makes affected attribution incomplete. Unattributed time is not evidence of idle time. GC and activity delivery only correlate. Runnable wait excludes Inactive timer delay; pre-trace gaps cannot establish a traced mechanism.'}
}
Export-ModuleMember -Function Measure-SchedulingTrace
