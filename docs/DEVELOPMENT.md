# 開發、執行與文件管理

日誌與安全錯誤變更須執行 `scripts/Verify.ps1 -Browser -Performance`，加跑實際 Angular＋Kestrel 查證流程、原有 UI 回歸及隔離效能量測。report／TRX／screenshots 在 artifacts，見 [驗收文件](DIAGNOSTICS_VERIFICATION.md)。`Test-Repository.ps1 -WorkingTree` 可在不改 Git index 的情況檢查追蹤與未忽略的新檔；預設仍檢查 staged。應用日誌位於站外 Diagnostics.Directory，與 `.local`／ANCM stdout 分開。

既有 Playwright UI 回歸由 `Start-BrowserTest.ps1` 啟動編譯好的網站，使用 artifacts 下明確指定的空設定／秘密檔與獨立附件、金鑰、診斷目錄，不載入開發機 `.local`。API fixture 回歸與使用隔離 SQLite 的真實診斷端到端測試分開；前者的 SQL 匯入降級是刻意未配置資料庫，不能當成 SQL Server 效能或功能驗證。

## 第一次啟動

專案固定 Node 26.5.0、npm 11.6.1、.NET SDK 10.0.401／runtime 10.0.12，以及 PowerShell 7.4 以上；版本見 `.node-version`、`global.json` 與 lockfiles。從 repository 根目錄的 PowerShell 7 執行：

```powershell
./scripts/Restore.ps1
./scripts/Configure-Local.ps1
./scripts/Initialize-Database.ps1
dotnet dev-certs https --trust
./scripts/Start-Local.ps1
```

開啟 https://localhost:5080/chat。第一次初始化使用有 DDL 權限的既有 SQL 登入；設定方式見 [CONFIGURATION](CONFIGURATION.md)。開發機首次信任 SDK HTTPS 憑證後，已有設定與 migrations 的日常啟動只需要 `Start-Local.ps1`。

## 兩種運行方式

| 方式 | 指令 | 程序／網址 | 適合 |
| --- | --- | --- | --- |
| 整合預覽 | `./scripts/Start-Local.ps1` | 一個 ASP.NET host，5080 | 日常體驗、接近 IIS 部署的同源驗收 |
| 開發更新 | `./scripts/Start-Dev.ps1` | Angular 4200 + API 5080，由腳本管理 | 編輯前後端、自動更新 |

整合預覽先 build Angular，再 publish .NET，將靜態產物放進 `artifacts/publish/wwwroot`；不需另外跑 npm server。已 build 可用 `Start-Local.ps1 -SkipBuild`。Ctrl+C 停止 host，重新 build 前先停止正在使用 publish 目錄的程序。

開發模式會啟動 `dotnet watch` 與 Angular dev server。請開 4200；`/api`／`/health` 代理至後端，同源 cookie／CSRF 可以正常運作。Ctrl+C 同時停止腳本啟動的兩個程序。日誌放 `.local/logs/backend.log`、`frontend.log` 與各自 `.error.log`。啟動後等待前後端編譯完成才開頁面。

兩種模式預設 HTTPS，Session／Antiforgery Cookie 使用 `Secure`、`HttpOnly`、`SameSite=Strict`。開發代理使用同一 SDK 憑證，將 PEM／private key 暫存在 ignored `.local/certs`，Node 只信任該憑證。純 HTTP 的自動化／localhost 測試必須明確加 `-Http`；例外只在 Development 且 Host 與來源 IP 都為 loopback 時生效，不能用於 Production。舊 `Security:DisableHttpsRedirection` 已移除，詳見 [安全](SECURITY.md)。

```powershell
# 既有預覽占用 5080 時使用另一組 port
./scripts/Start-Dev.ps1 -BackendPort 5081 -FrontendPort 4201
# 首次開發可合併 restore
./scripts/Start-Dev.ps1 -Restore
# 整合預覽可指定 port
./scripts/Start-Local.ps1 -SkipBuild -Port 5081
```

兩種方式都讀同一份 `.local/config` 與 `.local/secrets`，不把帳密複製進 build；修改參數需重啟。開發代理優先用 Ldap 登入；Windows Negotiate 經代理的連線親和性需實測，IIS 的 Windows 驗證設定見部署文件。

## 建置與驗證

```powershell
./scripts/Build.ps1 -Restore  # restore + Angular build + .NET publish
./scripts/Verify.ps1         # 腳本測試 + build + 後端 + 前端 lint／測試
./scripts/Verify.ps1 -Browser -Performance  # 再加真實瀏覽器、e2e 與效能量測
./scripts/Build.ps1 -OutputDirectory artifacts/verification # 預覽仍運行時使用獨立產物
./scripts/Test-Environment.ps1  # 真實 SQL／AD／模型，與自動化測試分開
```

每支入口腳本都有說明：`Get-Help ./scripts/Verify.ps1 -Detailed`。共用函式在 `scripts/AiNexus/AiNexus.psm1`，腳本本身的測試（Pester 5）在 `scripts/tests`，由 Verify 執行；`Restore.ps1` 會在缺少時安裝 Pester。研究用的 `Test-LocalAI.ps1`、`Compare-Embeddings.ps1` 在 `tooling/embeddings`。

後端測試的執行方式與 `NexusFactory` 的寫法見 [後端測試](BACKEND_TESTING.md)。

GitHub Actions 的 CI 目前只能手動觸發（Actions 頁面的 Run workflow），不會在 push 或 PR 時自動執行；它在 Windows 上執行 `Restore.ps1` 與 `Verify.ps1`，和本機驗證是同一組步驟。送 PR 前請在本機跑 `Verify.ps1`（含 `has-pending-model-changes`，模型有未產生的 migration 就失敗）。

瀏覽器測試使用本機已安裝 Edge，測試伺服器在 5180。後端的真實瀏覽器測試（`Category=Browser`：PDF 匯出、診斷與監控頁）只在設定 `AINEXUS_TEST_BROWSER` 時執行，其值為 channel（`msedge`、`chrome`）或瀏覽器執行檔絕對路徑；`Verify.ps1 -Browser` 會設定它，Linux 以 `-BrowserTarget /opt/pw-browsers/chromium` 指定 Playwright 的 Chromium。測試替身僅存在 `backend/tests`、`tests/e2e`，正式程式不接受測試身分 header。Playwright 覆蓋鍵盤、中文組字、版本分支、斷線、Markdown 安全、模型政策、Context、可讀字體與窄螢幕。結果、trace 與畫面全部在 ignored `artifacts`。後端測試使用獨立 SQLite；SQL schema/migrations、AD 與真模型仍由連線檢查／實機驗收驗證。

## API 與 migration 更新

`Verify.ps1` 使用 `artifacts/verification`，避免覆寫正在運行的 publish DLL。Playwright 繼承 `NEXUS_E2E_PUBLISH_DIRECTORY` 選擇產物；單獨執行 npm 測試預設 publish，可設 `$env:NEXUS_E2E_PUBLISH_DIRECTORY='artifacts/verification'`。手動預覽可指定 `Start-Local.ps1 -SkipBuild -PublishDirectory artifacts/verification -Port 5081`。Build 只接受 workspace artifacts 內的輸出，刪除舊 wwwroot 前驗證絕對路徑及 reparse point。

開啟 Development API 後執行（整合預覽也可）：

```powershell
./scripts/Export-Contracts.ps1 -BaseUrl https://localhost:5080
```

一起提交 `contracts/openapi.json` 與自動產生的 `frontend/src/app/core/api/schema.ts`，不手改 generated 型別。JSON／SSE 的額外規範在 [contracts/SSE](../contracts/SSE.md)。工具的 TypeScript 5 獨立於 Angular 的 TypeScript 6。

資料結構修改先更新 entity／mapping，再新增 migration：

```powershell
dotnet ef migrations add DescriptiveChange --project backend/src/AiNexus.Features --startup-project backend/src/AiNexus.Host --output-dir Persistence/Migrations
./scripts/Initialize-Database.ps1
```

migration 在本機驗證後提交 source、designer 與 snapshot；給 DBA 的 SQL 由 `Publish-IIS.ps1` 在發布時產生，不進版控。正式環境以受控部署帳號執行；不要用正式資料測 `EnsureCreated`、migration rollback 或測試身分。工具由 `.config/dotnet-tools.json` 固定版本。

## 資料夾的責任

| 路徑 | 責任 |
| --- | --- |
| `backend/src/AiNexus.Host` | host：設定載入、middleware 管線、模組組裝（`Program.cs`）與維運指令（`Commands/`：`db init`、`verify …`，`AiNexus.Host --help` 列出） |
| `backend/src/AiNexus.Features/<Module>` | 業務模組：`<Module>Module.cs` 註冊服務、政策與端點，旁邊是 endpoint、entity、service |
| `backend/src/AiNexus.Features/Persistence` | 共用 `NexusDbContext`、migrations、資料庫初始化與 schema 檢查 |
| `backend/src/AiNexus.Platform` | 不依賴業務的共用基礎：錯誤、安全、設定、診斷、HTTP 限制、手寫 SQL 存取（`Data/Sql`） |
| `backend/tests/AiNexus.Tests` | 整合測試（`WebApplicationFactory`）、OpenAPI 合約與端點慣例 |
| `backend/tests/AiNexus.ArchitectureTests` | 專案依賴方向與模組邊界（模組之間不可有循環） |
| `frontend/src/app/features` | 按路由功能的 UI 與 store |
| `frontend/src/app/core` | API、認證、偏好與 SSE 基礎服務 |
| `frontend/src/app/shared/ui` | 無業務狀態的圖示、Markdown、訊號元件 |
| `frontend/src/*.scss` | 三層 tokens、base、分區樣式與 motion |

頂層資料夾各自的用途見 README 的[資料夾地圖](../README.md#資料夾地圖)。

## 什麼文件進 Git

`README` 是入口；`docs` 放會隨程式維護的操作、架構、資料庫、授權與設計文件。本機設定、秘密、日誌與個人筆記放 `.local`，建置與測試輸出放 `artifacts`，兩者都不進 Git，各子資料夾的用途見 [本機產生的資料夾](development/LOCAL_FOLDERS.md)。

提交前看 `git status`／`git diff --cached`，確認只包含 source、public defaults／examples、lockfiles、generated contracts/migrations 與長期文件。`.gitignore` 不是秘密掃描器，不以 `git add -f` 強制加入本機資料。

先 stage 預計提交的檔案，再執行 `./scripts/Test-Repository.ps1`。它檢查 index 中的本機／產物路徑、public config 的空白秘密欄位、常見 live key 與本機秘密值、source whitespace；只輸出檔名／結果，不輸出秘密。這是針對此專案的檢查，不能取代內容審閱或組織秘密掃描服務。
