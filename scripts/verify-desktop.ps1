param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$appPath = Join-Path $workspaceRoot ("src\Alpha6Ops.Desktop\bin\$Configuration\net10.0-windows\Alpha6OPS.exe")
if (-not (Test-Path -LiteralPath $appPath)) { throw 'Build Alpha6Ops.slnx before running desktop verification.' }
$runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$resultDirectory = Join-Path $workspaceRoot ('work\dashboard-review\' + $runName)
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$appProcess = Start-Process -FilePath $appPath -ArgumentList @('--smoke-test', ('"' + $resultDirectory + '"')) -WindowStyle Hidden -PassThru
if (-not $appProcess.WaitForExit(60000)) {
    Stop-Process -Id $appProcess.Id
    throw ('Desktop verification timed out. Results: ' + $resultDirectory)
}
$failurePath = Join-Path $resultDirectory 'desktop-smoke-error.txt'
if (Test-Path -LiteralPath $failurePath) { throw (Get-Content -LiteralPath $failurePath -Raw) }
$desktopPath = Join-Path $resultDirectory 'desktop-smoke.json'
$dashboardPath = Join-Path $resultDirectory 'dashboard-smoke.json'
if (-not (Test-Path -LiteralPath $desktopPath) -or -not (Test-Path -LiteralPath $dashboardPath)) {
    throw ('Verification did not produce both success reports. Results: ' + $resultDirectory)
}
$desktop = Get-Content -LiteralPath $desktopPath -Raw | ConvertFrom-Json
$dashboard = Get-Content -LiteralPath $dashboardPath -Raw | ConvertFrom-Json
if (-not $desktop.passed -or -not $dashboard.passed) { throw 'Desktop verification failed.' }
$activationProcess = Start-Process -FilePath $appPath -ArgumentList @('--activation-smoke', ('"' + $resultDirectory + '"')) -WindowStyle Hidden -PassThru
if (-not $activationProcess.WaitForExit(15000)) { Stop-Process -Id $activationProcess.Id; throw 'Single-instance activation verification timed out.' }
$activationPath = Join-Path $resultDirectory 'activation-smoke.json'
if (-not (Test-Path -LiteralPath $activationPath)) { throw 'Single-instance activation verification did not produce a report.' }
$activation = Get-Content -LiteralPath $activationPath -Raw | ConvertFrom-Json
if (-not $activation.passed) { throw 'A second launch did not restore the tray-hidden primary window.' }
[pscustomobject]@{ passed = $true; desktopChecks = $desktop.checks.Count; dashboardChecks = $dashboard.count; singleInstanceActivation = $activation.passed; outputDirectory = $resultDirectory } | ConvertTo-Json
