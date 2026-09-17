param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug', [switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($env:ALPHA6_TEST_DATABASE) -or [string]::IsNullOrWhiteSpace($env:ALPHA6_SERVER_TEST_DATABASE)) {
    throw 'Set ALPHA6_TEST_DATABASE and ALPHA6_SERVER_TEST_DATABASE to dedicated local disposable PostgreSQL databases. Full verification must not silently skip database checks.'
}
Push-Location $workspaceRoot
try {
    if (-not $SkipBuild) {
        & dotnet build Alpha6Ops.slnx --configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'Desktop solution build failed.' }
        & dotnet build Alpha6Ops.Cloud.slnx --configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'Cloud solution build failed.' }
    }
    foreach ($project in @('Alpha6Ops.Tests', 'Alpha6Ops.Accounts.Tests', 'Alpha6Ops.Server.Tests', 'Alpha6Ops.Desktop.Identity.Tests')) {
        & dotnet run --project ('tests/' + $project) --configuration $Configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw ($project + ' failed.') }
    }
    & node tests/Alpha6Ops.Server.Tests/auth0-actions.test.cjs
    if ($LASTEXITCODE -ne 0) { throw 'Auth0 Action unit checks failed.' }
    & (Join-Path $PSScriptRoot 'verify-desktop.ps1') -Configuration $Configuration
    Write-Output 'Local identity verification passed. Real Auth0 and hosted deployment acceptance remain separate.'
}
finally { Pop-Location }
