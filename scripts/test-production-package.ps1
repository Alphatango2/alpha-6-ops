[CmdletBinding()]
param(
    [string]$DotNetRoot = (Join-Path $PSScriptRoot '../work/dotnet'),
    [string]$WorkDirectory = (Join-Path $PSScriptRoot '../work/checkpoint-7.26b-package-contract'),
    [switch]$SkipBuild
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$work=[IO.Path]::GetFullPath($WorkDirectory)
if(-not $work.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package test work directory must be inside the repository.'}
if(Test-Path -LiteralPath $work){Remove-Item -LiteralPath $work -Recurse -Force}
New-Item -ItemType Directory -Path $work -Force|Out-Null

function Require([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
function Inventory([string]$Root){
    @(Get-ChildItem -LiteralPath $Root -File -Recurse|ForEach-Object{
        [pscustomobject]@{path=[IO.Path]::GetRelativePath($Root,$_.FullName).Replace('\','/');bytes=[int64]$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    }|Sort-Object path)
}
function Fingerprint([object[]]$Rows){
    $text=($Rows|ForEach-Object{"$($_.path)|$($_.bytes)|$($_.sha256)"}) -join "`n"
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))
}

$project=Get-Content -LiteralPath (Join-Path $repo 'src/Alpha6Ops.Desktop/Alpha6Ops.Desktop.csproj') -Raw
$setup=Get-Content -LiteralPath (Join-Path $repo 'packaging/PreviewSetup.cs') -Raw
$build=Get-Content -LiteralPath (Join-Path $repo 'packaging/build-desktop.ps1') -Raw
Require ($project -match '<AssemblyName>Sierra4</AssemblyName>') 'Desktop assembly/executable identity is not Sierra4.'
Require ($project -match '<Version>0\.23\.0</Version>') 'Desktop version is not 0.23.0.'
Require ($project -match '<Product>Sierra 4</Product>') 'Desktop product metadata is not Sierra 4.'
Require ($setup -match '"Programs", "Sierra 4"') 'Installer does not target %LOCALAPPDATA%\Programs\Sierra 4.'
Require ($setup -match 'Sierra4-Desktop-0\.23\.0' -and $setup -match '\.sierra4') 'Installer identity/ownership marker is not Sierra 4 v0.23.0.'
Require ($setup -match 'Uninstall\\Sierra4' -and $setup -match 'Sierra 4\.lnk') 'Registry or shortcut identity is not Sierra 4.'
Require ($build -match 'IncludeDiagnosticHarnesses=false') 'Production build does not explicitly exclude diagnostic harnesses.'

if(-not $SkipBuild){& (Join-Path $repo 'packaging/build-desktop.ps1') -DotNetRoot $DotNetRoot;if($LASTEXITCODE){throw 'Production package build failed.'}}
$publish=Join-Path $repo 'outputs/Sierra4-Desktop-0.23.0'
$zip=Join-Path $repo 'outputs/Sierra4-Desktop-0.23.0-win-x64.zip'
$installer=Join-Path $repo 'outputs/Sierra4-Setup-0.23.0.exe'
Require (Test-Path -LiteralPath $publish -PathType Container) 'Sierra 4 publish directory is missing.'
Require (Test-Path -LiteralPath $zip -PathType Leaf) 'Sierra 4 portable ZIP is missing.'
Require (Test-Path -LiteralPath $installer -PathType Leaf) 'Sierra 4 installer is missing.'
Require (Test-Path -LiteralPath (Join-Path $publish 'Sierra4.exe')) 'Sierra4.exe is missing.'
Require (Test-Path -LiteralPath (Join-Path $publish 'Sierra4.dll')) 'Sierra4.dll is missing.'
Require (-not (Test-Path -LiteralPath (Join-Path $publish 'Alpha6OPS.exe'))) 'Legacy executable leaked into the Sierra 4 payload.'
Require (@(Get-ChildItem -LiteralPath $publish -Recurse -File -Include '*.pdb','*.cs','*.jsonl','*summary*','*evidence*').Count -eq 0) 'Source, symbols, tests, or evidence leaked into the payload.'

$assemblyBytes=[IO.File]::ReadAllBytes((Join-Path $publish 'Sierra4.dll'))
$assemblyText=[Text.Encoding]::UTF8.GetString($assemblyBytes)+[Text.Encoding]::Unicode.GetString($assemblyBytes)
foreach($command in @('--smoke-test','--map-performance-checks','--map-v2-checks','--replay-journals','--observed-cruise-native-checks','--activation-smoke','--simconnect-probe','--trace-map-input')){
    Require (-not $assemblyText.Contains($command,[StringComparison]::Ordinal)) "Production assembly contains diagnostic command $command."
}

$fromZip=Join-Path $work 'zip';Expand-Archive -LiteralPath $zip -DestinationPath $fromZip
$fromInstaller=Join-Path $work 'installer';$process=Start-Process -FilePath $installer -ArgumentList @('--extract-test',$fromInstaller) -Wait -PassThru -WindowStyle Hidden
Require ($process.ExitCode -eq 0) 'Installer extraction test failed.'
$publishInventory=Inventory $publish;$zipInventory=Inventory $fromZip;$installerInventory=Inventory $fromInstaller
Require ((Fingerprint $publishInventory) -eq (Fingerprint $zipInventory)) 'Portable ZIP differs from the publish payload.'
Require ((Fingerprint $publishInventory) -eq (Fingerprint $installerInventory)) 'Embedded installer payload differs from the publish payload.'

$upgrade=Join-Path $work 'upgrade';Copy-Item -LiteralPath $fromInstaller -Destination $upgrade -Recurse
Set-Content -LiteralPath (Join-Path $upgrade '.sierra4') -Value 'Sierra4-Desktop-0.23.0' -NoNewline
Set-Content -LiteralPath (Join-Path $upgrade 'obsolete-seeded-file.txt') -Value 'remove me'
$externalProfile=Join-Path $work 'external-profile';New-Item -ItemType Directory -Path $externalProfile|Out-Null;Set-Content -LiteralPath (Join-Path $externalProfile 'preserve.txt') -Value 'preserve'
$process=Start-Process -FilePath $installer -ArgumentList @('--upgrade-test',$upgrade) -Wait -PassThru -WindowStyle Hidden
Require ($process.ExitCode -eq 0) 'Same-channel replacement test failed.'
Require (-not (Test-Path -LiteralPath (Join-Path $upgrade 'obsolete-seeded-file.txt'))) 'Same-channel replacement retained an obsolete payload file.'
Require ((Get-Content -LiteralPath (Join-Path $externalProfile 'preserve.txt') -Raw).Trim() -eq 'preserve') 'Package replacement modified external profile data.'
$upgradePayload=@(Inventory $upgrade|Where-Object path -ne '.sierra4')
Require ((Fingerprint $publishInventory) -eq (Fingerprint $upgradePayload)) 'Replacement payload differs from clean publish content.'

$dev=Join-Path $work 'development';$dotnet=Join-Path $DotNetRoot 'dotnet.exe'
& $dotnet publish (Join-Path $repo 'src/Alpha6Ops.Desktop') -c Release --no-restore --no-self-contained -p:IncludeDiagnosticHarnesses=true -p:DebugType=None -p:DebugSymbols=false -o $dev
if($LASTEXITCODE){throw 'Development harness build failed.'}
$devBytes=[IO.File]::ReadAllBytes((Join-Path $dev 'Sierra4.dll'));$devText=[Text.Encoding]::UTF8.GetString($devBytes)+[Text.Encoding]::Unicode.GetString($devBytes)
Require ($devText.Contains('--smoke-test',[StringComparison]::Ordinal)) 'Development build no longer contains the required diagnostic harnesses.'

$report=[ordered]@{
    schema='sierra4.production-package-contract.v1';passed=$true;testedAt=(Get-Date).ToString('o');version='0.23.0'
    publish=[ordered]@{path=$publish;files=$publishInventory.Count;bytes=[int64](($publishInventory|Measure-Object bytes -Sum).Sum);fingerprint=Fingerprint $publishInventory}
    zip=[ordered]@{path=$zip;sha256=(Get-FileHash $zip -Algorithm SHA256).Hash;files=$zipInventory.Count;fingerprint=Fingerprint $zipInventory}
    installer=[ordered]@{path=$installer;sha256=(Get-FileHash $installer -Algorithm SHA256).Hash;files=$installerInventory.Count;fingerprint=Fingerprint $installerInventory}
    identity=[ordered]@{product='Sierra 4';executable='Sierra4.exe';installPath='%LOCALAPPDATA%\Programs\Sierra 4';marker='.sierra4';registry='HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Sierra4';shortcut='Sierra 4.lnk'}
    diagnostics=[ordered]@{productionCommandsAbsent=$true;developmentHarnessesPresent=$true}
    replacement=[ordered]@{obsoleteFileRemoved=$true;externalProfilePreserved=$true}
}
$report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $work 'package-contract.json') -Encoding utf8
$report|ConvertTo-Json -Depth 5
