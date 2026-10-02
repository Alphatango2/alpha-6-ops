import json,pathlib
from analyze import read,attribution,stats,root
rows=read(root/'preserved-contention-inventory.json');out=[]
for r in rows:
    if r.get('passed',False):continue
    path=pathlib.Path(r['raw'])/'stressed';d=read(path/'timings.json');v=d['heartbeatIntervalsMs']
    r['stats']=stats(v);r['relativeExceedances']=sum(x>r['limit'] for x in v);r['absoluteExceedances']=sum(x>=500 for x in v)
    r['detail']=attribution(path/'scheduling-trace.json',r['limit']) if (path/'scheduling-trace.json').exists() else None
    out.append(r)
(root/'preserved-failure-correlation.json').write_text(json.dumps(out,indent=2))
for r in out:
    w=max(r['detail']['windows'],key=lambda x:x['intervalMs']) if r['detail'] else {}
    print(json.dumps(dict(path=r['path'],max=r['stress'],relativeExceedances=r['relativeExceedances'],absoluteExceedances=r['absoluteExceedances'],readyWait=w.get('readyWaitMs'),union=w.get('completedCallbackUnionMs'),longest=w.get('longestCallbackMs'),counts=w.get('kindCounts'),bridge=r.get('bridge'))))
