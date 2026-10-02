$ErrorActionPreference='Stop'
$repo=(Get-Location).Path
$run=Join-Path $repo 'work/checkpoint-7.24c/direct-acceptance-20260929'
$env:DOTNET_ROOT=Join-Path $repo 'work/dotnet'
$dotnet=Join-Path $env:DOTNET_ROOT 'dotnet.exe'
$old='work/checkpoint-7.24c/jst771-review-20260929'
$results=@()
function AppCheck([string]$Executable,[string[]]$Arguments,[string]$Name,[int]$Seconds=240){
 $p=Start-Process -FilePath $Executable -ArgumentList @($Arguments|ForEach-Object{'"'+$_+'"'}) -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput ($run+'/'+$Name+'.stdout') -RedirectStandardError ($run+'/'+$Name+'.stderr')
 if(!$p.WaitForExit($Seconds*1000)){throw "$Name diagnostic exceeded existing deadline; owned PID $($p.Id)"};$p.WaitForExit()
 Write-Output "$Name process exit: $($p.ExitCode) (summary is authoritative)"
}
# Predetermined final sequence; no retry-on-failure loop.
& work/checkpoint-7.24c/run-full-baseline.ps1 -DotNetRoot $env:DOTNET_ROOT -ReplayManifest work/checkpoint-7.24c/full-20260929-103356-2cb4f4e5/replay-manifest.json *> ($run+'/full-baseline.log')
$baseline=Get-ChildItem work/checkpoint-7.24c -Directory -Filter 'full-*'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
$baseline.FullName|Set-Content ($run+'/baseline-path.txt')
Write-Output "Full baseline evidence: $($baseline.FullName)"
$app=Join-Path $repo 'outputs/Alpha6OPS-Desktop-0.22.0/Alpha6OPS.exe'
foreach($n in @(1)){
 AppCheck $app @('--map-v2-checks',($run+'/raw-move-full-'+$n)) ('raw-move-full-'+$n)
}
foreach($mode in @('direct-contracts','contracts','interaction-contract','restoration-verify')){
 $minutes=if($mode -eq 'interaction-contract'){'0.2'}else{'0'}
 AppCheck $app @('--map-performance-checks',($run+'/'+$mode),$mode,$minutes) $mode
}
foreach($mode in @('terminal','active-restart','verify')){
 & $dotnet run --project tests/Alpha6Ops.RecoveryChecks -c Release -- ('--'+$mode) ($run+'/'+$mode) *> ($run+'/'+$mode+'.log')
 Write-Output "$mode exit: $LASTEXITCODE"
}
AppCheck $app @('--replay-journals',(Join-Path $repo 'work/checkpoint-7.24c/route/replay-manifest-current.json'),($run+'/jst776-replay')) 'jst776-replay'
AppCheck $app @('--replay-journals',(Join-Path $repo ($old+'/jst771-replay-manifest.json')),($run+'/jst771-replay')) 'jst771-replay'
foreach($arm in @('baseline','contract')){
 & scripts/verify-map-scheduling.ps1 -Arm $arm -Seconds 30 -OutputDirectory ($run+'/scheduling-'+$arm) *> ($run+'/scheduling-'+$arm+'.log')
}
$python=Join-Path ([Environment]::GetFolderPath('UserProfile')) '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$supplement=$old+'/recovery-review/supplemental'
& $python scripts/test-jst771-evidence.py --supplemental-journal ($supplement+'/Alpha6OPS-20260929-132028-8b82910e17c643edb0daaadcaf382fe3.jsonl') --supplemental-journal ($supplement+'/Alpha6OPS-20260929-140712-45a372128d6c48869b91cd6e6a66176e.jsonl') --history-db ($supplement+'/Alpha6OPS-flights.sqlite') --output ($run+'/jst771-evidence.json') *> ($run+'/jst771-evidence.log')
'Completed fixed verification sequence'|Set-Content ($run+'/verification-complete.txt')

