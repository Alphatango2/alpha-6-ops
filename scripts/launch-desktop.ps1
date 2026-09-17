$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$launchOutput = Join-Path $workspaceRoot ('work\desktop-launcher\' + $runName)
$launchLog = Join-Path $launchOutput 'build.log'
try {
    New-Item -ItemType Directory -Path $launchOutput -Force | Out-Null
    Push-Location $workspaceRoot
    try {
        # Separate build outputs allow the shortcut to open login while an older app is running.
        & dotnet build src/Alpha6Ops.Desktop/Alpha6Ops.Desktop.csproj --output $launchOutput *> $launchLog
        if ($LASTEXITCODE -ne 0) { throw ('Build failed. Details: ' + $launchLog) }
    }
    finally { Pop-Location }
    $launchExe = Join-Path $launchOutput 'Alpha6OPS.exe'
    $launchProcess = Start-Process -FilePath $launchExe -ArgumentList '--preview-login' -WorkingDirectory $launchOutput -PassThru
    if ($launchProcess.WaitForExit(2500)) { throw ('The app exited during startup. Build: ' + $launchOutput) }
    [pscustomobject]@{ ProcessId = $launchProcess.Id; Executable = $launchExe; Arguments = '--preview-login' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $launchOutput 'launch.json') -Encoding UTF8
}
catch {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($_.Exception.Message, 'Alpha 6 OPS could not start', 'OK', 'Error') | Out-Null
    exit 1
}
