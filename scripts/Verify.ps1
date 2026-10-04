#requires -Version 7.4
param([switch]$SkipBrowser)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location -LiteralPath $taskRoot
try {
    & (Join-Path $PSScriptRoot 'Test-Settings.ps1')
    & (Join-Path $PSScriptRoot 'Build.ps1')
    dotnet test backend/tests/AiNexus.Tests/AiNexus.Tests.csproj --no-restore --logger 'trx;LogFileName=backend.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    npm --prefix frontend test
    if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
    if (!$SkipBrowser) {
        npm run test:e2e
        if ($LASTEXITCODE -ne 0) { throw 'Browser tests failed.' }
    }
} finally { Pop-Location }
