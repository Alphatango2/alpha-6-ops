import gzip,hashlib,json,pathlib,shutil
RUN=pathlib.Path(__file__).resolve().parent
ROOT=RUN.parents[2]
TARGET=ROOT/'docs/flight-tracking/evidence/7.24c-direct-responsiveness-20260929'
if TARGET.exists():raise SystemExit('Evidence destination already exists; do not overwrite it')
TARGET.mkdir(parents=True)
sources=[]
def copy(source,relative):
    original=relative
    compressed=source.name.endswith('-trace.json')
    if compressed:relative=pathlib.Path(str(relative)+'.gz')
    target=TARGET/relative;target.parent.mkdir(parents=True,exist_ok=True)
    if compressed:
        with source.open('rb') as src,gzip.open(target,'wb',compresslevel=6) as dst:shutil.copyfileobj(src,dst)
    else:shutil.copy2(source,target)
    with source.open('rb') as src:digest=hashlib.file_digest(src,'sha256').hexdigest().upper()
    sources.append({'source':str(source),'copy':str(relative),'rawSha256':digest,'gzip':compressed})
for name in ('request.txt','progress.md','acceptance-input.json','acceptance.json','metrics.json','calibration.json','preservation.json',
             'policy-red.log','policy-green.log','calibration-red.log','calibration-green.log','policy-final.log','review-policy-green.log',
             'build-1.log','build-2.log','build-3.log','build-4.log','review-red-build.log','review-green-build.log',
             'run-verification.ps1','summarize.py','preserve.py','archive.py','verification.log','final-baseline.log','review.md'):
    copy(RUN/name,pathlib.Path(name))
for folder in ('direct-1','direct-2','direct-3','direct-contracts','review-red','review-green','raw-move-full-1','contracts','interaction-contract','restoration-verify','scheduling-baseline','scheduling-contract','terminal','active-restart','verify','jst776-replay','jst771-replay'):
    for f in (RUN/folder).glob('*.json'):
        # Detailed top-level results include trace and browser observations. Isolated
        # runtime/profile/browser caches remain at their original raw paths.
        copy(f,pathlib.Path(folder)/f.name)
baseline=pathlib.Path((RUN/'baseline-path-final.txt').read_text(encoding='utf-8-sig').strip())
for name in ('summary.json','build-hashes.json','build.log','package-build.log','input-hashes.json','fleet-import.log'):
    copy(baseline/name,pathlib.Path('full-baseline')/name)
copy(RUN/'jst771-evidence.json',pathlib.Path('jst771-evidence.json'))
(TARGET/'raw-sources.json').write_text(json.dumps(sources,indent=2),encoding='utf-8')
manifest=[]
for f in sorted(TARGET.rglob('*')):
    if f.is_file():manifest.append({'path':str(f.relative_to(TARGET)),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest().upper()})
(TARGET/'hashes.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({'directory':str(TARGET),'filesHashed':len(manifest),'bytes':sum(r['bytes'] for r in manifest)}))
