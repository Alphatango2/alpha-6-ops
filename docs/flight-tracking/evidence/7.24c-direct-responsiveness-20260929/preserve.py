import hashlib,json,pathlib
RUN=pathlib.Path(__file__).resolve().parent
ROOT=RUN.parents[2]
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
groups={}
def verify(name,rows):
    results=[{'path':str(p),'before':h.upper(),'after':sha(p)} for p,h in rows]
    groups[name]={'count':len(results),'unchanged':bool(results) and all(r['before']==r['after'] for r in results),'files':results}
frozen=read(ROOT/'work/checkpoint-7.24c/heartbeat-review-20260929/frozen-before.json')
verify('frozenT2',[(pathlib.Path(r['path']),r['sha256']) for r in frozen])
manifest=read(ROOT/'work/checkpoint-7.24c/jst771-review-20260929/recovery-review/preservation-manifest.json')
profile=pathlib.Path('C:/Users/DanniBaLs/AppData/Local/Alpha6Designs/Alpha6OPS')
verify('profile',[(pathlib.Path(r['Source']),r['SHA256Before']) for r in manifest if pathlib.Path(r['Source']).parent==profile])
for name in ('7.24c-heartbeat-review-20260929','7.24c-gate-validity-20260929'):
    base=ROOT/'docs/flight-tracking/evidence'/name
    verify(name,[(base/r['path'],r['sha256']) for r in read(base/'hashes.json')])
original=read(RUN/'source-before.json')
changed=[str(pathlib.Path(r['Path']).relative_to(ROOT)) for r in original if sha(pathlib.Path(r['Path']))!=r['SHA256'].upper()]
allowed=['src\\Alpha6Ops.Desktop\\MapV2\\MapContentionChecks.cs','src\\Alpha6Ops.Desktop\\MapV2\\MapPerformanceChecks.cs']
result={'groups':groups,'sourceFilesCompared':len(original),'changedExistingSourceFiles':changed,'onlyExpectedDiagnosticChanges':sorted(changed)==sorted(allowed)}
result['passed']=all(g['unchanged'] for g in groups.values()) and result['onlyExpectedDiagnosticChanges'] and groups['frozenT2']['count']==524 and groups['profile']['count']==9
(RUN/'preservation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({**{k:v for k,v in result.items() if k!='groups'},'groups':{k:{'count':v['count'],'unchanged':v['unchanged']} for k,v in groups.items()}},indent=2))
raise SystemExit(0 if result['passed'] else 1)
