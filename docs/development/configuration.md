# 設定與秘密

- **一份預設值**：`backend/src/AiNexus.Host/appsettings.json` 列出全部鍵與預設值，隨版本更新。
- **機器檔只寫不同的值**：一般設定與秘密各一份，放在 app 與 Git 之外；範本只列每台機器必須填的鍵。
- **只有一個版本**：沒有版本號與遷移工具。區段寫錯或值不合法時，網站與所有 host 指令都不會啟動，錯誤訊息指出是哪個鍵。
- 設定只在啟動時讀取，改檔後重新啟動後端或回收 IIS application pool。前端只呼叫同源 `/api/v1`，不保存 SQL、AD 密碼或 AI key。

## 檔案與載入順序

| 用途 | 本機開發 | IIS | 範本 |
| --- | --- | --- | --- |
| 全部預設值 | `backend/src/AiNexus.Host/appsettings.json` | `app/appsettings.json`（來自發版） | — |
| 此機器的一般設定 | `.local/config/appsettings.Local.json` | `config/appsettings.Production.json` | `appsettings.Local.example.json`、`appsettings.Production.example.json` |
| 密碼與 key | `.local/secrets/appsettings.Secrets.json` | `config/appsettings.Secrets.json` | `appsettings.Secrets.example.json` |
| 登入 cookie 加密金鑰 | `.local/keys` | `keys`（保留，不隨發版覆蓋） | — |

後面的來源優先：`appsettings.json` → 一般設定 → 秘密 → 環境變數 → 命令列。環境變數用 `__` 表示階層，例如 `Database__Password`、`Inference__Providers__Google__ApiKey`。密碼不可放在命令列或前端。

Development 向上尋找 `global.json`，使用工作區 `.local`。Production 找 app 旁邊的 `../config/` 與 `../keys`，不讀工作區 `.local`。Testing 不讀機器設定。`LocalConfigPath`、`SecretsConfigPath`、`DataProtection__KeyRingPath` 可明確指定，相對路徑以 app 的 content root 為準；明確指定的檔案不存在時啟動失敗，避免悄悄用錯環境。載入邏輯在 [`NexusConfiguration`](../../backend/src/AiNexus.Platform/Configuration/NexusConfiguration.cs)。

## 區段與模組

區段名稱與擁有它的模組一致。欄位、預設值與合法範圍寫在對應的 Options 類別（DataAnnotations 屬性），不在這裡重抄。

| 區段 | Options 類別 | 秘密欄位 |
| --- | --- | --- |
| `Database`、`ConnectionStrings` | 啟動時組成連線字串：[`LocalDatabaseSettings`](../../backend/src/AiNexus.Platform/Data/LocalDatabaseSettings.cs) | `User`、`Password`、`ConnectionStrings.*` |
| `Identity.ActiveDirectory` | [`AdAuthenticationOptions`](../../backend/src/AiNexus.Features/Identity/Authentication/AdAuthenticationOptions.cs) | `BindPassword` |
| `Administration` | [`AdministrationOptions`](../../backend/src/AiNexus.Features/Administration/AdministrationOptions.cs) | — |
| `Inference` | [`InferenceOptions`](../../backend/src/AiNexus.Features/Inference/InferenceOptions.cs) | `Providers.Google.ApiKey` |
| `Knowledge` | [`KnowledgeOptions`](../../backend/src/AiNexus.Features/Knowledge/KnowledgeOptions.cs) | — |
| `WebSearch` | [`WebSearchOptions`](../../backend/src/AiNexus.Features/WebSearch/WebSearchOptions.cs) | `ApiKey`（Brave） |
| `Integrations` | [`IntegrationsOptions`](../../backend/src/AiNexus.Features/Integrations/IntegrationsOptions.cs) | `<來源>.Database.User`／`Password` |
| `Repositories.Gitea` | [`GiteaOptions`](../../backend/src/AiNexus.Features/Repositories/GiteaOptions.cs) | —（權杖由使用者連線後加密存 SQL） |
| `Attachments` | [`AttachmentOptions`](../../backend/src/AiNexus.Features/Attachments/AttachmentOptions.cs) | — |
| `Artifacts.Export` | [`ExportOptions`](../../backend/src/AiNexus.Features/Artifacts/ArtifactExport.cs) | — |
| `Monitoring` | [`MonitoringOptions`](../../backend/src/AiNexus.Features/Monitoring/MonitoringOptions.cs) | — |
| `Diagnostics` | [`DiagnosticOptions`](../../backend/src/AiNexus.Platform/Diagnostics/DiagnosticOptions.cs) | — |
| `Security`、`DataProtection`、`AllowedHosts` | Host 與 Platform 直接讀取 | — |

## 啟動驗證

每個 Options 類別以 `AddSettings<T, TValidator>(區段)`（[`SettingsRegistration`](../../backend/src/AiNexus.Platform/Configuration/SettingsRegistration.cs)）註冊：`BindConfiguration` 綁定、`[OptionsValidator]` source generator 產生的驗證器檢查，`ValidateOnStart` 讓錯誤在接受任何請求前出現。

- **未知的鍵**（拼錯、放錯區段、舊版名稱）：綁定失敗，訊息列出找不到的鍵，例如 `... the following properties were not found on the instance of AiNexus.Features.Inference.InferenceOptions: 'Execution'`。
- **不合法的值**：訊息含類別與屬性，例如 `KnowledgeOptions.Retrieval.TopK` 超出範圍，或 `Model 'ollama/qwen3:8b': ContextTokens must be ...`。
- `SettingsTests` 確保每個區段都有產生的驗證器、`appsettings.json` 加上三份範本都能綁定通過，且公開檔案的秘密欄位為空。

新增設定時在模組的 Options 類別加 `public const string Section`、屬性與驗證屬性，跨欄位規則寫在 `IValidatableObject.Validate`；同時在 `appsettings.json` 補上預設值。

## 第一次設定

`./scripts/Configure-Local.ps1` 第一次從範本建立 `.local` 的兩個檔案，再以遮蔽方式詢問 SQL 帳密、AD 服務密碼與 Google key（Enter 保留舊值）；秘密檔只開放給目前使用者、SYSTEM 與 Administrators。其他欄位直接編輯 `.local/config/appsettings.Local.json`。IIS 的步驟見 [IIS 外部設定](../operations/iis-configuration.md)。

## 程式碼表達不了的注意事項

- **SQL 憑證**：`Database.TrustServerCertificate=true` 只略過 SQL 憑證鏈驗證，連線仍加密；Production 也可用，不需把 IIS 設成 Development。非空的 `ConnectionStrings.Nexus` 優先於分項欄位，須含 `Encrypt=True`；`Encrypt=Strict` 會忽略 TrustServerCertificate。[SqlClient 說明](https://learn.microsoft.com/en-us/sql/connect/ado-net/connection-string-syntax?view=sql-server-ver17#use-trustservercertificate)
- **Migration**：`Database.ApplyMigrationsOnStartup` 預設 false；啟動仍檢查 migration 版本，未升級就停止。正式環境以部署步驟套用。
- **模型清單**：`Inference.Providers.<Google|Ollama>.Models` 是以別名為 key 的物件，不是陣列；設定來源依別名合併，所以要改預設模型就改 `default` 項目的欄位，新增模型用新的別名。公開路由是 `google/<原生Id>`、`ollama/<原生Id>`，`Inference.DefaultModelId` 與群組白名單都用路由。停用的 provider 不會出現在模型清單。
- **圖片能力**：Ollama 模型的 `SupportsImages` 省略或 null 時依 `/api/show` 的回報判定，`false` 明確停用，`true` 不能覆蓋供應商回報的不支援。
- **主機位置**：IIS 與 GPU 不在同一台時，`Inference.Providers.Ollama.Endpoint` 的 localhost 指的是 IIS 主機，要改成 GPU 主機位址。`Knowledge.Embedding.Endpoint` 空值沿用 Ollama 端點。
- **完全地端**：停用 `Inference.Providers.Google.Enabled`，embedding 用 ollama 或 none（none 只用全文檢索，SQL Server 需安裝全文元件）。
- **向量設定變更**：embedding 模型、維度、前處理、`Revision` 或切段設定改變時要重建 profile，見 [向量架構](../research/vector-architecture.md)。
- **附件路徑**：`Attachments.StoragePath` 必須是站外絕對路徑；Development 留空時用 `.local/data/attachments`。web.config 可用 `Attachments__StoragePath` 覆寫。
- **其他**：`AllowedHosts` 是 IIS 接受的 Host 名稱（不含 scheme 與 port）。`Security.AllowInsecureLocalhost` 只由 `-Http` 開啟，Production 一律忽略。Google API 與 Brave 只連官方 HTTPS endpoint。
