# AI Nexus

校內 AI 工作平台：私人對話、知識庫、專案、成果文件與分享，模型可同時路由到 Google AI 與地端 Ollama。後端是 ASP.NET Core 10 模組化單體（Minimal API、vertical slice、EF Core／SQL Server），前端是 Angular 22，部署在單一 IIS 網站，以 AD 登入。

## 快速啟動

需要 Node 26.5.0、npm 11.6.1、.NET SDK 10.0.401、PowerShell 7.4 以上。在專案根目錄執行：

```powershell
./scripts/Restore.ps1
./scripts/Configure-Local.ps1      # 建立 .local 設定，遮蔽輸入 SQL／AD 密碼與 Google key
./scripts/Initialize-Database.ps1  # 只在資料庫不存在時建庫，再套用 migration
dotnet dev-certs https --trust     # 開發機首次
./scripts/Start-Local.ps1          # 一個程序同時提供前端與 API
```

開啟 https://localhost:5080/ 。熱更新用 `./scripts/Start-Dev.ps1`（Angular 4200＋API 5080）；送 PR 前跑 `./scripts/Verify.ps1`。每支腳本的參數用 `Get-Help ./scripts/<名稱>.ps1 -Detailed` 查看。詳見 [開發與執行](docs/development/DEVELOPMENT.md)。

## 資料夾地圖

| 項目 | 用途 |
| --- | --- |
| `backend/` | 後端方案 `AiNexus.slnx`：`src/` 有主機 `AiNexus.Host`、業務模組 `AiNexus.Features`、共用基礎 `AiNexus.Platform`，`tests/` 有單元、整合與架構測試；NuGet 版本集中在 `Directory.Packages.props` |
| `frontend/` | Angular 前端；`npm` 指令在這裡執行，`src/app` 分為 `core`、`shared`、`features` |
| `tests/e2e/` | Playwright 端到端測試，對發布後的網站執行 |
| `contracts/` | 由後端產生的 API 合約 `openapi.json`（前端型別來源、合約測試比對） |
| `deploy/` | 部署素材：`sql/` 是交給外部來源 DBA 的授權 view 範本；部署步驟在 `docs/operations/` |
| `docs/` | 長期文件，依讀者分子資料夾（見下方文件地圖） |
| `scripts/` | PowerShell 入口：還原、建置、啟動、驗證、發布；共用函式在 `AiNexus/` 模組，腳本與文件檢查在 `tests/` |
| `tooling/` | 非建置必需的工具：`contracts/` 由 OpenAPI 產生前端 `schema.ts`，`embeddings/` 比較 embedding 模型與實測本地 Ollama |
| `.github/` | 手動觸發的 CI，以及每月一次、依生態系合併成一個 PR 的 Dependabot |
| `.config/dotnet-tools.json` | 固定 `dotnet-ef` 版本（`dotnet tool restore`） |
| `global.json` | 固定 .NET SDK 版本與測試執行器 |
| `package.json`、`package-lock.json`、`.node-version` | 安裝 `tests/e2e` 用的 Playwright，固定 Node 版本 |
| `.editorconfig`、`.gitattributes`、`.gitignore` | 縮排與分析器規則、換行正規化、不進 Git 的檔案 |
| `CLAUDE.md` | 給 AI 程式助手的規則、指令與「改什麼讀哪份」索引 |
| `.local/`、`artifacts/` | 本機產生、不進 Git：本機設定與秘密、建置與測試輸出，見 [本機產生的資料夾](docs/development/LOCAL_FOLDERS.md) |

## 文件地圖

| 資料夾 | 讀者與內容 |
| --- | --- |
| [`docs/architecture/`](docs/architecture/ARCHITECTURE.md) | 改後端的人：架構總覽、模組邊界、撰寫慣例、資料庫、授權、安全、錯誤與日誌、生成與背景任務、SSE |
| [`docs/development/`](docs/development/DEVELOPMENT.md) | 開發者：啟動與驗證、設定與秘密、測試、本機資料夾 |
| [`docs/operations/`](docs/operations/IIS_DEPLOYMENT.md) | 維運：IIS 部署、設定、驗收、更新、備份還原、診斷日誌 |
| `docs/features/` | 使用者與管理員：各功能的操作與規則（見下方功能表） |
| `docs/decisions/` | 長期有效的架構決定與原因（ADR），例如 [單一 IIS 與程序內鎖](docs/decisions/0001-single-iis-in-process-locks.md)、[vertical slice 與單一 Features 專案](docs/decisions/0002-vertical-slice-single-features-project.md) |
| `docs/research/` | 研究與評估：[向量架構](docs/research/VECTOR_ARCHITECTURE.md)、[embedding 比較](docs/research/EMBEDDING_MODELS.md)、[本地 AI](docs/research/LOCAL_AI.md)、[檢索驗收](docs/research/RETRIEVAL_TESTING.md) |
| `docs/frontend/` | 前端 UI、樣式與聊天渲染（前端重整時再合併） |

## 功能

| 工作區 | 能力 | 文件 |
| --- | --- | --- |
| 總覽 | 空間流程與狀態、日期篩選、每次／全對話／使用者費用、價格版本與 CSV | [費用](docs/features/BILLING.md)、[名詞與統計](docs/features/TERMINOLOGY.md) |
| 對話 | 文件／圖片分析、分支、停止與斷線恢復、範本、搜尋、收藏／封存／標籤、草稿、文字備份、快捷指令 | [對話](docs/features/CHAT.md)、[文件與圖片](docs/features/ATTACHMENTS.md) |
| 個人設定 | 當頁設定視窗、主題、閱讀與密度、通知、草稿、用量、快捷鍵 | [個人設定](docs/features/SETTINGS.md) |
| 檔案庫 | 對話／知識／專案原檔、搜尋篩選、重用、容量 | [檔案庫](docs/features/FILES.md)、[附件保存](docs/features/ATTACHMENT_STORAGE.md) |
| 知識庫 | ACL、OCR 與索引、SQL 向量檢索、引用與原文核對、背景任務 | [知識庫](docs/features/KNOWLEDGE.md) |
| 專案 | 共用指示、文件、範本與成果，提問仍屬個人 | [專案](docs/features/PROJECTS.md) |
| 成果文件 | 共用編輯、不可變版本、段落工具、Word／PDF | [成果](docs/features/ARTIFACTS.md) |
| 分享 | 具名收件人、版本快照、附件授權、到期與撤銷 | [分享](docs/features/SHARING.md) |
| 品質評測 | 私人回饋、固定題庫、模型與指令比較、人工評分 | [品質](docs/features/QUALITY.md) |
| 程式庫 | Gitea 唯讀檔案與議題、固定 commit 快照、背景 AI review | [Gitea](docs/features/GITEA.md)、[程式碼 review](docs/features/CODE_REVIEW.md) |
| 連網搜尋 | 手動開啟、SearXNG／Brave、來源與時間、配額 | [網路搜尋](docs/features/WEB_SEARCH.md) |
| 資料來源 | 公文／校務唯讀 adapter、歷程、私人成果與聊天草稿 | [資料來源](docs/features/INTEGRATIONS.md) |
| 通知 | 共用通知中心、未讀、進度與分享事件、瀏覽器通知 | [通知](docs/features/NOTIFICATIONS.md) |
| 平台管理 | 帳號、角色／群組／功能、模型政策與配額、測試身分、用量與唯讀對話 | [管理](docs/features/ADMINISTRATION.md)、[模型政策](docs/features/MODEL_POLICY.md)、[測試身分](docs/features/TEST_IDENTITY.md) |
| 活動稽核 | 登入、異動與查閱紀錄、前後差異、與日誌互查 | [活動稽核](docs/features/ACTIVITY_AUDIT.md) |
| 系統日誌 | 查證代碼、錯誤與耗時、關聯流程；查詢／詳情／匯出分別授權 | [系統日誌](docs/features/SYSTEM_LOGS.md) |
| 即時監控 | 在線人員與工作階段、API／SQL／HTTP 負載、操作時間軸 | [監控](docs/features/MONITORING.md) |
| 介面元件 | 管理員檢視實際元件、主題、鍵盤與動畫 | [設計系統](docs/frontend/DESIGN_SYSTEM.md) |

各功能的新增、刪除、保存與權限對照見 [資源操作](docs/features/FEATURE_LIFECYCLE.md)。
