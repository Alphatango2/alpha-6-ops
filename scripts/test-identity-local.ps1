param([switch]$LaunchOnly, [switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$runDirectory = Join-Path $workspaceRoot ('work\identity-runs\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$transcriptPath = Join-Path $runDirectory 'console.txt'
Start-Transcript -Path $transcriptPath | Out-Null
$startedDatabase = $false
$previousAccountsDatabase = $env:ALPHA6_TEST_DATABASE
$previousServerDatabase = $env:ALPHA6_SERVER_TEST_DATABASE
$failed = $false
Push-Location $workspaceRoot
try {
    Write-Host ('Diagnostic log: ' + $transcriptPath)
    $appPath = Join-Path $workspaceRoot 'src\Alpha6Ops.Desktop\bin\Debug\net10.0-windows\Alpha6OPS.exe'
    if (-not $LaunchOnly) {
        if (Get-Process -Name Alpha6OPS -ErrorAction SilentlyContinue) {
            throw 'Alpha 6 OPS is running. Use Exit OPS (including the tray icon), then run this command again. Tests cannot verify single-instance startup while another copy is running.'
        }
        Get-Command dotnet, node -ErrorAction Stop | Out-Null
        $pgCtl = Join-Path $env:ProgramFiles 'PostgreSQL\17\bin\pg_ctl.exe'
        $testData = Join-Path $workspaceRoot 'work\identity-postgres'
        if (-not (Test-Path -LiteralPath $pgCtl) -or -not (Test-Path -LiteralPath (Join-Path $testData 'PG_VERSION'))) {
            throw 'The prepared local PostgreSQL 17 test installation is missing. This launcher uses the existing work\identity-postgres cluster.'
        }
        $pgLog = Join-Path $testData 'server.log'
        & $pgCtl -D $testData status
        if ($LASTEXITCODE -ne 0) {
            Write-Host 'Starting the local test database on 127.0.0.1:55439...'
            $pgStart = Start-Process -FilePath $pgCtl -ArgumentList @(
                '-D', ('"' + $testData + '"'), '-l', ('"' + $pgLog + '"'),
                '-o', '"-h 127.0.0.1 -p 55439"', '-w', 'start'
            ) -WindowStyle Hidden -Wait -PassThru
            # Query the server itself: a missing Process.ExitCode must not imply startup failure.
            & $pgCtl -D $testData status
            if ($LASTEXITCODE -ne 0) { throw ('Test database startup failed. Database log: ' + $pgLog) }
            $startedDatabase = $true
        }
        $env:ALPHA6_TEST_DATABASE = 'Host=127.0.0.1;Port=55439;Username=alpha6_test;Database=alpha6_identity_test'
        $env:ALPHA6_SERVER_TEST_DATABASE = 'Host=127.0.0.1;Port=55439;Username=alpha6_test;Database=alpha6_server_test'
        & (Join-Path $PSScriptRoot 'verify-identity.ps1') -SkipBuild:$SkipBuild
        Write-Host 'All local identity tests passed.' -ForegroundColor Green
    }
    if (-not (Test-Path -LiteralPath $appPath)) {
        throw 'The desktop executable is missing. Run this launcher without -LaunchOnly to build it first.'
    }
    Write-Host 'Opening Alpha 6 OPS...'
    $desktopProcess = Start-Process -FilePath $appPath -WorkingDirectory (Split-Path -Parent $appPath) -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 250
        $desktopProcess.Refresh()
    } while (-not $desktopProcess.HasExited -and $desktopProcess.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
    if ($desktopProcess.HasExited) {
        $existing = Get-Process -Name Alpha6OPS -ErrorAction SilentlyContinue
        if (-not $existing) {
            throw ('Desktop exited during startup. Exit code: ' + $desktopProcess.ExitCode + '. Check %LOCALAPPDATA%\Alpha6Designs\Alpha6OPS\CrashReports.')
        }
        Write-Host 'A running copy received the request to restore its window.'
    }
    elseif ($desktopProcess.MainWindowHandle -eq 0) {
        throw ('Desktop process ' + $desktopProcess.Id + ' started but did not create a window within 15 seconds. It has been left running for diagnosis.')
    }
    else { Write-Host ('Desktop window opened: ' + $desktopProcess.MainWindowTitle) -ForegroundColor Green }
    if (-not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $appPath) 'alpha6-identity.json'))) {
        Write-Host 'This build opens Local Preview. Live account login requires Auth0 configuration.'
    }
}
catch {
    $failed = $true
    Write-Host ('FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
    Write-Host ('Full output saved to: ' + $transcriptPath)
}
finally {
    if ($startedDatabase) {
        & $pgCtl -D $testData -m fast -w stop
        if ($LASTEXITCODE -ne 0) { Write-Warning ('Could not stop the test database. Data directory: ' + $testData) }
    }
    $env:ALPHA6_TEST_DATABASE = $previousAccountsDatabase
    $env:ALPHA6_SERVER_TEST_DATABASE = $previousServerDatabase
    Pop-Location
    Stop-Transcript | Out-Null
}
if ($failed) { exit 1 }
