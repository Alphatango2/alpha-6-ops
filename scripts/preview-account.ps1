$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
# Separate outputs let this preview run alongside the installed app or an older preview.
$runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$previewOutput = Join-Path $workspaceRoot ('work\account-previews\' + $runName)
Push-Location $workspaceRoot
try {
    & dotnet build src/Alpha6Ops.Desktop/Alpha6Ops.Desktop.csproj --output $previewOutput
    if ($LASTEXITCODE -ne 0) { throw 'Account preview build failed. See the build output above.' }
    $previewExe = Join-Path $previewOutput 'Alpha6OPS.exe'
    $previewProcess = Start-Process -FilePath $previewExe -ArgumentList '--preview-login' -WorkingDirectory $previewOutput -PassThru
    if ($previewProcess.WaitForExit(2500)) { throw 'The account preview exited during startup.' }
    Write-Host 'Account portal opened. Click Preview your workspaces to explore the second screen.' -ForegroundColor Green
    Write-Host 'This preview uses sample memberships and does not sign in to an account.'
}
finally { Pop-Location }
