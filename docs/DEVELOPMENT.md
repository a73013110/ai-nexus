# 開發、執行與文件管理

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
./scripts/Verify.ps1         # build + 後端 + 前端 + Edge 瀏覽器測試
./scripts/Verify.ps1 -SkipBrowser
./scripts/Test-Connections.ps1  # 真實外部連線，與自動化測試分開
```

瀏覽器測試使用本機已安裝 Edge，測試伺服器在 5180。測試替身僅存在 `backend/tests`、`tests/e2e`，正式程式不接受測試身分 header。Playwright 覆蓋鍵盤、中文組字、版本分支、斷線、Markdown 安全、模型政策、Context、可讀字體與窄螢幕。結果、trace 與畫面全部在 ignored `artifacts`。後端測試使用獨立 SQLite；SQL schema/migrations、AD 與真模型仍由連線檢查／實機驗收驗證。

## API 與 migration 更新

開啟 Development API 後執行（整合預覽也可）：

```powershell
./scripts/Export-Contracts.ps1 -BaseUrl https://localhost:5080
```

一起提交 `contracts/openapi.json` 與自動產生的 `frontend/src/app/core/api/schema.ts`，不手改 generated 型別。JSON／SSE 的額外規範在 [contracts/SSE](../contracts/SSE.md)。工具的 TypeScript 5 獨立於 Angular 的 TypeScript 6。

資料結構修改先更新 entity／mapping，再新增 migration 與 DBA 審閱 SQL：

```powershell
dotnet ef migrations add DescriptiveChange --project backend/src/AiNexus.Api --output-dir BuildingBlocks/Migrations
dotnet ef migrations script --idempotent --project backend/src/AiNexus.Api --output db/migrations.sql
./scripts/Initialize-Database.ps1
```

migration 在本機驗證後提交 source、designer、snapshot 與 SQL。正式環境以受控部署帳號執行；不要用正式資料測 `EnsureCreated`、migration rollback 或測試身分。工具由 `.config/dotnet-tools.json` 固定版本。

## 資料夾的責任

| 路徑 | 責任 |
| --- | --- |
| `backend/src/AiNexus.Api/Modules/<feature>` | 按業務模組的 endpoint、entity、service／policy |
| `backend/src/AiNexus.Api/BuildingBlocks` | host 組裝、共用契約／錯誤、context 與 migrations |
| `backend/src/AiNexus.Api/Database` | SqlClient adapter、初始化、原 EDoc helpers |
| `frontend/src/app/features` | 按路由功能的 UI 與 store |
| `frontend/src/app/core` | API、認證、偏好與 SSE 基礎服務 |
| `frontend/src/app/shared/ui` | 無業務狀態的圖示、Markdown、訊號元件 |
| `frontend/src/*.scss` | 三層 tokens、base、分區樣式與 motion |
| `contracts`／`db`／`deploy` | 可審閱的 API、資料庫、部署產物與說明 |
| `scripts`／`tooling`／`tests` | 可重現的操作、契約工具與測試 |

## 什麼文件進 Git

`README` 是入口；`docs` 放會隨程式維護的操作、架構、資料庫、授權與設計文件。`deploy` 放交付步驟，`db` 放 migration SQL 與說明。具體開發機環境、排查過程、進度與臨時計畫放 `.local/notes`。本機設定、key ring、logs 放 `.local`；所有 build／報告／screenshots 放 `artifacts`。兩個資料夾整體忽略，不逐一列例外。

提交前看 `git status`／`git diff --cached`，確認只包含 source、public defaults／examples、lockfiles、generated contracts/migrations 與長期文件。`.gitignore` 不是秘密掃描器，不以 `git add -f` 強制加入本機資料。需要分享測試證據時審閱後獨立提供，不取消 artifacts ignore。

先 stage 預計提交的檔案，再執行 `./scripts/Test-Repository.ps1`。它檢查 index 中的本機／產物路徑、public config 的空白秘密欄位、常見 live key 與本機秘密值、source whitespace；只輸出檔名／結果，不輸出秘密。這是針對此專案的檢查，不能取代內容審閱或組織秘密掃描服務。
