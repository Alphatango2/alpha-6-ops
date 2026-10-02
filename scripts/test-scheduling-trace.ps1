param([string]$OutputPath='work/checkpoint-7.24a.2/trace-contracts.json')
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'SchedulingTraceAnalysis.psm1') -Force
function Trace($rows,$dropped=0){@{dropped=$dropped;events=$rows;conditions=@{trace=$true}}}
function Row($t,$kind,$id=0,$name='',$value=0,$priority=5){@{t=$t;kind=$kind;id=$id;name=$name;value=$value;priority=$priority}}
$cases=@(
 @{name='600ms synchronous stall detected';trace=(Trace @((Row 0 'begin' 1 'blocking' 0 9),(Row 600 'end' 1 'blocking' 0 9),(Row 610 'heartbeat' 0 '' 610)));gap=$true;busy=600},
 @{name='idle wait not called busy';trace=(Trace @((Row 0 'posted' 2 'waiting'),(Row 610 'begin' 2 'waiting'),(Row 611 'end' 2 'waiting'),(Row 612 'heartbeat' 0 '' 612)));gap=$true;busy=1},
 @{name='nested work not double counted';trace=(Trace @((Row 0 'begin' 1 'outer'),(Row 50 'begin' 2 'inner'),(Row 550 'end' 2 'inner'),(Row 600 'end' 1 'outer'),(Row 610 'heartbeat' 0 '' 610)));gap=$true;busy=600},
 @{name='healthy trace';trace=(Trace @((Row 20 'heartbeat' 0 '' 20),(Row 40 'heartbeat' 0 '' 20)));gap=$false;busy=0},
 @{name='dropped evidence rejected';trace=(Trace @((Row 20 'heartbeat' 0 '' 20)) 1);invalid=$true},
 @{name='pending final stall retained';trace=(Trace @((Row 610 'pending-heartbeat' 0 '' 610)));gap=$true;busy=0},
 @{name='pre-trace gap cannot establish traced mechanism';trace=(Trace @((Row 20 'heartbeat' 0 '' 600)));gap=$true;busy=0;fullyObserved=$false},
 @{name='timer inactive wait excluded from runnable queue age';trace=(Trace @((Row 0 'posted' 1 'timer' 0 0),(Row 1000 'priority' 1 'timer' 0 5),(Row 1010 'begin' 1 'timer' 0 5),(Row 1011 'end' 1 'timer' 0 5)));gap=$false;busy=0;readyWait=10},
 @{name='pending runnable operation retained at stop';trace=(Trace @((Row 0 'posted' 1 'waiting'),(Row 610 'pending-heartbeat' 0 '' 610)));gap=$true;busy=0;pending=1;pendingAge=610},
 @{name='unclosed execution makes gap attribution incomplete';trace=(Trace @((Row 0 'posted' 1 'running'),(Row 1 'begin' 1 'running'),(Row 610 'pending-heartbeat' 0 '' 610)));gap=$true;busy=0;fullyObserved=$false;pending=1},
 @{name='aborted queued operation retained';trace=(Trace @((Row 0 'posted' 1 'aborted'),(Row 600 'aborted' 1 'aborted'),(Row 610 'pending-heartbeat' 0 '' 610)));gap=$true;busy=0;aborted=1},
 @{name='pending timer retains its promoted priority';trace=(Trace @((Row 0 'posted' 1 'timer' 0 0),(Row 100 'priority' 1 'timer' 0 5),(Row 610 'pending-heartbeat' 0 '' 610)));gap=$true;busy=0;pending=1;pendingPriority=5}
)
$results=@(foreach($c in $cases){$r=Measure-SchedulingTrace $c.trace;$ok=if($c.invalid){!$r.valid}else{$r.valid -and ($r.gaps.Count -gt 0) -eq $c.gap -and (!$c.gap -or [Math]::Abs($r.gaps[0].busyMs-$c.busy) -lt .01)};if($c.ContainsKey('fullyObserved')){$ok=$ok -and $r.gaps[0].fullyObserved -eq $c.fullyObserved};if($c.ContainsKey('readyWait')){$ok=$ok -and $r.longestQueue[0].readyWaitMs -eq $c.readyWait};if($c.ContainsKey('pending')){$ok=$ok -and $r.pendingOperations.Count -eq $c.pending};if($c.ContainsKey('pendingAge')){$ok=$ok -and $r.pendingOperations[0].readyAgeMs -eq $c.pendingAge};if($c.ContainsKey('pendingPriority')){$ok=$ok -and $r.pendingOperations[0].priority -eq $c.pendingPriority};if($c.ContainsKey('aborted')){$ok=$ok -and $r.abortedOperations.Count -eq $c.aborted};@{name=$c.name;passed=$ok;actual=$r}})
@{executed=$results.Count;passed=@($results|Where-Object passed).Count;failed=@($results|Where-Object {!$_.passed}).Count;checks=$results}|ConvertTo-Json -Depth 12|Set-Content $OutputPath
$results|Select-Object name,passed|ConvertTo-Json
if(@($results|Where-Object {!$_.passed}).Count){exit 1}
