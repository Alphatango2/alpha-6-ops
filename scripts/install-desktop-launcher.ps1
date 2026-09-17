$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$launcherScript = Join-Path $PSScriptRoot 'launch-desktop.ps1'
$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Alpha 6 OPS - Login.lnk'
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcut = $shortcutShell.CreateShortcut($shortcutPath)
if ((Test-Path -LiteralPath $shortcutPath) -and $shortcut.Arguments -notlike '*launch-desktop.ps1*') {
    throw ('A different shortcut already exists at ' + $shortcutPath + '. It was not overwritten.')
}
$shortcut.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $launcherScript + '"'
$shortcut.WorkingDirectory = $workspaceRoot
$shortcut.IconLocation = (Join-Path $workspaceRoot 'src\Alpha6Ops.Desktop\Assets\Alpha6OPS.ico') + ',0'
$shortcut.Description = 'Build the latest Alpha 6 OPS development preview. Always starts at login; choose Flying as a Pilot to continue.'
$shortcut.WindowStyle = 7
$shortcut.Save()
Write-Output ('Created: ' + $shortcutPath)
