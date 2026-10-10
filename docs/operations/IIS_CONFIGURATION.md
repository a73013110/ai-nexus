# IIS 外部設定與 web.config

## 準備外部設定

首次部署將套件 app 複製到 `D:\CoreProject\AiNexus\app`。使用套件 config 範本或保留既有 config，依 [CONFIGURATION](../development/CONFIGURATION.md) 填妥；切勿用空範本覆蓋已填的秘密。設定只有一個版本，沒有遷移工具：區段名稱或欄位寫錯時網站不會啟動，錯誤訊息會指出是哪個鍵（見 [CONFIGURATION 的啟動驗證](../development/CONFIGURATION.md#啟動驗證)）。

一般檔只寫和預設值不同的鍵，至少確認：

| 欄位 | 本環境要填 |
| --- | --- |
| `AllowedHosts` | 實際 IIS DNS Host，例如 `ai.company.internal`；多個用 `;`，不要含 https／port |
| `Database.Server`／`Name` | `192.168.2.95`／`AiNexus`，非預設 SQL port 時填 `192.168.2.95,port` |
| `Database.TrustServerCertificate` | true（依目前內部 SQL 自簽憑證需求） |
| `Identity.ActiveDirectory.Url` | `ad.hanglong.com.tw/DC=hanglong,DC=com,DC=tw`，或經確認的 LDAPS 位址 |
| `Identity.ActiveDirectory.Domain` | `hanglong.com.tw` |
| `Identity.ActiveDirectory.DnUser` | `CN=hanglong,CN=Users,DC=hanglong,DC=com,DC=tw` |
| `Administration.BootstrapAdministrators` | `["a73013110"]`，一般帳號不會自動取得管理員 |
| `Attachments.StoragePath` | `D:\CoreProject\AiNexus\data\attachments`，必須在 app 外 |
| `Diagnostics.Directory` | `D:\CoreProject\AiNexus\data\diagnostics`，實體本機目錄、app 外、不可映射為網站 URL |
| `Inference.Providers.<provider>.Enabled`、`Inference.DefaultModelId` | Google／Ollama 可同時啟用；DefaultModelId 使用完整 `provider/model` 路由 |
| `Knowledge.Embedding.Provider` | 與對話分開設定；離線使用 ollama 或暫用 none |

`Identity.ActiveDirectory.Mode`（Ldap）、`Database.ApplyMigrationsOnStartup`（false）與附件容量沿用預設，不必寫入。

秘密檔填 `Database.User`、`Database.Password`、`Identity.ActiveDirectory.DnPass`；Google 模式再填 `Inference.Providers.Google.ApiKey`。不要將使用者 AD 密碼保存到設定檔。來源系統另外使用專用唯讀帳號。

若秘密檔另有非空 `ConnectionStrings.Nexus`，它會優先於 Database 分項欄位；自簽 SQL 憑證需在完整字串也設定 `Encrypt=True;TrustServerCertificate=True`。`Encrypt=Strict` 仍會驗證憑證，這種情況不能只修改一般檔的 TrustServerCertificate。

## web.config 的完整定位

以版本庫 `backend/src/AiNexus.Host/web.config` 為唯一範本，發版 SDK 會帶到 app。關鍵內容：

```xml
<aspNetCore processPath="dotnet" arguments=".\AiNexus.Host.dll"
            hostingModel="inprocess" stdoutLogEnabled="false"
            stdoutLogFile="..\logs\stdout">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="LocalConfigPath" value="..\config\appsettings.Production.json" />
    <environmentVariable name="SecretsConfigPath" value="..\config\appsettings.Secrets.json" />
    <environmentVariable name="DataProtection__KeyRingPath" value="..\keys" />
    <!-- 選用：需要覆寫外部 JSON 路徑時才加入這個變數 -->
    <!-- <environmentVariable name="Attachments__StoragePath" value="D:\CoreProject\AiNexus\data\attachments" /> -->
  </environmentVariables>
</aspNetCore>
```

外部設定與 keys 的相對位置由 app content root 解析；Attachments.StoragePath 必須為絕對路徑，Attachments\_\_StoragePath 有較高優先權，無需改程式。相對位置由 app content root 解析，不依 shell 的工作目錄。明確指定的設定檔缺失、JSON 錯誤或無讀取權限會讓啟動失敗；修檔後回收集區。ASP.NET Core 不會自動把 app 外的同名 Production 檔當作環境檔，這裡由 AI Nexus 的外部設定載入器處理。不要同時保留 app 內另一份帶秘密的環境檔或在集區設重複環境變數。
