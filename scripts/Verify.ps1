#requires -Version 7.4

<#
.SYNOPSIS
送 PR 前的完整本機驗證。

.DESCRIPTION
依序執行：腳本測試（Pester，scripts/tests）、前端 schema.ts 與 openapi.json 一致性、前端 Prettier 格式、Build.ps1（輸出到 artifacts/verification，不覆寫正在執行的 publish）、
後端 build、資料庫專案 build（database/App → dacpac）、後端單元／整合／架構測試、前端 lint 與單元測試。
整合測試以 dacpac 在 LocalDB（SQL Server 2025）建庫，見 docs/development/test-database.md。真實瀏覽器與效能測試需另外加參數。
TRX 寫在 artifacts/test-results。需先執行 Restore.ps1；.github/workflows/ci.yml 也是 Restore.ps1 加上這支腳本。

.PARAMETER Browser
加跑後端真實瀏覽器測試（Category=Browser：PDF 匯出、診斷與監控頁）與 Playwright e2e。

.PARAMETER BrowserTarget
後端瀏覽器測試用的瀏覽器：msedge、chrome，或執行檔絕對路徑（Linux：/opt/pw-browsers/chromium）。

.PARAMETER Performance
加跑隔離的效能量測（Category=Performance），結果寫在 artifacts/。

.EXAMPLE
./scripts/Verify.ps1

.EXAMPLE
./scripts/Verify.ps1 -Browser -Performance

#>
[CmdletBinding()]
param([switch]$Browser, [string]$BrowserTarget = 'msedge', [switch]$Performance)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AiNexus') -Force

$root = Get-NexusRoot
$verification = 'artifacts/verification'
$results = 'artifacts/test-results'

function Invoke-BackendTests([string]$Filter, [string]$Report) {
    # 分類篩選時，沒有該分類的測試專案會以「零項測試」（結束代碼 8）結束，這不算失敗。
    dotnet test --solution backend/AiNexus.slnx --no-build -c Release --filter $Filter --report-xunit-trx --report-xunit-trx-filename $Report --results-directory $results --ignore-exit-code 8
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

    npm --prefix frontend run contracts:check
    if ($LASTEXITCODE -ne 0) { throw '前端 schema.ts 與 contracts/openapi.json 不一致，請執行 npm --prefix frontend run contracts。' }
    npm --prefix frontend run format:check
    if ($LASTEXITCODE -ne 0) { throw '前端格式不符 Prettier，請執行 npm --prefix frontend run format。' }
    & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $verification
    dotnet build backend/AiNexus.slnx --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw '後端 build 失敗。' }
    dotnet build database/App --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw '資料庫專案 build 失敗：匯出的物件有引用錯誤，或 database/App/App.sqlproj 設定有誤。' }
    Invoke-BackendTests 'Category!=Performance&Category!=Browser' 'backend.trx'
    npm --prefix frontend run lint
    if ($LASTEXITCODE -ne 0) { throw '前端 lint 失敗。' }
    npm --prefix frontend test
    if ($LASTEXITCODE -ne 0) { throw '前端測試失敗。' }

    if ($Browser) {
        $env:AINEXUS_TEST_BROWSER = $BrowserTarget
        $env:NEXUS_DIAGNOSTIC_WEBROOT = Join-Path $root "$verification/wwwroot"
        Invoke-BackendTests 'Category=Browser' 'diagnostic-browser.trx'
        $env:NEXUS_E2E_PUBLISH_DIRECTORY = $verification
        npm --prefix frontend run test:e2e
        if ($LASTEXITCODE -ne 0) { throw 'e2e 測試失敗。' }
    }
    if ($Performance) { Invoke-BackendTests 'Category=Performance' 'diagnostic-performance.trx' }
    Write-Output '驗證通過。'
} finally {
    $env:AINEXUS_TEST_BROWSER = $previous.Browser
    $env:NEXUS_DIAGNOSTIC_WEBROOT = $previous.WebRoot
    $env:NEXUS_E2E_PUBLISH_DIRECTORY = $previous.Publish
    Pop-Location
}
