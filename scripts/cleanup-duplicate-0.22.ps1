$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')
$candidate=Join-Path $repo 'outputs/Alpha6OPS-Desktop-S4-7.25A-A350-20261001-6020896a402b'
$targets=@(
    @{path='outputs/Alpha6OPS-Desktop-0.22.0';kind='directory';candidate='portable'},
    @{path='outputs/Alpha6OPS-Desktop-0.22.0-win-x64.zip';kind='file';candidate='Alpha6OPS-Desktop-0.22.0-7.25A-A350-ENDURANCE-win-x64.zip'},
    @{path='outputs/Alpha6OPS-Setup-0.22.0.exe';kind='file';candidate='Alpha6OPS-Setup-0.22.0-7.25A-A350-ENDURANCE.exe'}
)
function Inventory([string]$root){
    @(Get-ChildItem -LiteralPath $root -File -Recurse|ForEach-Object{
        [pscustomobject]@{path=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}
    }|Sort-Object path)
}
$removed=@()
foreach($entry in $targets){
    $source=[IO.Path]::GetFullPath((Join-Path $repo $entry.path))
    if(-not $source.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Target outside repository: $source"}
    $retained=[IO.Path]::GetFullPath((Join-Path $candidate $entry.candidate))
    if($entry.kind -eq 'file'){
        if((Get-FileHash $source -Algorithm SHA256).Hash -ne (Get-FileHash $retained -Algorithm SHA256).Hash){throw "Duplicate hash mismatch: $($entry.path)"}
        $files=1;$bytes=(Get-Item $source).Length
    }else{
        $left=Inventory $source;$right=Inventory $retained
        if(($left|ConvertTo-Json -Compress) -ne ($right|ConvertTo-Json -Compress)){throw "Duplicate payload mismatch: $($entry.path)"}
        $files=$left.Count;$bytes=[int64](($left|Measure-Object bytes -Sum).Sum)
    }
    Remove-Item -LiteralPath $source -Recurse -Force
    if(Test-Path -LiteralPath $source){throw "Duplicate removal failed: $source"}
    $removed += [pscustomobject]@{path=$entry.path;files=$files;bytes=$bytes;retainedCopy=[IO.Path]::GetRelativePath($repo,$retained).Replace('\','/')}
}
$ledgerPath=Join-Path $repo 'docs/checkpoint-7.26b/cleanup-ledger.json'
$ledger=Get-Content -LiteralPath $ledgerPath -Raw|ConvertFrom-Json
$ledger|Add-Member -NotePropertyName postPackagingCleanup -NotePropertyValue ([pscustomobject]@{
    completedAt=(Get-Date).ToString('o');verification='Generic 0.22 payload, ZIP and installer were byte-identical to the retained 7.25A frozen candidate before removal.';removed=$removed
}) -Force
$ledger|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $ledgerPath -Encoding utf8
$removed|ConvertTo-Json
