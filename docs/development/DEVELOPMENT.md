# 開發與執行

- 版本固定：Node 26.5.0、npm 11.6.1、.NET SDK 10.0.401（`global.json`）、PowerShell 7.4 以上；依賴以 lockfile 還原。
- 日常只需要 `./scripts/Start-Local.ps1`（整合預覽）或 `./scripts/Start-Dev.ps1`（熱更新）；送 PR 前必跑 `./scripts/Verify.ps1`。
- 每支入口腳本都有說明：`Get-Help ./scripts/<名稱>.ps1 -Detailed`。共用函式在 `scripts/AiNexus/AiNexus.psm1`，腳本測試（Pester 5）在 `scripts/tests`。
- 設定見 [設定與秘密](CONFIGURATION.md)，測試見 [測試](TESTING.md)，本機產生的資料夾見 [LOCAL_FOLDERS](LOCAL_FOLDERS.md)。

## 第一次啟動

在 repository 根目錄的 PowerShell 7 執行：

```powershell
./scripts/Restore.ps1
./scripts/Configure-Local.ps1
./scripts/Initialize-Database.ps1
dotnet dev-certs https --trust
./scripts/Start-Local.ps1
```

開啟 https://localhost:5080/ 。初始化需要有 DDL 權限的 SQL 登入；`Initialize-Database.ps1` 只在資料庫不存在時建庫，再套用未完成的 migration，不刪除既有資料（見 [資料庫](../architecture/DATABASE.md#初始化與-sql-權限)）。

## 兩種運行方式

| 方式 | 指令 | 程序／網址 | 適合 |
| --- | --- | --- | --- |
| 整合預覽 | `./scripts/Start-Local.ps1` | 一個 ASP.NET host，5080 | 日常使用、接近 IIS 的同源驗收 |
| 開發更新 | `./scripts/Start-Dev.ps1` | Angular 4200＋API 5080 | 編輯前後端、自動更新 |

- 整合預覽先 build Angular 再 publish .NET，靜態檔放進 `artifacts/publish/wwwroot`；已 build 時加 `-SkipBuild`。重新 build 前先停止正在使用 publish 目錄的程序。
- 開發模式啟動 `dotnet watch` 與 Angular dev server，請開 4200；`/api`、`/health` 代理到後端，同源 cookie 與 CSRF 照常運作。日誌在 `.local/logs`。Ctrl+C 同時停止兩個程序。
- 兩種模式都讀同一份 `.local/config` 與 `.local/secrets`，修改後重啟。開發代理建議用 Ldap 登入；Windows Negotiate 經代理的行為需另外實測。
- 預設 HTTPS，Session／Antiforgery cookie 是 `Secure`、`HttpOnly`、`SameSite=Strict`。開發代理用同一張 SDK 憑證（暫存在 `.local/certs`）。純 HTTP 的本機測試要明確加 `-Http`，只在 Development 且 Host 與來源 IP 都是 loopback 時生效，見 [網站安全](../architecture/SECURITY.md)。

```powershell
./scripts/Start-Dev.ps1 -BackendPort 5081 -FrontendPort 4201   # 5080 已被預覽占用時
./scripts/Start-Dev.ps1 -Restore                               # 首次開發合併 restore
./scripts/Start-Local.ps1 -SkipBuild -Port 5081
```

## 建置與驗證

```powershell
./scripts/Build.ps1 -Restore                                 # restore + Angular build + .NET publish
./scripts/Verify.ps1                                         # 送 PR 前必跑
./scripts/Verify.ps1 -Browser -Performance                   # 加跑真實瀏覽器、e2e 與效能量測
./scripts/Build.ps1 -OutputDirectory artifacts/verification  # 預覽執行中時用獨立產物
./scripts/Test-Environment.ps1                               # 真實 SQL／AD／模型，與自動化測試分開
```

- `Verify.ps1` 依序跑腳本測試、`schema.ts` 與 `openapi.json` 一致性、build（輸出到 `artifacts/verification`，不覆寫執行中的 publish）、後端 build、`has-pending-model-changes`、後端測試、前端 lint 與單元測試。
- 改到日誌、安全錯誤或監控時加 `-Browser -Performance`。
- GitHub Actions 只能手動觸發（Actions 頁面的 Run workflow），在 Windows 上跑 `Restore.ps1`＋`Verify.ps1`，和本機是同一組步驟。
- 建置只接受 `artifacts/` 內的輸出路徑，刪除舊 wwwroot 前驗證絕對路徑與 reparse point。

## API 合約

API 有變動時，啟動 Development API（整合預覽也可）後執行：

```powershell
./scripts/Export-Contracts.ps1 -BaseUrl https://localhost:5080
```

一起提交 `contracts/openapi.json` 與產生的 `frontend/src/app/core/api/schema.ts`，不手改。只改了 `openapi.json` 時用 `npm --prefix frontend run contracts` 重產 `schema.ts`。

型別鏈：後端 DTO 改變 → `OpenApiContractTests` 要求重產 `openapi.json` → `Verify.ps1` 的 `contracts:check` 要求重產 `schema.ts` → 前端用到舊欄位的地方編譯失敗。前端所有請求都經過 `ApiClient`，路徑、參數、body 與回應型別都來自 `schema.ts`（寫法見 [前端共用邊界](../frontend/FRONTEND_BOUNDARIES.md#呼叫-api)）。

- 合約由 `backend/src/AiNexus.Host/OpenApiContract.cs` 調整成前端可直接使用：只出現在回應的 DTO，每個屬性都標 required（伺服器一定會寫出），只有條件式 `JsonIgnore` 的屬性可省略；也會當請求 body 的型別維持產生器的判斷，因為有預設值的欄位用戶端可以不送。query 參數名一律 camelCase，enum 以名稱傳遞。
- `openapi-typescript` 宣告的 peer 是 TypeScript 5；`frontend/package.json` 以 `overrides` 讓它使用前端的 TypeScript 6，產出與 TypeScript 5 相同。
- SSE 等 OpenAPI 表達不了的規則在 [SSE](../architecture/SSE.md)。

## 資料結構

先改實體與組態，再產生 migration 並提交 source、designer 與 snapshot，命令與規則見 [資料庫](../architecture/DATABASE.md#migration)。給 DBA 的 SQL 由 `Publish-IIS.ps1` 在發布時產生，不進版控。不要用正式資料測 `EnsureCreated`、migration rollback 或測試身分。

## 程式碼放哪裡

頂層資料夾見 README 的 [資料夾地圖](../../README.md#資料夾地圖)；後端三個專案的責任見 [架構總覽](../architecture/ARCHITECTURE.md#三個專案)，模組內的檔案配置見 [後端撰寫慣例](../architecture/BACKEND_CONVENTIONS.md)。

## 提交前

- `.local/`（本機設定、秘密、日誌、個人筆記）與 `artifacts/`（建置與測試輸出）都不進 Git。
- 看 `git status`／`git diff --cached`，只提交 source、公開預設值與範本、lockfile、產生的合約與 migration、長期文件。不要用 `git add -f` 強制加入本機資料。
- stage 後執行 `./scripts/Test-Repository.ps1`（或 `-WorkingTree` 不動 index）：檢查本機與產物路徑、公開設定的秘密欄位、常見 live key、本機秘密值與 whitespace，只輸出檔名與結果。它不能取代審閱 diff 或組織的秘密掃描。
