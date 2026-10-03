#requires -Version 7.4
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$env:NG_CLI_ANALYTICS = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath $taskRoot
try {
    foreach ($taskFolder in @('.', 'frontend', 'tooling/contracts')) {
        npm --prefix $taskFolder ci --no-fund --no-audit
        if ($LASTEXITCODE -ne 0) { throw "Dependency restore failed: $taskFolder" }
    }
    dotnet restore backend/tests/AiNexus.Tests/AiNexus.Tests.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Backend/test dependency restore failed.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Local .NET tools restore failed.' }
    Write-Output 'Locked dependencies and EF tools restored.'
} finally { Pop-Location }
