import json,pathlib,math,statistics,collections
root=pathlib.Path(__file__).resolve().parent
def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'),object_hook=lambda d:{k[:1].lower()+k[1:]:v for k,v in d.items()})
def stats(values):
    s=sorted(values)
    if not s:return dict(count=0)
    return dict(count=len(s),min=s[0],median=statistics.median(s),p95=s[math.ceil(.95*len(s))-1],p99=s[math.ceil(.99*len(s))-1],max=s[-1])
def union(intervals):
    end=-float('inf'); total=0
    for a,b in sorted(intervals):
        total+=max(0,b-max(a,end));end=max(end,b)
    return total
def attribution(path,limit):
    d=read(path); ev=d['events'];begins={x['id']:x for x in ev if x['kind']=='begin'}; ends={x['id']:x for x in ev if x['kind']=='end'}
    posted={x['id']:x for x in ev if x['kind']=='posted'};promoted={x['id']:x for x in ev if x['kind']=='priority'}
    hs=[x for x in ev if x['kind']=='heartbeat'];selected=[x for x in hs if x['value']>limit or x['value']>=500]
    maximum=max(hs,key=lambda h:h['value'],default=None)
    if maximum is not None and maximum not in selected:selected.append(maximum)
    windows=[]
    for h in selected:
        start=h['t']-h['value'];end=h['t'];within=[x for x in ev if start<=x['t']<=end]
        spans=[(max(start,b['t']),min(end,ends[i]['t']),b) for i,b in begins.items() if i in ends and ends[i]['t']>start and b['t']<end]
        active=sorted((b for b in begins.values() if 'timer-20ms' in b['name'] and b['t']<=h['t'] and (b['id'] not in ends or ends[b['id']]['t']>=h['t'])),key=lambda b:b['t'])
        hb=active[-1] if active else None;ready=promoted.get(hb['id']) if hb else None;post=posted.get(hb['id']) if hb else None
        bykind=collections.Counter(x['kind'] for x in within)
        byname=collections.Counter(x['name'] for x in within if x['kind']=='begin')
        longest=max(spans,key=lambda x:x[1]-x[0],default=(0,0,{'name':''}))
        os=[x for x in d.get('os',[]) if start-250<=x['t']<=end+250]
        windows.append(dict(startMs=start,endMs=end,intervalMs=h['value'],relativeLimitMs=limit,relativeExceeded=h['value']>limit,absoluteExceeded=h['value']>=500,
            timerPostMs=post['t'] if post else None,promotedMs=ready['t'] if ready else None,readyWaitMs=hb['t']-ready['t'] if hb and ready else None,
            completedCallbackUnionMs=union([(a,b) for a,b,_ in spans]),longestCallbackMs=longest[1]-longest[0],longestCallback=longest[2]['name'],
            callbackNames=dict(byname),kindCounts=dict(bykind),nativeMessageTypes=dict(collections.Counter(x['name'] for x in within if x['kind']=='native-message')),
            nativePendingMasks=dict(collections.Counter(int(x['value']) for x in within if x['kind']=='native-pending')),
            activities=[x for x in within if x['kind'] in ['activity-end','gc','web-message']],os=os,
            unclosedOperations=[b for i,b in begins.items() if b['t']<end and i not in ends]))
    return dict(path=str(path),dropped=d.get('dropped'),hookCostMs=d.get('hookCostMs'),events=len(ev),nativeObserverCost=[x for x in ev if x['kind']=='native-observer-cost'],windows=windows)
def summarize():
    results=[];groups=collections.defaultdict(list);details=[]
    for p in sorted(root.glob('run-*/contention.json')):
        c=read(p);run=p.parent.name;i=int(run.split('-')[1]);traced=i in [1,2,5,6,9,10]
        bound=max(150.,c['baseline']['heartbeatMaxMs']+100.);context={}
        for condition in ['idle','normal']:
            t=read(p.parent/f'context-{condition}/timings.json');context[condition]=t
        for name,path,result,limit in [
            ('baseline',pathlib.Path(c['isolatedDirectory'])/'baseline',c['baseline'],bound),
            ('stressed',pathlib.Path(c['isolatedDirectory'])/'stressed',c['stressed'],bound),
            ('idle',p.parent/'context-idle',None,max(150.,max(context['idle']['heartbeatIntervalsMs'])+100.)),
            ('normal',p.parent/'context-normal',None,max(150.,max(context['idle']['heartbeatIntervalsMs'])+100.))]:
            t=read(path/'timings.json');values=t['heartbeatIntervalsMs'];pending=t.get('pendingHeartbeatMs')
            if (path/'pending-heartbeat.json').exists():pending=read(path/'pending-heartbeat.json')['ageMs']
            rows=dict(run=run,trace=traced,condition=name,stats=stats(values),limit=limit,relativeExceedances=sum(v>limit for v in values),absoluteExceedances=sum(v>=500 for v in values),pendingTailMs=pending)
            if result:rows.update(sampleCount=result['sampleCount'],mapMetrics=result.get('mapMetrics'))
            else: rows.update(mapMetrics=t['mapMetrics'],elapsedMs=t['elapsedMs'],dimensions={k:t[k] for k in ['mainWidth','mainHeight','mapWidth','mapHeight']})
            results.append(rows);groups[name]+=values;groups[f'{name}/trace-{traced}']+=values
            if name in ['idle','normal']:groups[f'{name}/order-{"normal-first" if i%2 else "idle-first"}']+=values
            if (path/'scheduling-trace.json').exists():details.append(attribution(path/'scheduling-trace.json',limit))
    allvalues=sum((v for k,v in groups.items() if '/' not in k),[])
    output=dict(completedRuns=len(results)//4,arms=results,pooled=stats(allvalues),groups={k:stats(v) for k,v in groups.items()},
        failingOriginalComparisons=sum(x['condition']=='stressed' and (x['relativeExceedances']>0 or x['absoluteExceedances']>0) for x in results),
        newAuthoritativeSamples=sum(x.get('sampleCount',0) for x in results),
        absoluteExceedances=sum(x['absoluteExceedances'] for x in results),
        stressedRelativeExceedances=sum(x['relativeExceedances'] for x in results if x['condition']=='stressed'),
        stressedAbsoluteExceedances=sum(x['absoluteExceedances'] for x in results if x['condition']=='stressed'),
        contextNormalRelativeExceedances=sum(x['relativeExceedances'] for x in results if x['condition']=='normal'),
        maxPendingTailMs=max((x['pendingTailMs'] or 0 for x in results),default=0),
        limitation='Nearest-rank percentiles, medians use midpoint. Mixed-condition pooled numbers are descriptive, not one product SLA. Context relative limits are diagnostic comparisons only. Original assertions evaluated only on stressed arms; baseline and idle absolute excursions retained separately. PowerShell wrapper limitMs is rounded by integer overload; these exact double limits and original C# assertion are authoritative.')
    (root/'stability-statistics.json').write_text(json.dumps(output,indent=2));(root/'trace-attribution.json').write_text(json.dumps(details,indent=2))
    print(json.dumps({k:v for k,v in output.items() if k!='arms'},indent=2))
if __name__=='__main__':summarize()
