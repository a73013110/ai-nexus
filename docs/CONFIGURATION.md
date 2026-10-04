# 設定參數與檔案配置（v2）

設定只由後端在啟動時讀取。前端呼叫同源 `/api/v1`，不保存 SQL、AD 密碼或 AI key。改檔後須重新啟動後端／回收 IIS application pool。

## 檔案與載入順序

| 用途                 | 本機開發                                   | IIS                                  |
| -------------------- | ------------------------------------------ | ------------------------------------ |
| 版控的公開預設值     | `backend/src/AiNexus.Api/appsettings.json` | `app/appsettings.json`（來自發版）   |
| 此環境的一般設定     | `.local/config/appsettings.Local.json`     | `config/appsettings.Production.json` |
| 密碼與連線秘密       | `.local/secrets/appsettings.Secrets.json`  | `config/appsettings.Secrets.json`    |
| 登入 cookie 加密金鑰 | `.local/keys`                              | `keys`（保留，不隨發版覆蓋）         |

後面的來源優先：公開 `appsettings.json` → app 內環境檔 → 外部一般設定 → 外部秘密檔 → 環境變數 → 命令列。環境變數使用 `__` 表示階層；例如 `Database__Password`、`Inference__Providers__Google__ApiKey`。密碼不可放在命令列或前端。

Development 向上尋找 `global.json`，預設使用工作區 `.local`。Production 預設找 app 的旁邊 `../config/appsettings.Production.json`、`../config/appsettings.Secrets.json` 與 `../keys`；不讀工作區的 `.local`。Testing 不讀機器設定。`LocalConfigPath`、`SecretsConfigPath` 和 `DataProtection__KeyRingPath` 可明確指定；相對路徑一律相對 **app 的 content root**。明確指定的設定檔不存在會使啟動失敗，避免悄悄使用錯誤環境。

## 一次設定、快速執行

在專案根目錄用 PowerShell 7.4 以上執行：

```powershell
pwsh -NoProfile -File scripts/Configure-Local.ps1
pwsh -NoProfile -File scripts/Initialize-Database.ps1
pwsh -NoProfile -File scripts/Start-Local.ps1
```

第一個指令以遮蔽方式輸入 SQL 帳密、AD 服務密碼和 Google key；Enter 保留舊值。初始化可重跑，不會清空資料。`Start-Local` 編譯前端並發版後端，**一個後端程序**在 `http://localhost:5080` 同時提供網站與 API；日常使用不需開兩個視窗。若要前端熱更新，再使用 `Start-Dev.ps1` 的開發流程，見 [README](../README.md)。

```powershell
# 已建置時快速啟動
pwsh -NoProfile -File scripts/Start-Local.ps1 -SkipBuild
# 建置、測試與 IIS 套件
pwsh -NoProfile -File scripts/Verify.ps1
pwsh -NoProfile -File scripts/Publish-IIS.ps1 -SkipBuild
```

## 統一順序與舊檔遷移

所有範本與寫檔工具共用 `scripts/settings-layout.json` 的順序：版本／Host → SQL → AD／管理 → 金鑰／安全 → 對話模型 → 提示詞 → 知識 → 整合 → 附件／匯出 → 儲存／日誌 → 進階連線字串。子區塊與模型欄位也有固定順序；未知的擴充欄位排序在後，會保留。

```powershell
# 本機：備份 v1，遷移到 v2，再統一欄位順序
pwsh -NoProfile -File scripts/Migrate-Settings.ps1
# IIS：使用主機上的實際檔案，別把秘密複製回 Git
pwsh -NoProfile -File scripts/Migrate-Settings.ps1 `
  -SettingsPath 'D:\CoreProject\AiNexus\config\appsettings.Production.json' `
  -SecretsPath 'D:\CoreProject\AiNexus\config\appsettings.Secrets.json'
```

遷移在原檔旁保存 `.v1-時間.bak`，保留秘密檔 ACL，不輸出值；v2 重跑只格式化。舊欄位仍有相容讀取，但請將一般檔與秘密檔 **一起遷移**；同時保留 v1 與 v2 的同一參數容易造成覆蓋混淆。未知 `ConfigurationVersion` 會被拒絕。備份與實際 Local／Production／Secrets 檔都不進 Git。

## SQL、AD 與平台權限

| 參數                                                            | 放置       | 行為                                                                                  |
| --------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------------------- |
| `Database.Server`                                               | 一般       | IP／DNS、`host\\instance` 或 `host,port`                                              |
| `Database.Name`                                                 | 一般       | 專用 `AiNexus`；初始化工具不處理其他資料庫                                            |
| `Database.TrustServerCertificate`                               | 一般       | **Production 也支援 true**，略過 SQL 憑證鏈驗證；連線仍要求加密                       |
| `Database.ConnectTimeoutSeconds`                                | 一般       | 預設 10 秒                                                                            |
| `Database.User`／`Password`                                     | 秘密       | 使用既有 SQL Authentication 帳號，不會自動新增登入                                    |
| `ConnectionStrings.Nexus`                                       | 秘密，選用 | 非空優先於分欄設定，須 `Encrypt=True` 或 `Strict`；`TrustServerCertificate=True` 可用 |
| `AdAuthentication.Mode`                                         | 一般       | `Ldap`（網站登入）或 `Windows`（IIS 整合驗證）                                        |
| `AdAuthentication.Url`／`Domain`／`DnUser`／`AdAccountAttrName` | 一般       | LDAP 位址含 Base DN、網域、服務帳號 DN、帳號屬性（預設 `sAMAccountName`）             |
| `AdAuthentication.DnPass`                                       | 秘密       | 目錄查詢服務帳號密碼；不是使用者密碼                                                  |
| `Administration.BootstrapAdministrators`                        | 一般       | AD 短帳號陣列；首次 bootstrap 授權，後續用管理後台異動                                |
| `Storage.ApplyMigrationsOnStartup`                              | 一般       | 預設 false；正式用獨立部署步驟套用 migrations                                         |

`TrustServerCertificate=true` 只改變 SQL TLS 驗證，與網站 HTTPS、AD TLS、cookie 完全不同。**無需也不應為此將 IIS 設為 Development**。舊 `AllowUntrustedCertificateInProduction` 已移除，遷移工具會清理。AD LDAP 連線使用 TLS，主機須信任 AD 的憑證，見 [IIS 文件](../deploy/iis/README.md)。

分項設定會建立 `Encrypt=True` 的連線。若使用進階完整連線字串，自簽憑證請使用 `Encrypt=True;TrustServerCertificate=True`；`Encrypt=Strict` 會忽略 TrustServerCertificate 並仍驗證憑證，請勿以它搭配略過驗證的需求。[Microsoft SqlClient 憑證設定](https://learn.microsoft.com/en-us/sql/connect/ado-net/connection-string-syntax?view=sql-server-ver17#use-trustservercertificate)

## 對話模型與提示詞

```json
{
  "Inference": {
    "Provider": "ollama",
    "Execution": {
      "QueueCapacity": 16,
      "TimeoutSeconds": 300,
      "MaxInputCharacters": 12000,
      "MaxOutputCharacters": 65536
    },
    "ModelPolicy": { "AllowModelSelection": false, "ShowModelNames": false },
    "Providers": {
      "Ollama": {
        "Endpoint": "http://localhost:11434/",
        "DefaultModelId": "qwen3:8b"
      }
    }
  },
  "Prompts": {
    "DefaultSystemInstruction": "請以繁體中文回答，引用資料時指出來源。"
  }
}
```

`Provider` 只選擇對話供應商。`Providers.Google` 保存 Google 的 `DefaultModelId`、`Models`，其 `ApiKey` 只在秘密檔。Google API 固定連線到官方 HTTPS endpoint。`Providers.Ollama` 保存地端 `Endpoint`、`DefaultModelId`、`Models`，不需 Google key。IIS 與 GPU 不在同台時，`localhost` 是 IIS 主機，須改成 GPU 主機位址。

`Models` 是以別名為 key 的物件，**不是陣列**。`default` 是範本的預設模型項目；更換模型時更新它的 `Id`，並更新供應商的 `DefaultModelId`。增加模型可加 `secondary` 等穩定別名。不要在別名使用 `:`；實際模型 `Id` 可以有 `:`，例如 `qwen3:8b`。ASP.NET 設定來源依 key 合併，範本中已有的 `default` 仍會保留，應直接修改該項目。

每個 profile 含 `Id`、`DisplayName`、`ContextTokens`、`MaxOutputTokens`、`SupportsStreaming`、`SupportsUsage`、`SupportsImages`、`ReasoningControl`、`ReasoningEfforts`、`DefaultReasoningEffort`。Context 範圍 1,024–32,768，輸出 token 必須小於 Context。模型須同時通過設定核准、實際安裝／API 可用性及群組政策。文字 Qwen profile 不應宣告圖片能力；圖片須配置支援 vision 的模型。思考模式只在模型實際支援時開啟。

`AllowModelSelection=false` 固定系統指定模型，`ShowModelNames=false` 隱藏名稱。逾時範圍 5–600 秒、排隊容量 1–64、輸入 100–32,000 字元、輸出 4,096–262,144 字元。排隊容量不是 GPU 並行數。提示詞與參考文件也佔 Context；Context 使用量為估算，完成後另記錄模型實際回報用量。

## 向量設定獨立於對話

| 區塊                  | 參數                                                                                                        |
| --------------------- | ----------------------------------------------------------------------------------------------------------- |
| `Knowledge.Embedding` | `Provider`（google／ollama／none）、`Model`、`Dimensions`（目前 768）、`TimeoutSeconds`、`MaxDailyRequests` |
| `Knowledge.Indexing`  | `MaxCollections`、`MaxDocumentsPerCollection`、`ChunkCharacters`、`ChunkOverlap`                            |
| `Knowledge.Retrieval` | `UseNativeVector`、`PortableCandidateLimit`、`TopK`、`ContextCharacters`                                    |

對話用 Ollama 不會自動改變向量供應商。完全地端須同時設定 `Inference.Provider=ollama` 與 `Knowledge.Embedding.Provider=ollama`；`none` 改用既有關鍵字檢索。換 embedding model 必須重建索引，舊模型的向量不能與新模型混用，即使維度相同。後端驗證回傳長度、有限數值並正規化，原生 SQL 欄位目前為 `VECTOR(768)`。本地規劃與硬體建議見 [LOCAL-AI](LOCAL-AI.md)。

## 系統整合與其他限制

每個來源位於 `Integrations.Sources.Gdweb`／`Meiho`，包含 `Enabled`、`Transport`、`AclContractConfirmed`、`AllowedGroupIds`、`CommandTimeoutSeconds`、`MaxResults` 與自己的 `Database` 區塊。來源 SQL 的 Server／Name／憑證設定放一般檔，User／Password 放秘密檔。舊 `ConnectionStrings.LegacyGdweb`／`LegacyMeiho` 保留作進階相容方式。

`Transport=sql` 是目前已實作的唯讀 adapter。未來可在來源下擴充 API 的專屬設定與 adapter；目前若指定其他 transport，來源清楚顯示尚未支援，不會悄悄改用 SQL。來源端必須提供逐筆 ACL view，平台群組只是第一層門檻；見 [INTEGRATIONS](INTEGRATIONS.md)。

`Attachments` 管理檔案、訊息、個人容量、PDF 頁數、文字擷取、圖片 token 估算與草稿保留天數。`Exports` 管理 PDF 匯出瀏覽器及逾時。`AllowedHosts` 是 IIS 接受的實際 Host 名稱（不含 scheme／port）。`Security.DisableHttpsRedirection` 預設 false；停用轉址不會停用 Production 的 Secure cookie。`Logging.LogLevel` 控制日誌，不記錄密碼、key、完整提問或回答。
