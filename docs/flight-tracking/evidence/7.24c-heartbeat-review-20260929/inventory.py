import json,pathlib,hashlib
root=pathlib.Path(__file__).resolve().parent
rows=[]
for p in pathlib.Path('work/checkpoint-7.24c').rglob('contention.json'):
    if root in p.resolve().parents: continue
    try:
        d=json.loads(p.read_text(encoding='utf-8-sig')); b=d['baseline']; s=d['stressed']; bound=max(150,b['heartbeatMaxMs']+100)
        passed=s['heartbeatCount']>0 and s['heartbeatMaxMs']<500 and s['heartbeatMaxMs']<=bound
        source=pathlib.Path(d['isolatedDirectory'])
        if not source.exists():
            source=next(p.parent.glob('contention-*'),source)
        rows.append(dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),baseline=b['heartbeatMaxMs'],stress=s['heartbeatMaxMs'],limit=bound,passed=passed,
                         sampleCounts=[b['sampleCount'],s['sampleCount']],traced=(source/'stressed/scheduling-trace.json').exists(),raw=str(source),
                         bridge=s.get('mapMetrics'),sameTimeline=b['timelineHash']==s['timelineHash'],sameEvents=b['eventHash']==s['eventHash'],samePirep=b['pirepHash']==s['pirepHash']))
    except Exception as e: rows.append(dict(path=str(p),error=str(e)))
(root/'preserved-contention-inventory.json').write_text(json.dumps(rows,indent=2))
for x in rows:
    if not x.get('passed',False): print(json.dumps({k:v for k,v in x.items() if k!='bridge'}))
print('Inventory',len(rows),'failures',sum(not x.get('passed',False) for x in rows))
