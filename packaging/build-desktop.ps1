param(
    [string]$DotNetRoot = (Join-Path $PSScriptRoot '../work/dotnet'),
    [string]$RuntimeVersion = '10.0.11',
    [string]$SimConnectDll = 'M:\SimConnect SDK\lib\SimConnect.dll'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publish = Join-Path $repo 'outputs/Sierra4-Desktop-0.23.0'
$dotnet = Join-Path $DotNetRoot 'dotnet.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$setupSource = Join-Path $PSScriptRoot 'PreviewSetup.cs'
$icon = Join-Path $repo 'src/Alpha6Ops.Desktop/Assets/Alpha6OPS.ico'
$logo = Join-Path $repo 'src/Alpha6Ops.Desktop/Assets/Dashboard/alpha6-ops-logo.png'
$uninstaller = Join-Path $publish 'Uninstall.exe'
$installer = Join-Path $repo 'outputs/Sierra4-Setup-0.23.0.exe'
$archive = Join-Path $repo 'outputs/Sierra4-Desktop-0.23.0-win-x64.zip'
$previousCliHome = $env:DOTNET_CLI_HOME
$env:DOTNET_CLI_HOME = Join-Path $repo 'work/dotnet-home'
Push-Location $repo
try {
    foreach($generated in @($publish,$installer,$archive)) {
        $full=[IO.Path]::GetFullPath($generated)
        if(-not $full.StartsWith((Join-Path $repo 'outputs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Generated package path escaped outputs: $full"}
        if(Test-Path -LiteralPath $full){Remove-Item -LiteralPath $full -Recurse -Force}
    }
    & $dotnet restore src/Alpha6Ops.Desktop --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }
    & $dotnet publish src/Alpha6Ops.Desktop -c Release --no-restore --no-self-contained -p:IncludeDiagnosticHarnesses=false -p:AppHostDotNetSearch=AppRelative -p:AppHostRelativeDotNet=runtime -p:DebugType=None -p:DebugSymbols=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }
    foreach ($relative in @("host/fxr/$RuntimeVersion", "shared/Microsoft.NETCore.App/$RuntimeVersion", "shared/Microsoft.WindowsDesktop.App/$RuntimeVersion")) {
        $source = Join-Path $DotNetRoot $relative
        if (!(Test-Path -LiteralPath $source)) { throw "Runtime folder missing: $source" }
        $destination = Join-Path $publish "runtime/$relative"
        New-Item -ItemType Directory -Force $destination | Out-Null
        Get-ChildItem -LiteralPath $source | Copy-Item -Destination $destination -Recurse -Force
    }
    Copy-Item -LiteralPath (Join-Path $DotNetRoot 'LICENSE.txt'),(Join-Path $DotNetRoot 'ThirdPartyNotices.txt') -Destination (Join-Path $publish 'runtime') -Force
    Copy-Item -LiteralPath 'packaging/Desktop-Quick-Start.txt' -Destination (Join-Path $publish 'Read me.txt') -Force
    if (Test-Path -LiteralPath $SimConnectDll -PathType Leaf) {
        Set-Content -LiteralPath (Join-Path $publish 'simconnect-sdk-path.txt') -Value ([IO.Path]::GetFullPath($SimConnectDll)) -NoNewline
    } else {
        Write-Warning "SimConnect SDK DLL was not found at '$SimConnectDll'. OPS will use environment/config/drive discovery at runtime."
    }
    & $compiler /nologo /target:winexe /platform:x64 /define:UNINSTALLER "/win32icon:$icon" "/out:$uninstaller" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll "/resource:$logo,setup-logo.png" $setupSource
    if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed.' }
    # Files copied for portable use do not register anything in Windows.
    Compress-Archive -Path "$publish/*" -DestinationPath $archive -Force
    & $compiler /nologo /target:winexe /platform:x64 "/win32icon:$icon" "/out:$installer" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll "/resource:$logo,setup-logo.png" "/resource:$archive,payload.zip" $setupSource
    if ($LASTEXITCODE -ne 0) { throw 'Setup build failed.' }
    Get-FileHash $installer,$archive | Format-Table -AutoSize
} finally { $env:DOTNET_CLI_HOME = $previousCliHome; Pop-Location }
