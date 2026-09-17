$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$previewOutput = Join-Path $workspaceRoot ('work\pilot-previews\' + $runName)
Push-Location $workspaceRoot
try {
    & dotnet build src/Alpha6Ops.Desktop/Alpha6Ops.Desktop.csproj --output $previewOutput
    if ($LASTEXITCODE -ne 0) { throw 'Pilot preview build failed. See the build output above.' }
    $previewExe = Join-Path $previewOutput 'Alpha6OPS.exe'
    $previewProcess = Start-Process -FilePath $previewExe -ArgumentList '--preview-pilot' -WorkingDirectory $previewOutput -PassThru
    if ($previewProcess.WaitForExit(2500)) { throw 'The pilot preview exited during startup.' }
    Write-Host 'Free Pilot workspace opened. Explore the existing dashboard with Logbook, Dispatch, Tracking and Weather enabled.' -ForegroundColor Green
    Write-Host 'This preview uses a fresh, isolated local folder. It does not sign in or load your existing flights.'
}
finally { Pop-Location }
