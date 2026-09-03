param(
    [string]$DotNetRoot = (Join-Path $PSScriptRoot '../work/dotnet'),
    [string]$RuntimeVersion = '10.0.11'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publish = Join-Path $repo 'outputs/Alpha6OPS-Desktop'
$dotnet = Join-Path $DotNetRoot 'dotnet.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$setupSource = Join-Path $PSScriptRoot 'PreviewSetup.cs'
$uninstaller = Join-Path $publish 'Uninstall.exe'
$installer = Join-Path $repo 'outputs/Alpha6OPS-Setup-0.9.exe'
$archive = Join-Path $repo 'outputs/Alpha6OPS-Desktop-0.9-win-x64.zip'
$previousCliHome = $env:DOTNET_CLI_HOME
$env:DOTNET_CLI_HOME = Join-Path $repo 'work/dotnet-home'
Push-Location $repo
try {
    & $dotnet restore src/Alpha6Ops.Desktop --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }
    & $dotnet publish src/Alpha6Ops.Desktop -c Release --no-restore --no-self-contained -p:AppHostDotNetSearch=AppRelative -p:AppHostRelativeDotNet=runtime -p:DebugType=None -p:DebugSymbols=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }
    foreach ($relative in @("host/fxr/$RuntimeVersion", "shared/Microsoft.NETCore.App/$RuntimeVersion", "shared/Microsoft.WindowsDesktop.App/$RuntimeVersion")) {
        $source = Join-Path $DotNetRoot $relative
        if (!(Test-Path -LiteralPath $source)) { throw "Runtime folder missing: $source" }
        $destination = Join-Path $publish "runtime/$relative"
        New-Item -ItemType Directory -Force $destination | Out-Null
        Get-ChildItem -LiteralPath $source | Copy-Item -Destination $destination -Recurse -Force
    }
    Copy-Item -LiteralPath (Join-Path $DotNetRoot 'LICENSE.txt'),(Join-Path $DotNetRoot 'ThirdPartyNotices.txt') -Destination (Join-Path $publish 'runtime') -Force
    Copy-Item -LiteralPath 'outputs/Desktop-Quick-Start.txt' -Destination (Join-Path $publish 'Read me.txt') -Force
    & $compiler /nologo /target:winexe /platform:x64 /define:UNINSTALLER "/out:$uninstaller" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll $setupSource
    if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed.' }
    # Files copied for portable use do not register anything in Windows.
    Compress-Archive -Path "$publish/*" -DestinationPath outputs/Alpha6OPS-Desktop-0.9-win-x64.zip -Force
    & $compiler /nologo /target:winexe /platform:x64 "/out:$installer" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll "/resource:$archive,payload.zip" $setupSource
    if ($LASTEXITCODE -ne 0) { throw 'Setup build failed.' }
    Get-FileHash outputs/Alpha6OPS-Setup-0.9.exe,outputs/Alpha6OPS-Desktop-0.9-win-x64.zip | Format-Table -AutoSize
} finally { $env:DOTNET_CLI_HOME = $previousCliHome; Pop-Location }
