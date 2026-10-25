# AI Nexus

AI 工作平台：.NET 10 Minimal API（模組化單體＋vertical slice）、EF Core 10／SQL Server、Angular 22（zoneless、signals），部署在單一 IIS 執行個體。

## 讀文件的原則

- 不要整批讀 `docs/`。只讀這次修改涉及的那一份，需要時再讀下一份。
- 規則以程式碼與測試為準；文件只寫程式碼表達不了的東西（原因、取捨、操作步驟）。

| 修改內容 | 先讀 |
|---|---|
| 後端 slice、錯誤、驗證、軟刪除 | `docs/architecture/backend-conventions.md` |
| 模組邊界、跨模組依賴、domain event | `docs/architecture/module-boundaries.md` |
| 推論、聊天生成、背景工作 | `docs/architecture/generation.md` |
| 總覽區塊、儀表板 widget | `docs/architecture/dashboard-sections.md` |
| 監控子系統來源、健康判定 | `docs/architecture/monitoring-sources.md` |
| 郵件寄送、outbox | `docs/architecture/mail-outbox.md`（設定、重試、保存見 `docs/features/mail.md`） |
| 後端測試、`AppFactory` | `docs/development/testing.md` |
| 錯誤回應、查證代碼、遮罩 | `docs/architecture/error-contract.md` |
| 日誌事件、EventId | `docs/architecture/log-events.md` |
| 資料表、EF 對應、結構變更 | `docs/architecture/database.md` |
| procedure、function、view 的寫法 | `docs/architecture/sql-objects.md` |
| 設定、秘密 | `docs/development/configuration.md` |
| 授權、功能 grant、選單 | `docs/architecture/access-control.md` |
| 資料範圍、`IDataScope`、依部門切資料 | `docs/architecture/data-scopes.md` |
| 手動排序（`SortOrder`、拖曳排序列） | `docs/architecture/manual-ordering.md` |
| 腳本、建置、驗證 | `docs/development/development.md` |
| IIS 部署、發布套件 | `docs/operations/iis-deployment.md` |
| 前端資料夾、API 呼叫、讀取與寫入 | `docs/frontend/frontend-boundaries.md` |
| 前端 UI、樣式 | `docs/frontend/ui-patterns.md`、`docs/frontend/design-system.md`、`docs/frontend/shapes.md`、`docs/frontend/lines.md`、`docs/frontend/materials.md`、`docs/frontend/alignment.md` |
| 表單、對話框欄位排版、選人 | `docs/frontend/forms.md` |
| 共用元件、`/admin/design` 範例 | `docs/frontend/shared-components.md` |
| 檔案庫、知識庫、專案、成果等列表／詳細頁 | `docs/frontend/resource-pages.md` |
| 資料表、查詢與分頁（稽核、日誌、管理列表） | `docs/frontend/data-workspace.md` |
| 載入骨架、重新讀取、送出中、下拉與搜尋的等待 | `docs/frontend/loading.md` |
| 動效、動態島、通知（toast）、對話框進出場 | `docs/frontend/motion.md` |
| 手機版面、滑動關閉、螢幕鍵盤 | `docs/frontend/mobile.md` |
| 聊天渲染、串流 Markdown、前端請求與快取 | `docs/frontend/chat-rendering.md` |
| 單一功能 | `README.md` 功能表連到的 `docs/features/` 那一份 |
| 為什麼不拆專案、不用多個 DbContext、目錄為何對應模組等 | `docs/decisions/` |

## 指令

```powershell
./scripts/Verify.ps1                     # 送 PR 前必跑：腳本測試＋build＋後端＋前端 lint／測試
dotnet build backend/AiNexus.slnx         # 0 warning（warning 即錯誤）
dotnet test --solution backend/AiNexus.slnx --no-build --filter "FullyQualifiedName~<Module>"
./scripts/Export-Database.ps1 -Server <開發庫>    # 開發庫結構改完後匯出到 database/App/（詢問 SQL 帳密）
dotnet build database/App                 # 匯出檔建成 dacpac；引用錯誤在這裡失敗
./scripts/Export-Contracts.ps1 -BaseUrl https://localhost:5080   # API 有變動時重產 openapi.json 與 schema.ts
npm --prefix frontend run format          # 前端改完先排版；Verify 會以 format:check 擋下
```

GitHub Actions 只能手動觸發，驗證在本機完成。

## 必須遵守

- 資料庫優先：結構由開發庫匯出到 `database/App/`，EF 只做對應，不用 migration。實體與組態一表一檔放 `AiNexus.Data/App/<Module>/<Entity>.cs`（資料夾＝schema、實體＝表單數名），Features 只引用；組態不寫索引、約束、預設值與說明。
- 一個 use case 一個檔案（handler＋endpoint＋validator），放在 `AiNexus.Features/<Area>/<Module>/`（區見 ADR 0016，只是資料夾、不是依賴邊界；模組名全域唯一）；模組根目錄超過 15 個檔案就依能力分一層子資料夾；namespace＝資料夾。
- 前端 `features/<area>/<module-kebab>/` 與後端區、模組同名（能力子資料夾也同名，唯一例外 `admin/design/`）；選單是資料（`accesscontrol.NavigationItems`），不反映在目錄。跨模組引用用 `@core`／`@shared`／`@features` 別名。
- 頁面與動作只宣告在 `FeatureCatalog`（ID `<module>.<page>[.<action>]`，網址由它推得）；前端路由以 `data: { feature }` 指向它，後端連結用 `RouteOf`，不寫死路徑。
- 預期內的失敗回傳 `Result<T>`，錯誤定義在 `<Module>Errors`；不要為此 throw。
- request body 要有 `RequestValidator<T>`，刻意交給 handler 驗證的標 `[ValidatedInHandler(原因)]`；時間用 `TimeProvider`；使用者用 `ICurrentUser`。
- 設定用 `AddSettings<T, TValidator>(區段)` 註冊（`[OptionsValidator]` 驗證、啟動即檢查），區段名＝模組名；預設值寫進 `appsettings.json`。
- 註解用繁體中文，寫程式碼看不出來的事：為什麼這樣做、邊界與陷阱（游標、時區、URL 同步、權限、失敗即拒絕）；不逐行重述程式在做什麼，公開型別與共用元件一句說明用途。既有英文註解改到該段時順手改寫，不整批翻譯。
- 日誌用 `[LoggerMessage]`，EventId 固定且唯一；NuGet 版本只寫在 `backend/Directory.Packages.props`。
- 跨模組的副作用用 domain event；跨模組讀取只透過對方的 `public` 服務。
- 模組之間不可有循環依賴（`ModuleBoundaryTests`）。
- 不手改 `contracts/openapi.json`、`frontend/src/app/core/api/schema.ts`、`feature-catalog.ts`、`database/App/` 的匯出檔（手寫的只有 `Seed/`、`Data/`、`export-exclusions.txt` 與 `FullText/` 的 `Catalogs/`、`Indexes/` 以外）。
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
| 實體 | 資料表單數名 | `identity.Users` → `User` |
| 資料庫物件、約束 | ADR 0012：`usp_`、`vw_`、`pk_`、`fk_`、`ix_`、`ux_`、`ck_`、`df_` | `ix_Users_LastSeenAt` |
| 區 | 後端 PascalCase、前端小寫：`Work`、`Admin`、`Platform` | `Features/Admin/` → `features/admin/` |
| Angular 功能資料夾 | 區＋後端模組名的 kebab-case | `AccessControl` → `features/admin/access-control` |
| Angular 檔名 | 小寫 kebab-case，不加 `.component` | `chat-workspace.ts` |
| Angular 類別、signal | PascalCase 類別；signal 用名詞，不加 `$` | `ChatStore`、`messages` |
| CSS class | kebab-case | `message-row` |
| PowerShell 腳本 | `scripts/` 只放入口，動詞-名詞或單一動詞；共用函式放 `scripts/AiNexus` 模組，名詞加 `Repo` 前綴 | `Start-Local.ps1`、`Build.ps1`、`Get-RepoLocalPaths` |
| 產品識別 | 只寫成 `AiNexus`、`ainexus`、`AINEXUS`、`ai-nexus`、`AI Nexus`（fork 時由範本替換）；型別、函式、設定鍵不帶產品名 | cookie `AiNexus.Session`、`ai-nexus.appearance` |
| 文件 | `docs/<讀者>/` 下小寫 kebab-case.md；ADR 為 `docs/decisions/<四位編號>-kebab-case.md`；根目錄 `README.md`、`CLAUDE.md` 固定大寫 | `architecture/backend-conventions.md` |
| commit | Conventional Commits，說明用繁體中文 | `perf(inference): 串流只寫入增量` |

改 `docs/` 或任何 Markdown 時，另依 `.claude/rules/docs.md`；改前端畫面時，另依 `.claude/rules/frontend-ui.md`。
