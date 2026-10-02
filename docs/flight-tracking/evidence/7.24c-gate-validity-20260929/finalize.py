import hashlib,json,pathlib,shutil,re

HERE=pathlib.Path(__file__).resolve().parent
REPO=HERE.parents[2]
OLD=REPO/'work/checkpoint-7.24c/heartbeat-review-20260929'
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest().upper()

before=load(HERE/'source-before.json')
changed=[r['Path'] for r in before if not pathlib.Path(r['Path']).exists() or sha(pathlib.Path(r['Path']))!=r['SHA256']]
assert not changed
frozen=load(OLD/'frozen-before.json')
frozen_root=REPO/'outputs/Alpha6OPS-Desktop-S4-7.24C-T2-20260929-f421af15530c'
current={str(p) for p in frozen_root.rglob('*') if p.is_file()}
expected={r['path'] for r in frozen}
fp=dict(files=len(frozen),changed=[r['path'] for r in frozen if not pathlib.Path(r['path']).exists() or sha(pathlib.Path(r['path']))!=r['sha256']],added=sorted(current-expected),removed=sorted(expected-current))
assert fp['files']==524 and not any(fp[k] for k in ['changed','added','removed'])
profile=[]
for r in load(REPO/'work/checkpoint-7.24c/jst771-review-20260929/recovery-review/preservation-manifest.json'):
    if str(pathlib.Path(r['Source']).parent).lower()==r'c:\users\dannibals\appdata\local\alpha6designs\alpha6ops':
        profile.append(dict(path=r['Source'],unchanged=sha(pathlib.Path(r['Source']))==r['SHA256Before']))
assert len(profile)==9 and all(r['unchanged'] for r in profile)
sources=load(HERE/'analysis-sources-v2.json')
assert all(sha(REPO/p).lower()==h.lower() for p,h in sources.items())
prior_root=REPO/'docs/flight-tracking/evidence/7.24c-heartbeat-review-20260929'
prior=load(prior_root/'hashes.json')
assert all(sha(prior_root/r['path'])==r['sha256'] for r in prior)
preservation=dict(sourceFilesChecked=len(before),sourceFilesChanged=changed,frozen=fp,
    profile=profile,analysisInputFilesUnchanged=len(sources),priorEvidenceFilesUnchanged=len(prior),
    productionChanged=False,acceptanceChanged=False,installerLaunched=False)
(HERE/'preservation.json').write_text(json.dumps(preservation,indent=2),encoding='utf-8')
summary=dict(recommendation='HEARTBEAT VALID BUT ACCEPTANCE MODEL IS WRONG',
    classification='Engineering heuristic enforced as hard test; useful scheduling diagnostic, not established product-derived SLA',
    existingHeartbeatThresholdModified=False,productionChangeJustified=False,t2InstallationRecommended=False,
    reason='Approval and direct product-metric coverage required before replacing the current gate; no open-ended OS attribution required to decide validity.',
    newApplicationExecutions=0,regressionRerun=False,priorFailuresPreserved=True,
    correlation='correlation-v2.json',preservation='preservation.json')
(HERE/'review-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
dest=REPO/'docs/flight-tracking/evidence/7.24c-gate-validity-20260929'
dest.mkdir(parents=True,exist_ok=False)
for name in ['request.txt','analyze.py','finalize.py','correlation.json','analysis-sources.json',
             'correlation-v2.json','analysis-sources-v2.json','preservation.json','review-summary.json']:
    shutil.copy2(HERE/name,dest/name)
report=REPO/'docs/flight-tracking/7.24c-responsiveness-gate-validity-review.md'
bad=[]
for target in re.findall(r'\]\(([^)]+)\)',report.read_text(encoding='utf-8')):
    if not target.startswith('https://') and not (report.parent/target).resolve().exists():bad.append(target)
assert not bad,bad
manifest=[dict(path=p.name,bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(dest.iterdir()) if p.is_file()]
(dest/'hashes.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
assert all(sha(dest/r['path'])==r['sha256'] for r in manifest)
print(json.dumps(dict(sourceUnchanged=len(before),frozenUnchanged=len(frozen),profileUnchanged=len(profile),
    analysisInputsUnchanged=len(sources),priorArchiveFilesUnchanged=len(prior),newEvidenceFiles=len(manifest),reportLinksValid=True),indent=2))
