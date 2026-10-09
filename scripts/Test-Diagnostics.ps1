#requires -Version 7.4
# -BrowserTarget: msedge, chrome, chromium, or an absolute browser executable path (Linux: /opt/pw-browsers/chromium).
param([switch]$Browser, [string]$BrowserTarget = 'msedge', [switch]$Performance, [switch]$SkipBuild, [ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskBrowserPrevious = $env:AINEXUS_TEST_BROWSER
$taskWebRootPrevious = $env:NEXUS_DIAGNOSTIC_WEBROOT
Push-Location -LiteralPath $taskRoot
try {
    & (Join-Path $PSScriptRoot 'Test-Settings.ps1')
    if ($Browser -and !$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory 'artifacts/verification' }
    dotnet test --project backend/tests/AiNexus.Tests/AiNexus.Tests.csproj --no-restore -c $Configuration --filter 'FullyQualifiedName~DiagnosticTests|FullyQualifiedName~DiagnosticFailureTests' --report-xunit-trx --report-xunit-trx-filename diagnostics.trx --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostics tests failed.' }
    if ($Browser) {
        $env:AINEXUS_TEST_BROWSER = $BrowserTarget
        $env:NEXUS_DIAGNOSTIC_WEBROOT = Join-Path $taskRoot 'artifacts/verification/wwwroot'
        dotnet test --project backend/tests/AiNexus.Tests/AiNexus.Tests.csproj --no-build -c $Configuration --filter 'Category=Browser' --report-xunit-trx --report-xunit-trx-filename diagnostic-browser.trx --results-directory artifacts/test-results
        if ($LASTEXITCODE -ne 0) { throw 'Real browser tests failed.' }
    }
    if ($Performance) {
        dotnet test --project backend/tests/AiNexus.Tests/AiNexus.Tests.csproj --no-build -c $Configuration --filter 'Category=Performance' --report-xunit-trx --report-xunit-trx-filename diagnostic-performance.trx --results-directory artifacts/test-results
        if ($LASTEXITCODE -ne 0) { throw 'Diagnostics performance measurement failed.' }
    }
} finally { $env:AINEXUS_TEST_BROWSER = $taskBrowserPrevious; $env:NEXUS_DIAGNOSTIC_WEBROOT = $taskWebRootPrevious; Pop-Location }
