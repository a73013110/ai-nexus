# 0013 資料存取專案與範本命名

- **狀態**：提議（2026-10-18）
- **決定**：
  - 新增 `AiNexus.Data` 專案，依「資料庫 → 模組（schema）→ 資料表」放實體、組態與 DbContext；use case 仍依模組放在 `AiNexus.Features`，只引用、不定義資料表。
  - 型別名稱不帶產品名；產品名只以 `AiNexus` 出現在專案名與 namespace，由 `dotnet new` 範本在 fork 時替換。
- **修訂**：[ADR 0002](0002-vertical-slice-single-features-project.md)（後端由三個專案變四個：Host → Features → Data → Platform）。

## 目錄

```
backend/src/
  AiNexus.Platform/Data/        通用基底，不認識任何特定資料庫：ISqlDatabase、DatabaseOptions、SoftDelete
  AiNexus.Data/
    App/                        主資料庫
      AppDbContext.cs
      AuditEventInterceptor.cs  只屬於這個資料庫的元件放在資料庫資料夾根目錄
      Identity/User.cs          一表一檔：實體＋IEntityTypeConfiguration
      Identity/UserRole.cs
      Conversations/Conversation.cs
    Erp/                        另一個讀寫資料庫
      ErpDbContext.cs
  AiNexus.Features/Administration/Users/ListAdminUsers.cs   ← 引用 AppDbContext 與 User
database/App/identity/Tables/Users.sql                      ← 與 Data/App/Identity/User.cs 一一對應
```

- 知道表名就知道檔案：`identity.Users` → `AiNexus.Data/App/Identity/User.cs` → `database/App/identity/Tables/Users.sql`。
- 欄位用到的列舉與值型別（例如 `UserPreferences`）跟著實體放在 `AiNexus.Data`。
- 架構測試：`AiNexus.Features` 不得出現 `IEntityTypeConfiguration`；每個實體檔的資料夾與資料表 schema 一致。

## 為什麼獨立成專案

- 一張表常被多個功能使用。組態散在各功能時，多人開發很容易重複定義或找不到；集中後每張表只有一個位置。
- 放在 `AiNexus.Features` 底下的資料夾無法阻止實體引用模組型別，一引用就會和模組形成循環；獨立專案由編譯器保證實體只依賴 Platform。
- 現在的 `Persistence/` 混著 DbContext、攔截器、健康檢查、發布工具與 SQLite 相容碼，看不出哪些屬於主資料庫、哪些是通用元件；新結構依「通用（Platform）／某個資料庫（Data/<庫>）」分開。

## 命名

- **實體＝資料表單數名**：`Users` → `User`，`NexusUser` 改為 `User`。
- **只有代表某個資料庫的型別帶資料庫名**：`AppDbContext`、`ErpDbContext`、`MasterDatabase`。主資料庫用角色名 `App`：fork 後是另一個資料庫，但角色相同，元件不必改名。
- **其他型別一律中性**：`NexusProblemWriter` → `ProblemWriter`、`NexusConfiguration` → `AppConfiguration`；PowerShell 函式的 `-Nexus` 也一併改為中性名稱。
- **fork 用 `dotnet new` 範本**：`template.json` 設 `"sourceName": "AiNexus"`，執行 `dotnet new ainexus -n Contoso` 時，檔名、namespace、專案名中的 `AiNexus` 會自動替換。不必維護改名腳本。

## 何時重新評估

`AiNexus.Data` 超過約 150 張表、編譯時間明顯變慢，或某個資料庫需要獨立部署時，再依資料庫拆成多個專案。
