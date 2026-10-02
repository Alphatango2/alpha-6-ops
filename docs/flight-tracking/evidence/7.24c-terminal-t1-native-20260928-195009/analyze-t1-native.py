"""Preserve and analyze the completed T1 parked capture. No application writes."""
from pathlib import Path
import collections, datetime, hashlib, json, re

root = next(p for p in Path(__file__).resolve().parents if (p / 'src/Alpha6Ops.Core/TerminalShutdown.cs').is_file())
run = root / 'outputs/Alpha6OPS-Desktop-S4-7.24C-T1-20260928-00e3f628b90e/native-input-20260928-195009-d40cb07e'
logs = Path('C:/Users/DanniBaLs/AppData/Local/Alpha6Designs/Alpha6OPS/TestLogs')
stem = 'Alpha6OPS-20260929-005019-249327d130104a6880dff5112b739233'
dest = root / 'docs/flight-tracking/evidence/7.24c-terminal-t1-native-20260928-195009'
dest.mkdir(parents=True, exist_ok=True)

def preserve(path):
    data = path.read_bytes()
    target = dest / path.name
    if target.exists():
        assert target.read_bytes() == data, 'Do not overwrite different evidence'
    else:
        with target.open('xb') as f: f.write(data)
    assert path.read_bytes() == data
    return dict(source=str(path), preservedPath=str(target), bytes=len(data), sha256=hashlib.sha256(data).hexdigest().upper())

rows = [json.loads(x) for x in (logs / (stem + '.jsonl')).read_text(encoding='utf-8-sig').splitlines() if x.strip()]
assert rows[-1]['kind'] == 'session_ended'
assert [x['sequence'] for x in rows] == list(range(1, len(rows) + 1))
manifest = [preserve(p) for p in [run / 'record.json', run / 'trace.json', logs / (stem + '.jsonl'), logs / (stem + '.json')]]
export = json.loads((dest / (stem + '.json')).read_text(encoding='utf-8-sig'))
assert export['sessionEnded'] and not export['incompleteLastLine']
assert export['events'] == rows
trace = json.loads((dest / 'trace.json').read_text(encoding='utf-8-sig'))
by_kind = collections.defaultdict(list)
for e in trace['events']: by_kind[e['Kind']].append(e)
telemetry = [r for r in rows if r['kind'] == 'telemetry']
tracking = [r for r in rows if r['kind'] == 'tracking_state']
decisions = [r for r in rows if r['kind'] == 'terminal_block_in']
def dt(s): return datetime.datetime.fromisoformat(s)
def seconds(a, b): return (dt(b) - dt(a)).total_seconds()
def stats(values):
    values = sorted(values)
    return {'count':len(values), **({k:values[int((len(values)-1)*q)] for k,q in [('p50',.5),('p95',.95),('p99',.99),('max',1)]} if values else {})}
def canonical_utc(s):
    assert s.endswith('+00:00')
    return re.sub(r'\.(\d+)(?=\+00:00$)', lambda m:'.'+m[1].ljust(7,'0'),s)
def evidence_status(r):
    e = r['detail']['terminalShutdown']; now = dt(r['detail']['telemetryReceivedUtc'])
    fresh = lambda v: 0 <= (now-dt(v)).total_seconds() <= 3
    if not e['Valid'] or not e['Qualified'] or e['SimulatorUtc'] != r['simulatorUtc'] or not e['ConnectionId'] or not fresh(e['ReceivedUtc']): return 'Invalid'
    engines = e['Engines']
    if len(engines) != e['ExpectedEngineCount'] or sorted(x['Index'] for x in engines) != list(range(1, e['ExpectedEngineCount']+1)): return 'Invalid'
    if any(x['Cutoff'] is None or x['Combustion'] is None or not fresh(x['ReceivedUtc']) or x['Cutoff'] and x['Combustion'] for x in engines): return 'Invalid'
    return 'Operating' if e['OperatingEvidence'] or any(x['Cutoff'] != True or x['Combustion'] != False for x in engines) else 'Confirmed'
def sample(r):
    d = r['detail']
    return dict(simulatorUtc=r['simulatorUtc'], recordedAtUtc=r['recordedAtUtc'], shutdownStatusAtReceipt=evidence_status(r),
                terminal=d['terminalShutdown'], engineDataValid=d['engineDataValid'], engineStates=d['engineStates'],
                engineReadings=d['engineReadings'], enginesRunning=d['enginesRunning'], runningEngineCount=d['runningEngineCount'])

transitions=[]; prior=None
for r in telemetry:
    e=r['detail']['terminalShutdown']
    key=(e['Valid'], e['Qualified'], tuple((x['Index'],x['Cutoff'],x['Combustion']) for x in e['Engines']), evidence_status(r))
    if key != prior: transitions.append(sample(r)); prior=key
first_confirmed = next(r for r in telemetry if evidence_status(r)=='Confirmed')
engine_timelines = []
for i in (1,2):
    cutoff=next(r for r in telemetry if any(e['Index']==i and e['Cutoff']==True for e in r['detail']['terminalShutdown']['Engines']))
    no_combustion=next(r for r in telemetry if any(e['Index']==i and e['Combustion']==False for e in r['detail']['terminalShutdown']['Engines']))
    mechanical_off=next((r for r in telemetry if any(e['Index']==i and e['Condition']==1 for e in r['detail']['engineStates'])), None)
    engine_timelines.append(dict(engine=i, cutoffFirstObservedUtc=cutoff['simulatorUtc'], combustionOffFirstObservedUtc=no_combustion['simulatorUtc'],
                                 mechanicalOffFirstObservedUtc=mechanical_off['simulatorUtc'] if mechanical_off else None,
                                 cutoffToCombustionOffSeconds=seconds(cutoff['simulatorUtc'],no_combustion['simulatorUtc'])))

arrived=by_kind['native-telemetry-arrival']; processed=by_kind['native-telemetry-processed']; start,end=arrived[0]['T'],arrived[-1]['T']
posted={}; begun={}; queued=[]; executed=[]
for e in trace['events']:
    kind,ident=e['Kind'],e['Id']
    if kind=='posted': posted[ident]=e
    elif kind=='begin':
        begun[ident]=e; before=posted.pop(ident,None)
        if before and start<=before['T']<=end: queued.append(dict(name=e['Name'],priority=e['Priority'],ms=e['T']-before['T'],postedMs=before['T']))
    elif kind=='end':
        before=begun.pop(ident,None)
        if before and start<=before['T']<=end: executed.append(dict(name=e['Name'],priority=e['Priority'],ms=e['T']-before['T'],startedMs=before['T']))
    elif kind=='aborted': posted.pop(ident,None)
activities=collections.defaultdict(list)
for e in by_kind['activity-end']:
    if start<=e['T']<=end: activities[e['Name']].append(e['Value'])
live_drag=[e for e in by_kind['physical-drag'] if start<=e['T']<=end]
non_timer=[x for x in queued if 'DispatcherTimer' not in x['name']]
map_queue=[x for x in queued if any(k in x['name'] for k in ('WebMapRenderer','MapProjection','MapBridge'))]
a=collections.Counter(canonical_utc(e['Name']) for e in arrived)
p=collections.Counter(canonical_utc(e['Name']) for e in processed)
j=collections.Counter(canonical_utc(r['simulatorUtc']) for r in telemetry)
result = dict(build='S4-7.24C-T1-20260928-00e3f628b90e', evidence=manifest,
    userObservation={'hitchingOrFlickering':'None observed', 'F9':'Not pressed', 'shutdownOrder':[1,2],
        'scrollbarInitial':'User observed a right-side Operations scrollbar during the test.',
        'scrollbarFollowUp':'User reports no right-side scrollbar in the build just started; suggests it might have been in the test environment.',
        'scrollbarDisposition':'Transient observation, cause unconfirmed. No layout changes made.'},
    journal=dict(events=len(rows), sequenceContinuous=True, exportExactlyMatches=True, sessionEnded=True,
        kinds=dict(collections.Counter(r['kind'] for r in rows)), start=rows[0]['recordedAtUtc'], end=rows[-1]['recordedAtUtc'],
        telemetrySamples=len(telemetry), firstSample=telemetry[0]['simulatorUtc'], lastSample=telemetry[-1]['simulatorUtc'],
        aircraft=sorted(set(r['detail']['aircraft'] for r in telemetry)),
        phaseCounts=dict(collections.Counter(r['detail']['phase'] for r in tracking)),
        allStationaryBrakedUnpaused=all(r['detail']['onGround'] and r['detail']['groundSpeedKnots']<.5 and r['detail']['parkingBrake'] and not r['detail']['paused'] and not r['detail']['slewing'] for r in telemetry),
        decisions=decisions, anyTerminalEligible=any(r['detail']['TerminalBlockInEligible'] for r in tracking),
        trackingEvents=[r for r in rows if r['kind']=='tracking_event']),
    shutdown=dict(statusCounts=dict(collections.Counter(evidence_status(r) for r in telemetry)), transitions=transitions,
        engines=engine_timelines, firstConfirmed=sample(first_confirmed),
        afterFinalCutoffSeconds=seconds(engine_timelines[1]['cutoffFirstObservedUtc'], first_confirmed['simulatorUtc']),
        note='Status reconstructed at each telemetry receipt using the frozen evidence contract. First sample observations are not raw pilot-command timestamps.'),
    trace=dict(events=len(trace['events']), dropped=trace['dropped'], osSamples=len(trace['os']),
        eventKinds={k:len(v) for k,v in by_kind.items()}, hookCostMs=trace['hookCostMs'],
        liveWindowMs=[start,end], arrived=len(arrived), processed=len(processed), arrivalProcessedTimestampsMatch=a==p,
        traceTelemetryAllFoundInJournal=not bool(a-j), arrivalToProcessedMs=stats([e['Value'] for e in processed]),
        liveDragNotifications=len(live_drag), liveDragWpfMessageAgeMs=stats([e['Value'] for e in live_drag]),
        userStallMarkers=len(by_kind['physical-user-stall-marker']), resizeEvents=by_kind['resize'],
        liveNonTimerQueueMs=stats([x['ms'] for x in non_timer]), longestNonTimerQueue=sorted(non_timer,key=lambda x:x['ms'],reverse=True)[:10],
        liveMapQueueMs=stats([x['ms'] for x in map_queue]), longestMapQueue=sorted(map_queue,key=lambda x:x['ms'],reverse=True)[:10],
        liveActivityMs={k:stats(v) for k,v in activities.items()},
        longestOperationEnvelopes=sorted(executed,key=lambda x:x['ms'],reverse=True)[:10], limitations=trace['limitations']),
    limitations=['Parked/no-ON session cannot measure destination IN or PIREP eligibility latency.',
        'All arrival guards are false together; this does not isolate each individual guard.',
        'Native WPF notification timing is not browser/GPU frame-presentation latency.',
        'No ScrollViewer extent/viewport/scrollbar snapshots are recorded by this trace.',
        'Native trace is shorter than the closed journal; their counts describe different windows.',
        'No independent simulator source sequence counter or native heartbeat gate is available.',
        'Physical observation is authoritative for this run only; automated gates remain separate.',
        'Dispatcher timer queue waits include intentional inactive time; they are excluded from non-timer queue statistics.'])
target=dest/'analysis.json'
data=json.dumps(result,indent=2)+'\n'
if target.exists(): assert target.read_text(encoding='utf-8')==data
else: target.write_text(data,encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['evidence','journal','shutdown']},indent=2))
print('JOURNAL',json.dumps({k:v for k,v in result['journal'].items() if k not in ['decisions','trackingEvents']},indent=2))
print('SHUTDOWN',json.dumps(result['shutdown'],indent=2))
