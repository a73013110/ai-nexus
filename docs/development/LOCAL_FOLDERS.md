# 本機產生的資料夾

- `.local/` 與 `artifacts/` 整個不進 Git（`.gitignore`），`Test-Repository.ps1` 也會擋下 staged 的檔案。
- `artifacts/` 隨時可以整個刪除，下次執行腳本會重建。
- `.local/` 刪除後要重跑 `./scripts/Configure-Local.ps1` 重新輸入設定與秘密。
- 需要分享測試證據時，審閱後另外提供，不要取消忽略或用 `git add -f`。

## `.local/`：只在這台機器有意義的東西

| 路徑 | 內容 | 寫入者 |
| --- | --- | --- |
| `config/appsettings.Local.json` | 一般參數（SQL、AD、模型路由…） | 首次執行由範本建立；`Configure-Local.ps1` 修改 |
| `secrets/appsettings.Secrets.json` | 密碼與 API key，ACL 只開給目前使用者、SYSTEM、Administrators | `Configure-Local.ps1` |
| `config/dev-proxy.json` | Angular dev server 的 `/api`、`/health` 代理，每次啟動重寫 | `Start-Dev.ps1` |
| `certs/` | 匯出給 Angular dev server 的 HTTPS 憑證與私鑰 | `Start-Dev.ps1` |
| `logs/` | 開發模式前後端的輸出與錯誤 | `Start-Dev.ps1` |
| `notes/` | 個人的排查紀錄、進度與臨時計畫 | 手動 |

設定的載入順序與欄位見 [參數](../CONFIGURATION.md)。

## `artifacts/`：建置與測試輸出

| 路徑 | 內容 | 寫入者 |
| --- | --- | --- |
| `publish/` | Angular＋.NET 發布產物，整合預覽從這裡執行 | `Build.ps1`、`Start-Local.ps1` |
| `verification/` | 驗證用的獨立產物，不覆寫正在執行的 `publish/` | `Verify.ps1`、`Test-Diagnostics.ps1 -Browser` |
| `test-results/` | 後端測試的 TRX | `Verify.ps1`、`Test-Diagnostics.ps1`、CI |
| `browser-results/`、`browser-report/`、`browser-results.json` | Playwright 的失敗截圖與 trace、HTML 報告、JSON 結果 | `tests/e2e/playwright.config.ts` |
| `browser-server-*/` | e2e 測試伺服器專用的空設定、金鑰、附件與日誌 | `Start-BrowserTest.ps1` |
| `screenshots/`、`monitoring-*.png` | e2e 測試內手動留存的畫面 | `tests/e2e/*.spec.ts` |
| `diagnostic-acceptance/`、`monitoring-acceptance/`、`diagnostics-performance.json` | 後端真實瀏覽器與效能測試的畫面和結果 | `Category=Browser`／`Performance` 測試 |
| `iis/<時間>/` | IIS 部署套件 | `Publish-IIS.ps1` |
| `connection-checks.json`、`sql-capabilities.json` | 實際 AD／SQL／模型連線與 SQL 能力的報告 | `Test-Connections.ps1`、`Test-SqlCapabilities.ps1` |
| `settings-test/` | 設定腳本測試的暫存檔 | `Test-Settings.ps1` |
| `embeddings/` | embedding 模型比較結果 | `tooling/embeddings/compare.py` |

新增會產生檔案的腳本或測試時，輸出放在 `artifacts/` 的子資料夾，並在上表加一行。`Build.ps1`、`Publish-IIS.ps1`、`Start-BrowserTest.ps1` 只接受 `artifacts/` 內的輸出路徑。
