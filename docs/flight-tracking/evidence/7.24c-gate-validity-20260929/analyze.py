"""Read preserved traces only; never launch the app or alter an acceptance rule."""
import collections, hashlib, json, math, pathlib, statistics

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[2]
OLD = REPO / 'work/checkpoint-7.24c/heartbeat-review-20260929'
SOURCES = {}

def read(path):
    path = pathlib.Path(path)
    raw = path.read_bytes()
    SOURCES[str(path.relative_to(REPO))] = hashlib.sha256(raw).hexdigest()
    return json.loads(raw.decode('utf-8-sig'), object_hook=lambda d: {k[:1].lower()+k[1:]: v for k,v in d.items()})

def stats(values):
    s = sorted(values)
    return dict(count=len(s), median=statistics.median(s), p95=s[math.ceil(len(s)*.95)-1],
                p99=s[math.ceil(len(s)*.99)-1], max=s[-1]) if s else dict(count=0)

def events_in(events, start, end):
    e = [x for x in events if start <= x['t'] <= end]
    render = [x['t'] for x in e if x['kind']=='begin' and 'MediaContext.RenderMessageHandler' in x['name']]
    return dict(startMs=start, endMs=end, kinds=dict(collections.Counter(x['kind'] for x in e)),
        renderCallbacks=len(render), renderStartSpacingMs=stats([b-a for a,b in zip(render,render[1:])]),
        wpfInputs=[x for x in e if x['kind']=='wpf-input'],
        physicalDragMessageAgeMs=stats([x['value'] for x in e if x['kind']=='physical-drag']),
        telemetryProcessingDelayMs=stats([x['value'] for x in e if x['kind']=='native-telemetry-processed']),
        telemetryProcessed=[x for x in e if x['kind']=='native-telemetry-processed'],
        browserMessages=[x for x in e if x['kind'] in ['web-message','browser-message']])

trace = read(OLD/'run-09/context-idle/scheduling-trace.json')
events = trace['events']
heartbeat = max((x for x in events if x['kind']=='heartbeat'), key=lambda x:x['value'])
idle = events_in(events, heartbeat['t']-heartbeat['value'], heartbeat['t'])
idle['heartbeatIntervalMs'] = heartbeat['value']
attribution = read(OLD/'trace-attribution.json')
row = next(x for x in attribution if 'run-09\\context-idle' in x['path'])
idle['priorAttribution'] = max(row['windows'], key=lambda x:x['intervalMs'])
assert idle['heartbeatIntervalMs']==3128.1934 and idle['renderCallbacks']==750
assert len(idle['wpfInputs'])==6 and idle['telemetryProcessingDelayMs']['count']==0

# All six traced one-Hz contexts: pair ordered offers/ACKs only after checking
# 30 independent offers, 30 ACKs, zero coalescing, no rejected inputs, no pending tail.
# This is an ordinal association, not an unrecorded sequence-ID/frame measurement.
normal=[]
for p in sorted(OLD.glob('run-*/context-normal/timings.json')):
    t=read(p); m=t['mapMetrics']; item=dict(run=p.parents[1].name, metrics=m)
    tp=p.with_name('scheduling-trace.json')
    if tp.exists():
        ev=read(tp)['events']
        offers=[x for x in ev if x['kind']=='map-offered']
        acks=[x for x in ev if x['kind']=='web-message' and x['name']=='ack']
        assert len(offers)==len(acks)==30 and m['coalesced']==m['rejected']==0 and not m['pending']
        assert [x['value'] for x in offers]==list(range(30))
        pairs=[dict(index=i,offerMs=o['t'],ackMs=a['t'],elapsedMs=a['t']-o['t']) for i,(o,a) in enumerate(zip(offers,acks))]
        assert all(x['elapsedMs']>=0 for x in pairs)
        windows=[]
        for h in ev:
            if h['kind']!='heartbeat' or h['value']<500: continue
            a,b=h['t']-h['value'],h['t']
            overlapping=[x for x in pairs if x['offerMs']<=b and x['ackMs']>=a]
            windows.append(dict(startMs=a,endMs=b,heartbeatMs=h['value'],pairs=overlapping,
                ackCount=sum(a<=x['ackMs']<=b for x in pairs)))
        item.update(pairs=pairs,windows=windows)
    normal.append(item)
window_pairs={}
for n in normal:
    for w in n.get('windows',[]):
        for pair in w['pairs']:window_pairs[(n['run'],pair['index'])]=pair
over_one={}
for n in normal:
    for w in n.get('windows',[]):
        if w['heartbeatMs']>=1000:
            for pair in w['pairs']:over_one[(n['run'],pair['index'])]=pair
normal_summary=dict(runs=len(normal),offers=sum(n['metrics']['offered'] for n in normal),
    acknowledgments=sum(n['metrics']['acknowledged'] for n in normal),
    mapDispatchMaxMs=max(n['metrics']['dispatcherQueueMs']['max'] for n in normal),
    ackMaxMs=max(n['metrics']['acknowledgmentMs']['max'] for n in normal),
    tracedLongHeartbeatWindows=sum(len(n.get('windows',[])) for n in normal),
    offersOverlappingLongWindows=stats([x['elapsedMs'] for x in window_pairs.values()]),
    offersOverlappingLongWindowsExcludingFirst=stats([x['elapsedMs'] for x in window_pairs.values() if x['index']>0]),
    offersOverlappingOneSecondWindows=stats([x['elapsedMs'] for x in over_one.values()]),
    allTracedOffersAfterFirst=stats([p['elapsedMs'] for n in normal for p in n.get('pairs',[]) if p['index']>0]))

# Native T1: correlate the largest ready non-timer Background wait to actual
# already-recorded work, not to a hypothetical physical probe.
t1 = read(REPO/'docs/flight-tracking/evidence/7.24c-terminal-t1-native-20260928-195009/trace.json')
ev=t1['events']; posted={x['id']:x for x in ev if x['kind']=='posted'}
begin=[x for x in ev if x['kind']=='begin' and x['priority']==4 and x['id'] in posted and 'DispatcherTimer' not in x['name']]
worst=max(begin,key=lambda b:b['t']-posted[b['id']]['t']); post=posted[worst['id']]
t1_window=events_in(ev,post['t'],worst['t'])
t1_window.update(queueWaitMs=worst['t']-post['t'],callback=worst['name'])
map_begin=[x for x in ev if x['kind']=='begin' and 'WebMapRenderer' in x['name'] and x['id'] in posted and post['t']<=x['t']<=worst['t']]
t1_window['mapDispatchQueueMs']=stats([b['t']-posted[b['id']]['t'] for b in map_begin])
assert abs(t1_window['queueWaitMs']-1483.3783)<.001
# Keep actual production clock delay separate: posting-to-start includes the
# intentional one-second inactive timer phase. Use promotion-to-start as well.
promoted={x['id']:x for x in ev if x['kind']=='priority'}
clocks=[x for x in ev if x['kind']=='begin' and 'dashboard-clock' in x['name'] and x['id'] in promoted]
clock=max(clocks,key=lambda b:b['t']-promoted[b['id']]['t'])
clock_window=events_in(ev,promoted[clock['id']]['t'],clock['t'])
clock_window.update(readyWaitMs=clock['t']-promoted[clock['id']]['t'],callback=clock['name'],
    caveat='Observed late clock callback, not proof of an independently captured stale clock pixel or flight delay.')

# Ordered emission/processing pairs are valid because each diagnostic source
# emits a serial 395-reading stream and processed indices are explicitly recorded.
flight=[]
for p in sorted(OLD.glob('run-*/contention-*/stressed/scheduling-trace.json')):
    ev=read(p)['events']; emitted=[x for x in ev if x['kind']=='sample-emitted']; done=[x for x in ev if x['kind']=='sample-processed']
    assert len(emitted)==len(done)==395 and [x['value'] for x in done]==list(range(395))
    pairs=[dict(emittedMs=e['t'],processedMs=d['t'],elapsedMs=d['t']-e['t']) for e,d in zip(emitted,done)]
    h=max((x for x in ev if x['kind']=='heartbeat'),key=lambda h:h['value']); a,b=h['t']-h['value'],h['t']
    flight.append(dict(run=p.parents[2].name,telemetryDelayMs=stats(x['elapsedMs'] for x in pairs),
        maximumHeartbeatMs=h['value'],inWorstGap=stats(x['elapsedMs'] for x in pairs if a<=x['processedMs']<=b)))

restoration=read(REPO/'work/checkpoint-7.24c/input-gate-20260929/restoration-verify/restoration-navigation.json')
restoration_summary=dict(returns=len(restoration['rows']),coldMs=restoration['coldMs'],
    correctVisibleMs=stats(r['correctVisibleMs'] for r in restoration['rows']),
    restoreIncludingReadbackMs=stats(r['restoreMs'] for r in restoration['rows']))
result=dict(idle3128=idle,normalContextSummary=normal_summary,normalContexts=normal,
    nativeT1LongBackgroundWait=t1_window,nativeT1ClockReadyWait=clock_window,tracedStressedFlightTelemetry=flight,
    restoration=restoration_summary,
    limitations=['Offline reanalysis only; no new app execution or physical test.',
    'Render callback spacing is not GPU/frame presentation.',
    'ACK receipt is not rendered-map latency; normal-context offer/ACK pairs use verified ordinal association.',
    'No commanded input, telemetry producer, or map offers in the 3.128s idle interval; those response times are unmeasured.',
    'WPF input notifications are not proof of actual user actions.'])
(HERE/'correlation-v2.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
(HERE/'analysis-sources-v2.json').write_text(json.dumps(SOURCES,indent=2),encoding='utf-8')
print(json.dumps(dict(normal=normal_summary,nativeT1=t1_window,clock=clock_window,restoration=restoration_summary),indent=2))
