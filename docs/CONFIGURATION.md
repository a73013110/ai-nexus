# 設定參數與檔案配置（v3）

`Monitoring` 控制即時營運監控的啟用、工作階段容量、到期秒數與更新間隔；使用一般設定檔，不新增秘密。預設與單一執行個體觀測範圍見 [監控](MONITORING.md)。

設定只由後端在啟動時讀取。前端呼叫同源 `/api/v1`，不保存 SQL、AD 密碼或 AI key。改檔後須重新啟動後端／回收 IIS application pool。

`Diagnostics` 使用一般設定檔與相同外部載入順序；`scripts/settings-layout.json` 已列入其全部欄位。Production 範本日誌目錄為 `D:\CoreProject\AiNexus\data\diagnostics`，不可位於 app／wwwroot；空值使用 ProgramData。保留、容量、佇列、最低等級、採樣與 OTLP 的預設及限制見 [日誌設定](DIAGNOSTICS.md#設定與容量)。診斷 provider 的等級政策由 Diagnostics 控制；Error／Critical 與有代碼的核心拒絕事件不能被舊 Logging 設定或一般採樣略過。外部 secrets、Production 不讀 `.local` 的規則維持相同。

## 檔案與載入順序

| 用途                 | 本機開發                                   | IIS                                                       |
| -------------------- | ------------------------------------------ | --------------------------------------------------------- |
| 版控的公開預設值     | `backend/src/AiNexus.Api/appsettings.json` | `app/appsettings.json`（來自發版）                        |
| 此環境的一般設定     | `.local/config/appsettings.Local.json`     | `config/appsettings.Production.json`                      |
| 密碼與連線秘密       | `.local/secrets/appsettings.Secrets.json`  | `config/appsettings.Secrets.json`                         |
| 登入 cookie 加密金鑰 | `.local/keys`                              | `keys`（保留，不隨發版覆蓋）                              |
| 原始附件             | `.local/data/attachments`                  | `D:\CoreProject\AiNexus\data\attachments`（站外持久目錄） |

後面的來源優先：公開 `appsettings.json` → app 內環境檔 → 外部一般設定 → 外部秘密檔 → 環境變數 → 命令列。環境變數使用 `__` 表示階層；例如 `Database__Password`、`Inference__Providers__Google__ApiKey`。密碼不可放在命令列或前端。

Development 向上尋找 `global.json`，預設使用工作區 `.local`。Production 預設找 app 的旁邊 `../config/appsettings.Production.json`、`../config/appsettings.Secrets.json` 與 `../keys`；不讀工作區的 `.local`。Testing 不讀機器設定。`LocalConfigPath`、`SecretsConfigPath` 和 `DataProtection__KeyRingPath` 可明確指定；相對路徑一律相對 **app 的 content root**。明確指定的設定檔不存在會使啟動失敗，避免悄悄使用錯誤環境。

## 一次設定、快速執行

在專案根目錄用 PowerShell 7.4 以上執行：

```powershell
pwsh -NoProfile -File scripts/Configure-Local.ps1
pwsh -NoProfile -File scripts/Initialize-Database.ps1
pwsh -NoProfile -File scripts/Start-Local.ps1
```

第一個指令以遮蔽方式輸入 SQL 帳密、AD 服務密碼和 Google key；Enter 保留舊值。初始化可重跑，不會清空資料。`Start-Local` 編譯前端並發版後端，**一個後端程序**在 `https://localhost:5080` 同時提供網站與 API；開發機先以 `dotnet dev-certs https --trust` 信任 SDK 憑證。日常使用不需開兩個視窗。若要前端熱更新，再使用 `Start-Dev.ps1` 的開發流程，見 [README](../README.md)。

```powershell
# 已建置時快速啟動
pwsh -NoProfile -File scripts/Start-Local.ps1 -SkipBuild
# 建置、測試與 IIS 套件
pwsh -NoProfile -File scripts/Verify.ps1
pwsh -NoProfile -File scripts/Publish-IIS.ps1 -SkipBuild
```

## 統一順序與舊檔遷移

所有範本與寫檔工具共用 `scripts/settings-layout.json` 的順序：版本／Host → SQL → AD／管理 → 金鑰／安全 → 對話模型 → 提示詞 → 知識 → 工具 → 整合 → 附件／匯出 → 儲存／日誌 → 進階連線字串。子區塊與模型欄位也有固定順序；未知的擴充欄位排序在後，會保留。

```powershell
# 本機：備份 v1／v2，遷移到 v3，再統一欄位順序
pwsh -NoProfile -File scripts/Migrate-Settings.ps1
# IIS：使用主機上的實際檔案，別把秘密複製回 Git
pwsh -NoProfile -File scripts/Migrate-Settings.ps1 `
  -SettingsPath 'D:\CoreProject\AiNexus\config\appsettings.Production.json' `
  -SecretsPath 'D:\CoreProject\AiNexus\config\appsettings.Secrets.json'
```

遷移在原檔旁保存帶原版本的時間戳備份，保留秘密檔 ACL，不輸出值；v3 重跑只格式化。一般檔與秘密檔必須一起遷移，runtime 只接受 v3。工具保留自訂 provider 模型與秘密，將原本選定 provider 轉為 Enabled、預設模型改成 provider-qualified 路由，移除 `Inference.Provider` 及舊 `MaxOwnerBytes`；沒有新容量設定時改用 5 GB。這是設定檔轉換，沒有資料庫資料遷移。備份與實際 Local／Production／Secrets 檔都不進 Git。

## SQL、AD 與平台權限

| 參數                                                            | 放置       | 行為                                                                                   |
| --------------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------------- |
| `Database.Server`                                               | 一般       | IP／DNS、`host\\instance` 或 `host,port`                                               |
| `Database.Name`                                                 | 一般       | 專用 `AiNexus`；初始化工具不處理其他資料庫                                             |
| `Database.TrustServerCertificate`                               | 一般       | **Production 也支援 true**，略過 SQL 憑證鏈驗證；連線仍要求加密                        |
| `Database.ConnectTimeoutSeconds`                                | 一般       | 預設 10 秒                                                                             |
| `Database.User`／`Password`                                     | 秘密       | 使用既有 SQL Authentication 帳號，不會自動新增登入                                     |
| `ConnectionStrings.Nexus`                                       | 秘密，選用 | 非空優先於分欄設定，須 `Encrypt=True` 或 `Strict`；`TrustServerCertificate=True` 可用  |
| `AdAuthentication.Mode`                                         | 一般       | `Ldap`（網站登入）或 `Windows`（IIS 整合驗證）                                         |
| `AdAuthentication.Url`／`Domain`／`DnUser`／`AdAccountAttrName` | 一般       | LDAP 位址含 Base DN、網域、服務帳號 DN、帳號屬性（預設 `sAMAccountName`）              |
| `AdAuthentication.DnPass`                                       | 秘密       | 目錄查詢服務帳號密碼；不是使用者密碼                                                   |
| `Administration.BootstrapAdministrators`                        | 一般       | AD 短帳號陣列；首次 bootstrap 授權，後續用管理後台異動                                 |
| `Storage.ApplyMigrationsOnStartup`                              | 一般       | 預設 false；啟動仍檢查 migration 版本，未升級則停止；正式用獨立部署步驟套用 migrations |

`TrustServerCertificate=true` 只改變 SQL TLS 驗證，與網站 HTTPS、AD TLS、cookie 完全不同。**無需也不應為此將 IIS 設為 Development**。舊 `AllowUntrustedCertificateInProduction` 已移除，遷移工具會清理。AD LDAP 連線使用 TLS，主機須信任 AD 的憑證，見 [IIS 文件](../deploy/iis/README.md)。

分項設定會建立 `Encrypt=True` 的連線。若使用進階完整連線字串，自簽憑證請使用 `Encrypt=True;TrustServerCertificate=True`；`Encrypt=Strict` 會忽略 TrustServerCertificate 並仍驗證憑證，請勿以它搭配略過驗證的需求。[Microsoft SqlClient 憑證設定](https://learn.microsoft.com/en-us/sql/connect/ado-net/connection-string-syntax?view=sql-server-ver17#use-trustservercertificate)

## 對話模型與提示詞

```json
{
  "Inference": {
    "Execution": {
      "QueueCapacity": 16,
      "TimeoutSeconds": 300,
      "MaxInputCharacters": 12000,
      "MaxOutputCharacters": 65536
    },
    "ModelPolicy": {
      "AllowModelSelection": true,
      "ShowModelNames": true,
      "DefaultModelId": "google/gemma-4-26b-a4b-it"
    },
    "Providers": {
      "Google": {
        "Enabled": true,
        "MaxConcurrency": 1,
        "Models": {
          "default": { "Id": "gemma-4-26b-a4b-it", "DisplayName": "Gemma 4" }
        }
      },
      "Ollama": {
        "Enabled": true,
        "MaxConcurrency": 1,
        "Endpoint": "http://localhost:11434/",
        "Models": {
          "default": { "Id": "qwen3:8b", "DisplayName": "Qwen 3" }
        }
      }
    }
  },
  "Prompts": {
    "DefaultSystemInstruction": "請以繁體中文回答，引用資料時指出來源。"
  }
}
```

Google 與 Ollama 可同時 Enabled，不再有全域 provider 開關。每個核准模型的公開路由為 `google/<原生Id>` 或 `ollama/<原生Id>`，模型清單、預設選項、群組白名單及評測使用此路由；即使兩個供應商模型同名也不衝突。傳給 adapter 與價格管理的模型 ID 仍是原生 Id，供應商獨立保存。每個 run 凍結 provider／原生模型，避免設定切換改變已排隊請求。

`Providers.Google.ApiKey` 只在秘密檔，Google API 固定連線到官方 HTTPS endpoint。`Providers.Ollama` 保存地端 Endpoint，不需 Google key。IIS 與 GPU 不在同台時，`localhost` 是 IIS 主機，須改成 GPU 主機位址。各 provider 模型清單平行探測並隔離故障；一個離線仍可使用其餘模型，指定故障模型回應 503，不會自動改用其他供應商傳送資料。

`Models` 是以別名為 key 的物件，**不是陣列**。`default` 是範本的預設模型項目；更換模型時更新它的原生 `Id`，並更新 `ModelPolicy.DefaultModelId` 的完整路由。增加模型可加 `secondary` 等穩定別名。不要在別名使用 `:`；原生 Id 可以有 `:`，例如 `qwen3:8b`。設定來源依 key 合併，範本中已有的 `default` 仍會保留，應直接修改該項目。上例省略能力欄位，完整範本見 `appsettings.Production.example.json`。

每個 profile 含 `Id`、`DisplayName`、`ContextTokens`、`MaxOutputTokens`、`SupportsStreaming`、`SupportsUsage`、`SupportsImages`、`ReasoningControl`、`ReasoningEfforts`、`DefaultReasoningEffort`。Context 範圍 1,024–32,768，輸出 token 必須小於 Context。模型須同時通過設定核准、實際安裝／API 可用性及群組政策。Ollama 的 `SupportsImages=null`（或省略）表示由 `/api/show` 的 `capabilities` 自動判定，`false` 明確停用；`true` 也不能覆蓋供應商已回報不支援 vision 的模型。能力讀取失敗時停止圖片使用，文字仍可使用；結果短暫快取。Google 沿用管理者設定的圖片能力。圖片須使用實際含 vision／projector 的模型。思考模式只在模型實際支援時開啟。

`AllowModelSelection=false` 固定系統指定模型，`ShowModelNames=false` 隱藏名稱與 provider。逾時範圍 5–600 秒、排隊容量 1–64、輸入 100–32,000 字元、輸出 4,096–262,144 字元。各 provider 的 `MaxConcurrency` 為 1–8，聊天排程按 provider 分開，OCR／文字工具／評測與聊天共用 provider 容量；Google 與 Ollama 可同時執行。QueueCapacity 是整體待排隊容量，並行容量仍是每個 app 程序的設定，不是跨 IIS 實例的全域 GPU 限制。提示詞與參考文件也佔 Context；Context 使用量為估算，完成後另記錄實際 token 與耗時。

網頁中的模型名稱一律使用設定的 `DisplayName`，不顯示路由或原生 ID，也不自動附加供應商名稱。請為不同用途或供應商設定易辨識的 DisplayName。聊天、分享、評測、review、Token 用量與費用統計共用後端 `ModelPresentation`；價格與計費則依供應商解析原生 ID。未提供名稱或已移除的模型使用通用標籤，前端不以完整 ID 補值。`ShowModelNames=false` 的一般使用者仍顯示匿名助理名稱。

## 向量設定獨立於對話

| 區塊                  | 參數                                                                                                                                                        |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Knowledge.Embedding` | Provider=ollama、Model=bge-m3、Dimensions=1024、InputFormat=plain、QueryInstruction=Given a web search query, retrieve relevant passages that answer the query、Revision=""、Endpoint=""、TimeoutSeconds=60、MaxDailyRequests=20000、BatchSize=16、MaxConcurrentBatches=1、AutoActivate=false、RetiredRetentionDays=7 |
| `Knowledge.Indexing`  | MaxCollections=30、MaxDocumentsPerCollection=100、ChunkTargetTokens=450、ChunkMaxTokens=700、ChunkMinTokens=80、ChunkOverlapRatio=0.12 |
| `Knowledge.Retrieval` | Mode=hybrid、VectorCandidates=40、FtsCandidates=40、RrfK=60、VectorWeight=1、FtsWeight=1、RerankCandidates=30、TopK=6、MaxChunksPerDocument=3、MinVectorScore=0、ContextTokens=3500、QueryCacheMinutes=10 |
| `Knowledge.Rerank` | Provider=none、Endpoint=""、Model=bge-reranker-v2-m3、TimeoutSeconds=10、MinScore=0、FailurePolicy=skip |
| `Knowledge.QueryRewrite` | Enabled=true、MaxTurns=4、TimeoutSeconds=5 |

Embedding 支援 google／ollama／none，Mode 支援 hybrid／vector／keyword；none 強制全文，正式環境須有 SQL 全文元件。Endpoint 空值沿用 Inference.Providers.Ollama 端點，填值可獨立移至另一台主機。Rerank 支援 none／tei／openai-compatible，非 none 必填合法 HTTP(S) Endpoint；FailurePolicy 支援 skip／fail。完全地端另停用 Inference.Providers.Google.Enabled 並選核准 Ollama 聊天模型。

啟動驗證維度只允許 768／1024、批次 1–128、併發 1–8、候選 1–200、TopK 1–20 且不超過重排候選、重疊 0–0.3、ContextTokens 100–16000、快取 0–60 分鐘（0 停用），並檢查最小／目標／最大 token 次序及有限分數。MinVectorScore=0 表示不過濾。QueryRewrite 在既有 Knowledge.QueryRewrite 區塊綁定，只在有歷史時用本機核准模型，非額外一般設定頁功能。

模型／維度／前處理／Revision 或切段設定變更需重建 profile；管理端完整覆蓋後切換。設定鏈含正式 appsettings、兩個 example、settings-layout／schema／遷移腳本；Migrate-Settings 將舊字元切段及 context 近似換成 token、移除 PortableCandidateLimit／UseNativeVector。它只轉設定，不提供舊索引查詢。既有本機設定會保留明確值，不會因新預設而自動改成 Ollama。完整流程見 [架構](VECTOR_ARCHITECTURE.md)、[模型](EMBEDDING_MODELS.md)、[本機 AI](LOCAL-AI.md)。

## 工具與程式庫 connector

`Tools.WebSearch` 與 AI 推論供應商分開，包含 `Enabled`、`Provider`（searxng／brave）、SearXNG 的 `Endpoint`、`TimeoutSeconds`（2–30）、`MaxResults`（1–8）、`MaxDailyRequests`（1–10000）。Brave 的 `ApiKey` 只在秘密檔 `Tools.WebSearch.ApiKey`；遷移工具會移出誤放在一般檔的 key。搜尋預設停用，未配置不自動換用 Google。見 [連網搜尋](WEB_SEARCH.md)。

`Integrations.Connectors.Gitea` 保存 `Enabled`、`BaseUrl`、`TimeoutSeconds`（2–30）、`MaxFileBytes`（1024–500000）。BaseUrl 使用 HTTPS（loopback 可用 HTTP）。每個人的唯讀權杖由使用者在網頁連線，經後端加密存於 SQL，沒有共用權杖設定值；IIS 更新須保留 key ring。見 [Gitea](GITEA.md)。

模型與工具價格以管理頁中的不可變 SQL 版本維護，沒有散落在各供應商 JSON 的價格欄位；能追蹤生效時間與每次呼叫的快照。見 [費用](BILLING.md)。

## 系統整合與其他限制

每個來源位於 `Integrations.Sources.Gdweb`／`Meiho`，包含 `Enabled`、`Transport`、`AclContractConfirmed`、`AllowedGroupIds`、`CommandTimeoutSeconds`、`MaxResults` 與自己的 `Database` 區塊。來源 SQL 的 Server／Name／憑證設定放一般檔，User／Password 放秘密檔。舊 `ConnectionStrings.LegacyGdweb`／`LegacyMeiho` 保留作進階相容方式。

`Transport=sql` 是目前已實作的唯讀 adapter。未來可在來源下擴充 API 的專屬設定與 adapter；目前若指定其他 transport，來源清楚顯示尚未支援，不會悄悄改用 SQL。來源端必須提供逐筆 ACL view，平台群組只是第一層門檻；見 [INTEGRATIONS](INTEGRATIONS.md)。

`Attachments.StoragePath` 必須為網站目錄外的絕對路徑，正式外部 JSON 設為 `D:\CoreProject\AiNexus\data\attachments`；web.config 可加 `Attachments__StoragePath` 環境變數覆寫。`DefaultOwnerLimitBytes=5000000000` 為預設容量，個人設定優先於群組與預設。`CleanupIntervalMinutes=60` 與 `DraftRetentionDays=14` 控制定期回收。其餘檔案驗證見 [附件](ATTACHMENTS.md)；部署 ACL 及一致性備份見 [IIS](../deploy/iis/README.md)、[備份](BACKUP.md)。

`Exports` 管理 PDF 匯出瀏覽器及逾時。`AllowedHosts` 是 IIS 接受的實際 Host 名稱（不含 scheme／port）。`Security.DisableHttpsRedirection` 預設 false；停用轉址不會停用 Production 的 Secure cookie。`Logging.LogLevel` 控制日誌，不記錄密碼、key、完整提問或回答。
