#requires -Version 7.4

<#
.SYNOPSIS
送 PR 前的完整本機驗證。

.DESCRIPTION
依序執行：腳本測試（Pester，scripts/tests）、Build.ps1（輸出到 artifacts/verification，不覆寫正在執行的 publish）、
後端 build、EF 模型與 migration 一致性、後端單元／整合／架構測試、前端 lint 與單元測試。
真實瀏覽器、SQL Server 與效能測試需另外加參數。
TRX 寫在 artifacts/test-results。需先執行 Restore.ps1；.github/workflows/ci.yml 也是 Restore.ps1 加上這支腳本。

.PARAMETER Browser
加跑後端真實瀏覽器測試（Category=Browser：PDF 匯出、診斷與監控頁）與 Playwright e2e。

.PARAMETER BrowserTarget
後端瀏覽器測試用的瀏覽器：msedge、chrome，或執行檔絕對路徑（Linux：/opt/pw-browsers/chromium）。

.PARAMETER SqlServer
加跑真實 SQL Server 2025 測試（Category=SqlServer：全文／向量檢索、診斷批次寫入），需先設定 AINEXUS_SQLSERVER_TEST 連線字串。

.PARAMETER Performance
加跑隔離的效能量測（Category=Performance），結果寫在 artifacts/。

.EXAMPLE
./scripts/Verify.ps1

.EXAMPLE
./scripts/Verify.ps1 -Browser -Performance

.EXAMPLE
$env:AINEXUS_SQLSERVER_TEST = 'Server=localhost;Integrated Security=True;TrustServerCertificate=True'; ./scripts/Verify.ps1 -SqlServer
#>
[CmdletBinding()]
param([switch]$Browser, [string]$BrowserTarget = 'msedge', [switch]$SqlServer, [switch]$Performance)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($SqlServer -and -not $env:AINEXUS_SQLSERVER_TEST) { throw '-SqlServer 需要先設定 AINEXUS_SQLSERVER_TEST（測試用 SQL Server 連線字串）。' }
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-NexusRoot
$verification = 'artifacts/verification'
$results = 'artifacts/test-results'

function Invoke-BackendTests([string]$Filter, [string]$Report) {
    dotnet test --solution backend/AiNexus.slnx --no-build -c Release --filter $Filter --report-xunit-trx --report-xunit-trx-filename $Report --results-directory $results
    if ($LASTEXITCODE -ne 0) { throw "後端測試失敗：$Filter" }
}

$previous = @{ Browser = $env:AINEXUS_TEST_BROWSER; WebRoot = $env:NEXUS_DIAGNOSTIC_WEBROOT; Publish = $env:NEXUS_E2E_PUBLISH_DIRECTORY }
Push-Location -LiteralPath $root
try {
    Import-NexusPester
    $pester = New-PesterConfiguration
    $pester.Run.Path = Join-Path $PSScriptRoot 'tests'
    $pester.Run.PassThru = $true
    $pester.Output.Verbosity = 'Normal'
    if ((Invoke-Pester -Configuration $pester).Result -ne 'Passed') { throw '腳本測試失敗。' }

    & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $verification
    dotnet build backend/AiNexus.slnx --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw '後端 build 失敗。' }
    dotnet ef migrations has-pending-model-changes --no-build --configuration Release --project backend/src/AiNexus.Features --startup-project backend/src/AiNexus.Host
    if ($LASTEXITCODE -ne 0) { throw 'EF 模型有尚未產生的 migration。' }
    Invoke-BackendTests 'Category!=Performance&Category!=Browser&Category!=SqlServer' 'backend.trx'
    npm --prefix frontend run lint
    if ($LASTEXITCODE -ne 0) { throw '前端 lint 失敗。' }
    npm --prefix frontend test
    if ($LASTEXITCODE -ne 0) { throw '前端測試失敗。' }

    if ($Browser) {
        $env:AINEXUS_TEST_BROWSER = $BrowserTarget
        $env:NEXUS_DIAGNOSTIC_WEBROOT = Join-Path $root "$verification/wwwroot"
        Invoke-BackendTests 'Category=Browser' 'diagnostic-browser.trx'
        $env:NEXUS_E2E_PUBLISH_DIRECTORY = $verification
        npm run test:e2e
        if ($LASTEXITCODE -ne 0) { throw 'e2e 測試失敗。' }
    }
    if ($SqlServer) { Invoke-BackendTests 'Category=SqlServer' 'sqlserver.trx' }
    if ($Performance) { Invoke-BackendTests 'Category=Performance' 'diagnostic-performance.trx' }
    Write-Output '驗證通過。'
} finally {
    $env:AINEXUS_TEST_BROWSER = $previous.Browser
    $env:NEXUS_DIAGNOSTIC_WEBROOT = $previous.WebRoot
    $env:NEXUS_E2E_PUBLISH_DIRECTORY = $previous.Publish
    Pop-Location
}
