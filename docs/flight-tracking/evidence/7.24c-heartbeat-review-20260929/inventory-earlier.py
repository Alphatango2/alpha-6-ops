import json,pathlib
from analyze import read,attribution,stats,root
rows=[]
for directory in pathlib.Path('work').glob('checkpoint-7.24*'):
    if directory.name=='checkpoint-7.24c':continue
    for p in directory.rglob('contention.json'):
        try:
            d=read(p);b=d['baseline'];s=d['stressed'];bound=max(150.,b['heartbeatMaxMs']+100.)
            if s['heartbeatMaxMs']<500 and s['heartbeatMaxMs']<=bound:continue
            raw=pathlib.Path(d['isolatedDirectory'])/'stressed';t=read(raw/'timings.json') if (raw/'timings.json').exists() else None
            row=dict(path=str(p),baseline=b['heartbeatMaxMs'],stress=s['heartbeatMaxMs'],limit=bound,traceAvailable=(raw/'scheduling-trace.json').exists(),
                sameTimeline=b['timelineHash']==s['timelineHash'],sameEvents=b['eventHash']==s['eventHash'],sampleCounts=[b['sampleCount'],s['sampleCount']],bridge=s.get('mapMetrics'),
                relativeExceedances=sum(v>bound for v in t['heartbeatIntervalsMs']) if t else None,absoluteExceedances=sum(v>=500 for v in t['heartbeatIntervalsMs']) if t else None,
                limitation='Earlier checkpoint under its original host/scheduling/harness state; no native-message trace means exact per-gap cause cannot be recovered. Not counted in current controlled series.')
            if row['traceAvailable']:row['detail']=attribution(raw/'scheduling-trace.json',bound)
            rows.append(row)
        except Exception as e: rows.append(dict(path=str(p),error=str(e)))
(root/'earlier-failure-inventory.json').write_text(json.dumps(rows,indent=2))
print(json.dumps([{k:v for k,v in r.items() if k not in ['bridge','detail']} for r in rows],indent=2))
