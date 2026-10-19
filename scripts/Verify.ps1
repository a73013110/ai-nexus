#requires -Version 7.4

<#
.SYNOPSIS
送 PR 前的完整本機驗證。

.DESCRIPTION
依序執行：腳本測試（Pester，scripts/tests）、前端 schema.ts 與 openapi.json 一致性、前端 Prettier 格式、Build.ps1（輸出到 artifacts/verification，不覆寫正在執行的 publish）、
後端 build、資料庫專案 build（database/App → dacpac）、後端單元／整合／架構測試、前端 lint 與單元測試。
整合測試以 dacpac 在 LocalDB（SQL Server 2025）建庫，見 docs/development/test-database.md。真實瀏覽器與效能測試需另外加參數。
TRX 寫在 artifacts/test-results。需先執行 Restore.ps1；.github/workflows/ci.yml 也是 Restore.ps1 加上這支腳本。
每一步顯示 [n/總數] 標題，結束時（包括失敗）印出各步驟的結果與耗時；第一個失敗之後的步驟不執行。

.PARAMETER Browser
加跑後端真實瀏覽器測試（Category=Browser：PDF 匯出、診斷與監控頁）與 Playwright e2e。

.PARAMETER BrowserTarget
後端瀏覽器測試用的瀏覽器：msedge、chrome，或執行檔絕對路徑（Linux：/opt/pw-browsers/chromium）。

.PARAMETER Performance
加跑隔離的效能量測（Category=Performance），結果寫在 artifacts/。

.COMPONENT
建置與檢查

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

$root = Get-RepoRoot
$verification = 'artifacts/verification'
$results = 'artifacts/test-results'

function Invoke-BackendTests([string]$Filter, [string]$Report) {
    # 分類篩選時，沒有該分類的測試專案會以「零項測試」（結束代碼 8）結束，這不算失敗。
    dotnet test --solution backend/AiNexus.slnx --no-build -c Release --filter $Filter --report-xunit-trx --report-xunit-trx-filename $Report --results-directory $results --ignore-exit-code 8
    if ($LASTEXITCODE -ne 0) { throw "後端測試失敗：$Filter" }
}

$scripts = $PSScriptRoot
$previous = @{ Browser = $env:AINEXUS_TEST_BROWSER; WebRoot = $env:AINEXUS_DIAGNOSTIC_WEBROOT; Publish = $env:AINEXUS_E2E_PUBLISH_DIRECTORY }
Push-Location -LiteralPath $root
try {
    Invoke-RepoSteps '送 PR 前驗證' @(
        @{ Name = '腳本測試'; Run = {
                Import-RepoPester
                $pester = New-PesterConfiguration
                $pester.Run.Path = Join-Path $scripts 'tests'
                $pester.Run.PassThru = $true
                $pester.Output.Verbosity = 'Normal'
                if ((Invoke-Pester -Configuration $pester).Result -ne 'Passed') { throw '腳本測試失敗。' }
            }
        }
        @{ Name = '合約一致性'; Run = {
                npm --prefix frontend run contracts:check
                if ($LASTEXITCODE -ne 0) { throw '前端 schema.ts 或 feature-catalog.ts 與 contracts/openapi.json 不一致，請執行 npm --prefix frontend run contracts。' }
            }
        }
        @{ Name = '前端格式'; Run = {
                npm --prefix frontend run format:check
                if ($LASTEXITCODE -ne 0) { throw '前端格式不符 Prettier，請執行 npm --prefix frontend run format。' }
            }
        }
        @{ Name = '建置網站'; Run = { & (Join-Path $scripts 'Build.ps1') -OutputDirectory $verification } }
        @{ Name = '後端 build'; Run = {
                dotnet build backend/AiNexus.slnx --no-restore -c Release
                if ($LASTEXITCODE -ne 0) { throw '後端 build 失敗。' }
            }
        }
        @{ Name = '資料庫專案 build'; Run = {
                dotnet build database/App --no-restore -c Release
                if ($LASTEXITCODE -ne 0) { throw '資料庫專案 build 失敗：匯出的物件有引用錯誤，或 database/App/App.sqlproj 設定有誤。' }
            }
        }
        @{ Name = '後端測試'; Hint = "測試報告：$results/backend.trx"; Run = { Invoke-BackendTests 'Category!=Performance&Category!=Browser' 'backend.trx' } }
        @{ Name = '前端 lint'; Run = {
                npm --prefix frontend run lint
                if ($LASTEXITCODE -ne 0) { throw '前端 lint 失敗。' }
            }
        }
        @{ Name = '前端單元測試'; Run = {
                npm --prefix frontend test
                if ($LASTEXITCODE -ne 0) { throw '前端測試失敗。' }
            }
        }
        @{ Name = '瀏覽器測試'; Skip = $(if (!$Browser) { '未加 -Browser' }); Hint = "測試報告：$results/diagnostic-browser.trx"; Run = {
                $env:AINEXUS_TEST_BROWSER = $BrowserTarget
                $env:AINEXUS_DIAGNOSTIC_WEBROOT = Join-Path $root "$verification/wwwroot"
                Invoke-BackendTests 'Category=Browser' 'diagnostic-browser.trx'
            }
        }
        @{ Name = 'e2e'; Skip = $(if (!$Browser) { '未加 -Browser' }); Run = {
                $env:AINEXUS_E2E_PUBLISH_DIRECTORY = $verification
                npm --prefix frontend run test:e2e
                if ($LASTEXITCODE -ne 0) { throw 'e2e 測試失敗。' }
            }
        }
        @{ Name = '效能量測'; Skip = $(if (!$Performance) { '未加 -Performance' }); Run = { Invoke-BackendTests 'Category=Performance' 'diagnostic-performance.trx' } }
    )
} finally {
    $env:AINEXUS_TEST_BROWSER = $previous.Browser
    $env:AINEXUS_DIAGNOSTIC_WEBROOT = $previous.WebRoot
    $env:AINEXUS_E2E_PUBLISH_DIRECTORY = $previous.Publish
    Pop-Location
}
