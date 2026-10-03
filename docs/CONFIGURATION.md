# 專案參數與秘密設定

一般設定與秘密分開保存。第一次執行 `scripts/Configure-Local.ps1`、`Start-Local.ps1` 或 `Start-Dev.ps1` 會建立以下本機檔案；Configure 以遮蔽方式詢問 SQL 登入、密碼、AD 服務密碼與 API key，Enter 保留既有值。

| 位置                                                                             | 內容                                     | 版控 |
| -------------------------------------------------------------------------------- | ---------------------------------------- | ---- |
| `backend/src/AiNexus.Api/appsettings.json`                                       | 可公開的預設值、模型能力                 | 是   |
| `appsettings.Local.example.json`／`appsettings.Secrets.example.json`（同資料夾） | 可複製的設定格式，沒有真實帳密           | 是   |
| `.local/config/appsettings.Local.json`                                           | 此機器的 SQL／AD 位址、模型及管理政策    | 否   |
| `.local/secrets/appsettings.Secrets.json`                                        | SQL 帳密、AD 服務密碼、Google key        | 否   |
| `.local/keys`                                                                    | 本機 cookie 加密金鑰，Windows DPAPI 保護 | 否   |

舊的 `backend/src/AiNexus.Api/appsettings.Local.json` 會由啟動腳本拆分並遷移；新檔已有值時保留新值，只補空欄位，完整舊檔備份留在受保護的 `.local/secrets/legacy-settings-*.json`。秘密檔 NTFS ACL 限目前使用者、SYSTEM、Administrators；仍須依公司政策保護電腦與備份。不要把 `.local`、秘密檔或 key ring 放入 `wwwroot`。

## 設定優先順序

後面的來源覆蓋前面：ASP.NET 預設 appsettings 與環境設定 → 本機一般設定 → 本機秘密設定 → 環境變數 → 命令列。設定只在啟動讀取，修改後重新啟動後端。Testing 環境不載入本機設定，測試不使用真實帳密。

本機腳本自動指定 `LocalConfigPath` 與 `SecretsConfigPath`；Production 可用相同參數指向主機上受 ACL 保護的檔案，或用部署系統注入環境變數。階層使用雙底線，例如 `Database__Password`、`Inference__GoogleApiKey`。秘密不要放在命令列、shell history、前端環境檔或 Git。前端只呼叫同源 `/api/v1`，不需要 SQL、AD 或 Google key。

## SQL Server

| 參數                               | 檔案            | 說明                                               |
| ---------------------------------- | --------------- | -------------------------------------------------- |
| `Database.Server`                  | config          | DNS／IP、`host\\instance` 或 `host,port`           |
| `Database.Name`                    | config          | `AiNexus`；本機初始化指令只允許此專用名稱          |
| `Database.TrustServerCertificate`  | config          | 預設 false；只有 Development 可設 true，連線仍加密 |
| `Database.User`／`Password`        | secrets         | 既有 SQL Authentication 登入，不必再建立一組登入   |
| `ConnectionStrings.Nexus`          | secrets（選用） | 完整連線字串；非空時優先於分欄設定                 |
| `Storage.ApplyMigrationsOnStartup` | config          | 預設 false；用初始化／部署操作管理 schema          |

`Initialize-Database.ps1` 會檢查 master，AiNexus 不存在才建立，接著套用尚未執行的 EF migrations；重跑不會清空資料。需要建庫／DDL 權限，若既有登入權限不足，由 DBA 依 [資料庫文件](DATABASE.md) 初始化。正式應用帳號只保留 DML 權限。Production 強制加密且拒絕略過 SQL 憑證驗證，應使用受信任的憑證與符合憑證 SAN 的主機名稱。

## AD 登入

| 參數                                 | 說明                                                                                                                       |
| ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| `AdAuthentication.Mode`              | `Ldap` 顯示公司帳號／密碼登入頁；`Windows` 使用 Negotiate／IIS 整合登入                                                    |
| `AdAuthentication.Url`               | `ldap://ad.company.internal/DC=company,DC=internal`（StartTLS）或 `ldaps://ad.company.internal:636/DC=company,DC=internal` |
| `AdAuthentication.DnUser`            | 服務帳號 DN，例如 `CN=directory-reader,CN=Users,DC=company,DC=internal`                                                    |
| `AdAuthentication.DnPass`            | 放 secrets 的服務帳號密碼                                                                                                  |
| `AdAuthentication.AdAccountAttrName` | 預設 `sAMAccountName`，會 HTML decode 並 trim                                                                              |
| `AdAuthentication.Domain`            | 帳號呈現用的 AD 網域名稱                                                                                                   |

Ldap 模式先以服務帳號搜尋使用者，再以使用者 DN 和個人密碼 bind；以 AD `objectSid` 映射平台使用者。個人密碼只用於當次驗證，不寫 SQL／日誌。StartTLS／LDAPS 信任作業系統憑證；不提供略過 AD 憑證驗證的開關。Windows 模式需另驗 IIS 與瀏覽器政策，見 [IIS 部署](../deploy/iis/README.md)。

## Google AI 與模型政策

`Inference.Provider=google`；`Inference.GoogleApiKey` 只放 secrets。後端使用官方 HTTPS API，以 header 傳 key。系統會把使用者提問、系統指令與目前分支上下文送到 Google。模型清單必須同時存在於管理員核准的 `Inference.Models` 及該 key 可用的模型目錄。

模型控制都在伺服器執行，下列一般設定可直接放 `.local/config/appsettings.Local.json` 的 `Inference` 區段：

```json
{
  "Inference": {
    "Provider": "google",
    "AllowModelSelection": false,
    "ShowModelNames": false,
    "DefaultModelId": "gemma-4-26b-a4b-it"
  }
}
```

| 政策                                          | 結果                                     |
| --------------------------------------------- | ---------------------------------------- |
| AllowModelSelection=true、ShowModelNames=true | 顯示核准模型下拉與名稱                   |
| false、true                                   | 顯示指定模型，不能切換；後端拒絕其他模型 |
| false、false                                  | 顯示「系統指定」，API／歷史訊息使用代號  |
| true、false                                   | 可選「AI 助理 1／2…」，API 使用代號      |

鎖定模型不存在時會停用送出，不能自動換另一個模型。SQL 保留實際 provider ID 供執行與稽核；名稱隱藏涵蓋 catalog、run、message、preferences 回應，不會更改模型自行生成的文字。代號依核准清單順序產生，調整清單後前端應重新載入。

| 模型／執行參數                       | 用途                                                         |
| ------------------------------------ | ------------------------------------------------------------ |
| `Models[].Id`／`DisplayName`         | provider 真實 ID 與可見名稱                                  |
| `ContextTokens`／`MaxOutputTokens`   | 此工作台的上下文與輸出限制；輸出必須小於 context             |
| `SupportsStreaming`／`SupportsUsage` | 能力描述；只有 provider 回傳 usage 才有實際 token 數         |
| `SupportsImages`                     | 明確核准圖片能力；Gemma 範本為 true，其他 profile 預設 false |
| `ReasoningControl`                   | `none`、`google-level`、`ollama-toggle` 或 `ollama-level`    |
| `ReasoningEfforts`                   | 此模型實際支援的選項；後端拒絕未核准值                       |
| `DefaultReasoningEffort`             | 預設 `auto` 或上述選項之一                                   |
| `QueueCapacity`／`TimeoutSeconds`    | 有界佇列容量（包含執行中工作）／生成逾時秒數                 |
| `MaxInputCharacters`／`SystemPrompt` | 輸入字數上限／伺服器系統指令                                 |

目前 Gemma 4 設定為 `google-level`、`["minimal","high"]`、預設 `minimal`。官方 API 的 minimal 關閉 thinking，high 開啟；介面呈現「快速回應／深入思考」，`auto` 不傳 thinking override。依 [Google Gemma 官方說明](https://ai.google.dev/gemma/docs/core/gemma_on_gemini_api#thinking)，不要套用其他模型的思考級別。

切換 `ollama` 時須一起設定 `BaseUrl`、`DefaultModelId` 與完整核准 profiles（陣列覆蓋採 .NET 逐項合併，不是整體替換；若模型數減少請同步修改基礎清單）。預設不宣告思考能力；確認實際裝置 `/api/show` 支援後才設定 toggle 或 level，見 [Ollama thinking](https://docs.ollama.com/capabilities/thinking)。切換 provider／政策後重新啟動，舊的 active run 會依重啟恢復規則結束。

## Context 與診斷

輸入框旁用量包含系統與對話指令、目前分支、草稿及附件，預留輸出 token。文字採保守 UTF-8 byte 預算，圖片採 `Attachments.ImageTokenEstimate`；不是精確 tokenizer 計數。超出預算時先略過最早完整輪次，原歷史仍保留；最新提問仍超限會拒絕送出。生成完成的實際 usage 另存 `GenerationRuns`，不把預估當實測數字。附件格式、配額及保存規則見 [文件與圖片分析](ATTACHMENTS.md)。

`scripts/Test-Connections.ps1` 驗 SQL／EDoc helpers、預設功能關聯、AD 服務 bind 與設定模型的真實串流／思考選項。圖片能力啟用時，另以合成文字文件和純色圖片驗證辨識，不使用個人文件。會使用 Google 配額；SQL 只建立並清理自己的隨機驗證 audit。安全診斷在 `artifacts/connection-checks.json`，個人 AD 登入及跨帳號隔離需另行驗收。
