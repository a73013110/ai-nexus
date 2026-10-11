# AI Nexus

校內 AI 工作平台：.NET 10 Minimal API（模組化單體＋vertical slice）、EF Core 10／SQL Server、Angular 22（zoneless、signals），部署在單一 IIS 執行個體。

## 讀文件的原則

- 不要整批讀 `docs/`。只讀這次修改涉及的那一份，需要時再讀下一份。
- 規則以程式碼與測試為準；文件只寫程式碼表達不了的東西（原因、取捨、操作步驟）。

| 修改內容 | 先讀 |
|---|---|
| 後端 slice、錯誤、驗證、軟刪除 | `docs/architecture/backend-conventions.md` |
| 模組邊界、跨模組依賴、domain event | `docs/architecture/module-boundaries.md` |
| 推論、聊天生成、背景工作 | `docs/architecture/generation.md` |
| 後端測試、`NexusFactory` | `docs/development/testing.md` |
| 錯誤回應、查證代碼、遮罩 | `docs/architecture/error-contract.md` |
| 日誌事件、EventId | `docs/architecture/log-events.md` |
| 資料表、migration | `docs/architecture/database.md` |
| 設定、秘密 | `docs/development/configuration.md` |
| 授權、功能 grant | `docs/architecture/access-control.md` |
| 腳本、建置、驗證 | `docs/development/development.md` |
| IIS 部署、發布套件 | `docs/operations/iis-deployment.md` |
| 前端資料夾、API 呼叫、讀取與寫入 | `docs/frontend/frontend-boundaries.md` |
| 前端 UI、樣式 | `docs/frontend/ui-patterns.md`、`docs/frontend/design-system.md` |
| 聊天渲染、串流 Markdown、前端請求與快取 | `docs/frontend/chat-rendering.md` |
| 單一功能 | `README.md` 功能表連到的 `docs/features/` 那一份 |
| 為什麼不拆專案、不用多個 DbContext 等 | `docs/decisions/` |

## 指令

```powershell
./scripts/Verify.ps1                     # 送 PR 前必跑：腳本測試＋build＋後端＋前端 lint／測試
dotnet build backend/AiNexus.slnx         # 0 warning（warning 即錯誤）
dotnet test --solution backend/AiNexus.slnx --no-build --filter "FullyQualifiedName~<Module>"
dotnet ef migrations has-pending-model-changes --project backend/src/AiNexus.Features --startup-project backend/src/AiNexus.Host
./scripts/Export-Contracts.ps1 -BaseUrl https://localhost:5080   # API 有變動時重產 openapi.json 與 schema.ts
```

GitHub Actions 只能手動觸發，驗證在本機完成。

## 必須遵守

- 一個 use case 一個檔案（handler＋endpoint＋validator），放在 `AiNexus.Features/<Module>/`（大模組依能力分子資料夾）；namespace＝資料夾。
- 預期內的失敗回傳 `Result<T>`，錯誤定義在 `<Module>Errors`；不要為此 throw。
- request body 要有 `RequestValidator<T>`，刻意交給 handler 驗證的標 `[ValidatedInHandler(原因)]`；時間用 `TimeProvider`；使用者用 `ICurrentUser`。
- 設定用 `AddSettings<T, TValidator>(區段)` 註冊（`[OptionsValidator]` 驗證、啟動即檢查），區段名＝模組名；預設值寫進 `appsettings.json`。
- 日誌用 `[LoggerMessage]`，EventId 固定且唯一；NuGet 版本只寫在 `backend/Directory.Packages.props`。
- 跨模組的副作用用 domain event；跨模組讀取只透過對方的 `public` 服務。
- 模組之間不可有循環依賴（`ModuleBoundaryTests`）。
- 不手改 `contracts/openapi.json`、`frontend/src/app/core/api/schema.ts`、migrations。
- PowerShell 入口腳本要有 `[CmdletBinding()]`、`Set-StrictMode -Version Latest` 與 comment-based help（`scripts/tests` 檢查）。
- 不提交 `.local/`、`artifacts/`、秘密；不跳過或停用測試。

## 命名規範

| 對象 | 規則 | 例 |
|---|---|---|
| C# 型別、方法、屬性、檔名 | PascalCase，檔名＝主要型別名 | `SavePromptTemplate.cs` |
| C# 參數、區域變數 | camelCase；私有欄位不加 `_` | `ownerId` |
| use case 類別 | 動詞＋名詞 | `ListNotifications`、`DeleteArtifact` |
| 非同步方法 | 加 `Async` | `HandleAsync` |
| 介面 | `I` 開頭 | `ICurrentUser` |
| domain event | 名詞＋過去分詞 | `ConversationDeleted` |
| API 路徑 | `/api/v1/` ＋ 小寫 kebab-case 複數名詞；路徑參數 camelCase | `/api/v1/client-issues/{id}` |
| JSON 欄位 | camelCase | `createdAt` |
| 錯誤代碼 | snake_case，名詞＋狀態 | `template_not_found` |
| 資料庫 schema | 小寫單字，與模組對應 | `knowledge` |
| 資料表、欄位 | PascalCase，表名複數 | `GenerationRuns.OwnerId` |
| 索引、約束 | EF 預設：`IX_表_欄`、`CK_表_名` | `IX_Attachments_StorageKey` |
| Angular 檔名 | 小寫 kebab-case，不加 `.component` | `chat-workspace.ts` |
| Angular 類別、signal | PascalCase 類別；signal 用名詞，不加 `$` | `ChatStore`、`messages` |
| CSS class | kebab-case | `message-row` |
| PowerShell 腳本 | `scripts/` 只放入口，動詞-名詞或單一動詞；共用函式放 `scripts/AiNexus` 模組，名稱含 `-Nexus` | `Start-Local.ps1`、`Build.ps1`、`Get-NexusLocalPaths` |
| 文件 | `docs/<讀者>/` 下小寫 kebab-case.md；ADR 為 `docs/decisions/<四位編號>-kebab-case.md`；根目錄 `README.md`、`CLAUDE.md` 固定大寫 | `architecture/backend-conventions.md` |
| commit | Conventional Commits，說明用繁體中文 | `perf(inference): 串流只寫入增量` |

改 `docs/` 或任何 Markdown 時，另依 `.claude/rules/docs.md`。
