"""Read raw evidence. Never edit inputs or translate a failed heartbeat to pass."""
import hashlib, importlib.util, json, math, pathlib, statistics
ROOT=pathlib.Path(__file__).resolve().parents[3]
RUN=pathlib.Path(__file__).resolve().parent
def read(p): return json.loads(pathlib.Path(p).read_text(encoding='utf-8-sig'))
def write(name,value): (RUN/name).write_text(json.dumps(value,indent=2),encoding='utf-8')
spec=importlib.util.spec_from_file_location('policy',ROOT/'scripts/direct_responsiveness.py')
policy=importlib.util.module_from_spec(spec);spec.loader.exec_module(policy)
def summary(directory):
    s=read(directory/'summary.json')
    if isinstance(s,list): return {'passed':bool(s) and all(r['passed'] for r in s),'checks':len(s),'samples':sum(r['samples'] for r in s)}
    n=s['executed'];p=s['passed'];passed=p if type(p) is bool else p==n
    return {'passed':passed and s['failed']==0 and n>0,'checks':n,'path':str(directory)}
def stats(values):
    values=sorted(values)
    if not values:return {'count':0}
    return {'count':len(values),'min':values[0],'median':statistics.median(values),'p95':values[math.ceil(len(values)*.95)-1],'max':values[-1]}
baseline=pathlib.Path((RUN/'baseline-path.txt').read_text(encoding='utf-8-sig').strip())
if (RUN/'baseline-path-final.txt').exists():baseline=pathlib.Path((RUN/'baseline-path-final.txt').read_text(encoding='utf-8-sig').strip())
b=read(baseline/'summary.json')
direct=RUN/'direct-contracts'
if (RUN/'final-direct-path.txt').exists():direct=pathlib.Path((RUN/'final-direct-path.txt').read_text(encoding='utf-8-sig').strip())
ops=RUN/'raw-move-full-1'
suites={name:summary(path) for name,path in {
    'direct':direct,'operations':ops,'presentation':RUN/'contracts','restoration':RUN/'restoration-verify',
    'interaction-control':RUN/'interaction-contract'}.items()}
delivery=read(ops/'delivery-pressure.json')
suites['delivery']={'passed':policy.handoff_within_existing_control(delivery['deliveryQueueMaxMs']) and 0<=delivery['acknowledgedDuringPressureAt']<delivery['pressureMs'] and delivery['browser']['lastSequence']==2,'checks':4}
scheduling=[summary(RUN/('scheduling-'+arm)) for arm in ('baseline','contract')]
suites['scheduling']={'passed':all(s['passed'] for s in scheduling),'checks':sum(s['checks'] for s in scheduling)}
terminal=[summary(RUN/mode) for mode in ('terminal','active-restart','verify')]
suites['terminal-recovery']={'passed':all(s['passed'] for s in terminal),'checks':sum(s['checks'] for s in terminal)}
suites['regression']={'passed':b['baselineCompleted'] and b['allMigrationContractsPassed'] and b['counts']['failed']==0 and all(x['exitCode']==0 for x in b['builds']),'checks':b['counts']['executed']}
newreplays=[summary(RUN/p) for p in ('jst776-replay','jst771-replay')]
replays=b['replayCount']+sum(r['checks'] for r in newreplays)
samples=b['telemetrySamples']+sum(r['samples'] for r in newreplays)
suites['replay']={'passed':all(r['passed'] for r in newreplays) and replays==11 and samples==100632,'checks':replays,'samples':samples}
contracts=read(direct/'direct-contracts.json');observed=read(direct/'direct-observations.json')
c1=read(ROOT/'docs/flight-tracking/evidence/7.24c-native-c1-20260928-194840/native-analysis-v2.json')
t1=read(ROOT/'docs/flight-tracking/evidence/7.24c-terminal-t1-native-20260928-195009/analysis.json')
bad=max(r['delayMs'] for r in c1['longestLiveNonTimerQueuedOperations'] if 'ApplyAsync' in r['name'])
good=t1['trace']['liveMapQueueMs']['max']
negative=next(r['bad'] for r in observed['records'] if r['kind']=='out-of-bounds-negative')
positive=next(r['good'] for r in observed['records'] if r['kind']=='trusted-wpf-positive')
late=next(r['late'] for r in observed['records'] if r['kind']=='late-response-negative')
interaction=read(RUN/'interaction-contract/interaction.json')
calibration={'badHandoffMs':bad,'badHandoffRejected':not policy.handoff_within_existing_control(bad),
    'correctedNativeHandoffMs':good,'correctedNativeHandoffAccepted':policy.handoff_within_existing_control(good),
    'cachedCoordinateRejected':not negative['delivered'],'trustedInBoundsAccepted':positive['delivered'] and positive['trusted'] and positive['inBounds'] and positive['elapsedMs']<=1500,
    'latePositiveRejected':late['delivered'] and late['elapsedMs']>1500 and contracts['input']['passed'],
    'injectedUiStallDetected':interaction['injectedInputDelayMs']>=500,'injectedBrowserStallDetected':interaction['frameMaxMs']>=500,
    'nativeC1Input':{'userObservation':c1['physicalObservation'],'messageAge':c1['liveDragWpfMessageAgeMs']},
    'nativeT1Input':{'userObservation':t1['userObservation'],'messageAge':t1['trace']['liveDragWpfMessageAgeMs']},
    'nativeT1Integrity':t1['trace']['arrivalProcessedTimestampsMatch'] and t1['trace']['traceTelemetryAllFoundInJournal'],
    'boundary':'250ms existing controlled queue guard within750ms Render pressure, not a new universal pixel/native SLA. Native handoff classification is only the measured queue component; C1 remains a handoff failure despite smooth panning.'}
write('calibration.json',calibration)
heartbeat=read(ops/'heartbeat-diagnostic.json')
assessment={'contracts':contracts,'suites':suites,'heartbeat':heartbeat,
    'knownBadDetected':all(calibration[k] for k in ('badHandoffRejected','cachedCoordinateRejected','latePositiveRejected','injectedUiStallDetected','injectedBrowserStallDetected')),
    'knownGoodAccepted':calibration['correctedNativeHandoffAccepted'] and calibration['trustedInBoundsAccepted'] and calibration['nativeT1Integrity'] and suites['restoration']['passed']}
write('acceptance-input.json',assessment);write('acceptance.json',policy.evaluate(assessment))
telemetry=observed['telemetry'];by_at={r['at']:r for r in telemetry}
acks=observed['acknowledgments'];pairs=[]
for receipt in observed['browser']['receipts']:
    from datetime import datetime
    at=datetime.fromisoformat(receipt['observedAt']) if receipt['observedAt'] else None
    row=next((r for r in telemetry if datetime.fromisoformat(r['at'])==at),None)
    if row is not None and str(receipt['sequence']) in acks:
        pairs.append({'sequence':receipt['sequence'],'at':row['at'],'emissionToAckMs':acks[str(receipt['sequence'])]-row['emittedMs']})
# A repeated clock refresh can send the same observation again. First receipt/ACK
# per source sample measures freshness; preserve all pairs rather than erase repeats.
first={}
for p in pairs:first[p['at']]=min(first.get(p['at'],float('inf')),p['emissionToAckMs'])
trace=read(direct/'direct-trace.json');events=trace['events'];posted={e['id']:e for e in events if e['kind']=='posted'}
clock_wait=[e['t']-posted[e['id']]['t'] for e in events if e['kind']=='begin' and e['id'] in posted and 'FireTick' in e['name']]
ticks=observed['dashboardRows']
metrics={'suites':suites,'regressionCounts':b['counts'],'replays':replays,'samples':samples,
    'directRecords':observed['records'],'sourceSamples':len(telemetry),
    'sourceToIngestMs':stats([r['ingestedMs']-r['emittedMs'] for r in telemetry]),
    'sourceToProcessedMs':stats([r['delayMs'] for r in telemetry]),
    'stages':{stage:stats([r['delayMs'] for r in telemetry if r['stage']==stage]) for stage in sorted({r['stage'] for r in telemetry})},
    'directBridge':observed['bridge'],'mapEmissionToFirstAckMs':stats(first.values()),'allEmissionAckPairs':pairs,
    'dashboardTicks':len(ticks),'dashboardTickIntervalMs':stats([q['at']-p['at'] for p,q in zip(ticks,ticks[1:])]),
    'timerPostedToBeginMs_includesIntentionalWait':stats(clock_wait),
    'delivery':delivery,'heartbeat':heartbeat,
    'injectedStalls':{k:interaction[k] for k in ('injectedInputDelayMs','frameMaxMs','emitted','consumed')},
    'notes':'ACK pairs cover observed streamed updates; warmup/hidden coalesced frames need not have a receipt. All telemetry still required exactly. Browser render/source checks are not GPU pixels. Tick callbacks are sampled after real production refresh.'}
write('metrics.json',metrics)
print(json.dumps({'acceptance':policy.evaluate(assessment)['passed'],'suites':suites,'replays':replays,'samples':samples,'heartbeat':heartbeat},indent=2))
