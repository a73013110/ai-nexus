#requires -Version 7.4
param([switch]$SkipBrowser)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskPreviousPublishDirectory = $env:NEXUS_E2E_PUBLISH_DIRECTORY
Push-Location -LiteralPath $taskRoot
try {
    & (Join-Path $PSScriptRoot 'Test-Settings.ps1')
    & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory 'artifacts/verification'
    dotnet test backend/tests/AiNexus.Tests/AiNexus.Tests.csproj --no-restore --filter 'Category!=Performance&Category!=Browser' --logger 'trx;LogFileName=backend.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    npm --prefix frontend test
    if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
    if (!$SkipBrowser) {
        & (Join-Path $PSScriptRoot 'Test-Diagnostics.ps1') -Browser -Performance -SkipBuild
        $env:NEXUS_E2E_PUBLISH_DIRECTORY = 'artifacts/verification'
        npm run test:e2e
        if ($LASTEXITCODE -ne 0) { throw 'Browser tests failed.' }
    }
} finally { $env:NEXUS_E2E_PUBLISH_DIRECTORY = $taskPreviousPublishDirectory; Pop-Location }
