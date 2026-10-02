import json,pathlib,hashlib,zipfile,shutil
from analyze import read,root,summarize
assert read(root/'series-complete.json')['runs']==12
summarize()
stats=read(root/'stability-statistics.json')
assert stats['completedRuns']==12 and stats['newAuthoritativeSamples']==9480
hashfile=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
before=read(root/'frozen-before.json');frozen=pathlib.Path('outputs/Alpha6OPS-Desktop-S4-7.24C-T2-20260929-f421af15530c').resolve()
current={str(p) for p in frozen.rglob('*') if p.is_file()};original={r['path'] for r in before}
preserve=dict(files=len(before),changed=[r['path'] for r in before if not pathlib.Path(r['path']).exists() or hashfile(pathlib.Path(r['path']))!=r['sha256']],added=sorted(current-original),removed=sorted(original-current))
preserve['unchanged']=not any(preserve[k] for k in ['changed','added','removed'])
(root/'frozen-preservation.json').write_text(json.dumps(preserve,indent=2));assert preserve['unchanged']
entry=read(root/'entry-source-hashes.json');changed=[r['path'] for r in entry if not pathlib.Path(r['path']).exists() or hashfile(pathlib.Path(r['path']))!=r['sha256']]
scope=dict(existingChanged=changed,newSource=['src/Alpha6Ops.Desktop/MapV2/HeartbeatContextChecks.cs','src/Alpha6Ops.Desktop/MapV2/HeartbeatNativeObserver.cs'],productionBehaviorChanged=False)
assert all(pathlib.Path(p).name in ['MapContentionChecks.cs','MapV2Checks.cs'] for p in changed)
(root/'source-scope.json').write_text(json.dumps(scope,indent=2))
profile=[]
for r in read(pathlib.Path('work/checkpoint-7.24c/jst771-review-20260929/recovery-review/preservation-manifest.json')):
    if r['category']=='profile':profile.append(dict(path=r['source'],unchanged=hashfile(pathlib.Path(r['source']))==r['sHA256Before']))
if not profile:
    # Select the explicit known profile directory, not supplied/historical files.
    for r in read(pathlib.Path('work/checkpoint-7.24c/jst771-review-20260929/recovery-review/preservation-manifest.json')):
        if str(pathlib.Path(r['source']).parent).lower()==r'c:\users\dannibals\appdata\local\alpha6designs\alpha6ops':
            profile.append(dict(path=r['source'],unchanged=hashfile(pathlib.Path(r['source']))==r['sHA256Before']))
assert len(profile)==9 and all(r['unchanged'] for r in profile)
(root/'profile-preservation.json').write_text(json.dumps(profile,indent=2))
runs=[]
for p in sorted(root.glob('run-*/summary.json')):
    s=read(p);c=read(p.parent/'contention.json')
    runs.append(dict(run=p.parent.name,executed=s['executed'],failed=s['failed'],failedChecks=[v for v in s['checks'] if not v['passed']],
        baselineMs=c['baseline']['heartbeatMaxMs'],stressedMs=c['stressed']['heartbeatMaxMs'],exactLimitMs=max(150.,c['baseline']['heartbeatMaxMs']+100.),
        timelinesEqual=c['baseline']['timelineHash']==c['stressed']['timelineHash'],eventsEqual=c['baseline']['eventHash']==c['stressed']['eventHash'],pirepsEqual=c['baseline']['pirepHash']==c['stressed']['pirepHash']))
(root/'series-results-exact.json').write_text(json.dumps(runs,indent=2))
summary=dict(classification='UNRESOLVED',productionChange=False,thresholdChanged=False,nativeT2Cleared=False,checkpoint='NO-GO',
    runs=runs,freshAssertions=dict(executed=sum(r['executed'] for r in runs),failed=sum(r['failed'] for r in runs)),statistics=stats,
    retainedVerifiedBaseline=dict(fullRegressionPassed=1953,fullRegressionFailed=0,nativeSkipped=2,browserInputLifecycle=199,restoration=157,terminalRecovery=293,replays=11,telemetrySamples=100632),
    frozenPreserved=preserve,profilePreserved=profile,sourceScope=scope)
(root/'review-summary.json').write_text(json.dumps(summary,indent=2))
dest=pathlib.Path('docs/flight-tracking/evidence/7.24c-heartbeat-review-20260929');dest.mkdir(exist_ok=False,parents=True)
for p in root.iterdir():
    if p.is_file() and p.name not in ['entry.diff','entry-source-hashes.json','statistics-progress.txt']:
        shutil.copy2(p,dest/p.name)
with zipfile.ZipFile(dest/'raw-observations.zip','x',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for run in sorted(root.glob('run-*')):
        for p in run.rglob('*.json'):
            if p.name in ['timings.json','pending-heartbeat.json','scheduling-trace.json','contention.json','summary.json','browser-stall-result.json','delivery-pressure.json','host-lifetime.json','routed-input.json']:
                z.write(p,str(p.relative_to(root)))
manifest=[dict(path=str(p.relative_to(dest)),bytes=p.stat().st_size,sha256=hashfile(p)) for p in sorted(dest.rglob('*')) if p.is_file()]
(dest/'hashes.json').write_text(json.dumps(manifest,indent=2))
assert all(hashfile(dest/r['path'])==r['sha256'] for r in manifest)
print(json.dumps(dict(preserved=preserve,profiles=len(profile),source=scope,fresh=summary['freshAssertions'],archiveFiles=len(manifest),archive=str(dest)),indent=2))
