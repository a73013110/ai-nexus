# IIS 外部設定與 web.config

## 準備外部設定

首次部署將套件 app 複製到 `D:\CoreProject\AiNexus\app`。使用套件 config 範本或保留既有 config，依 [CONFIGURATION](../development/configuration.md) 填妥；切勿用空範本覆蓋已填的秘密。設定只有一個版本，沒有遷移工具：區段名稱或欄位寫錯時網站不會啟動，錯誤訊息會指出是哪個鍵（見 [CONFIGURATION 的啟動驗證](../development/configuration.md#啟動驗證)）。

一般檔只寫和預設值不同的鍵，至少確認：

| 欄位 | 本環境要填 |
| --- | --- |
| `AllowedHosts` | 實際 IIS DNS Host，例如 `ai.company.internal`；多個用 `;`，不要含 https／port |
| `Databases.App.Server`／`Name` | `192.168.2.95`／`AiNexus`，非預設 SQL port 時填 `192.168.2.95,port` |
| `Databases.App.TrustServerCertificate` | true（依目前內部 SQL 自簽憑證需求） |
| `Identity.ActiveDirectory.Domain` | `hanglong.com.tw` |
| `Identity.ActiveDirectory.BindUser` | `hanglong`（服務帳號名稱，自動補 `@Domain`；也可填 UPN、`網域\帳號` 或 DN） |
| `Identity.Bootstrap.Administrators` | `["a73013110"]`，一般帳號不會自動取得管理員 |
| `Files.StoragePath` | `D:\CoreProject\AiNexus\data\attachments`，必須在 app 外 |
| `Diagnostics.Directory` | `D:\CoreProject\AiNexus\data\diagnostics`，實體本機目錄、app 外、不可映射為網站 URL |
| `Inference.Providers.<provider>.Enabled`、`Inference.DefaultModelId` | Google／Ollama 可同時啟用；DefaultModelId 使用完整 `provider/model` 路由 |
| `Knowledge.Embedding.Provider` | 與對話分開設定；離線使用 ollama 或暫用 none |

`Identity.ActiveDirectory.Mode`（Ldap）與附件容量沿用預設，不必寫入。AD 的 `Server` 預設連 `Domain`（DNS 解析到網域控制站）、`SearchBase` 預設由 `Domain` 推出 `DC=…`、`UseLdaps` 預設 false（389 StartTLS），只有要指定網域控制站、縮小搜尋範圍或改走 636 時才填。

秘密檔填 `Databases.App.User`、`Databases.App.Password`（application pool 以 gMSA 或網域身分登入 SQL 時改設 `Databases.App.IntegratedSecurity=true`，帳密留空）、`Identity.ActiveDirectory.BindPassword`；Google 模式再填 `Inference.Providers.Google.ApiKey`。不要將使用者 AD 密碼保存到設定檔。來源系統另外使用專用唯讀帳號；唯一的例外是回寫假日的 `Databases.GdCalendar`，帳號權限見 [假日設定](../features/attendance-calendar.md#部署)，未設定時只停用回寫。

## 外部連結允許清單

選單的外部連結只能指向一般設定 `AccessControl.NavigationLinkHosts` 列出的主機；預設空陣列＝不允許任何外部連結（失敗即拒絕）。範例見 `backend/src/AiNexus.Host/appsettings.Production.example.json`。

- 每項是小寫主機名稱，不含 scheme 與路徑，最多 100 項；格式不合時網站不會啟動。國際化網域寫 punycode（`xn--…`）。
- `*.docs.company.internal` 只比對子網域，不含 `docs.company.internal` 本身，需要時兩者都列。
- 不帶 port 只接受 443；其他 port 寫成 `intranet.company.internal:8443`。
- 環境變數寫成 `AccessControl__NavigationLinkHosts__0`、`__1`…。
- 改完回收集區才生效。主機移出清單後，既有連結會從所有人的選單消失，不必刪資料。

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
    <!-- <environmentVariable name="Files__StoragePath" value="D:\CoreProject\AiNexus\data\attachments" /> -->
  </environmentVariables>
</aspNetCore>
```

外部設定與 keys 的相對位置由 app content root 解析，不依 shell 的工作目錄；`Files.StoragePath` 必須為絕對路徑，`Files__StoragePath` 環境變數優先。明確指定的設定檔缺失、JSON 錯誤或無讀取權限會讓啟動失敗；修檔後回收集區。ASP.NET Core 不會自動把 app 外的同名 Production 檔當作環境檔，這裡由 AI Nexus 的外部設定載入器處理。不要同時保留 app 內另一份帶秘密的環境檔或在集區設重複環境變數。
