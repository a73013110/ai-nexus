# AI Nexus：IIS 發版與驗證

此文件對應 `D:\CoreProject\AiNexus\app`、`config`、`keys`、`data/attachments`、`data/diagnostics` 的配置。正式環境使用 **Production**，`config/appsettings.Production.json` 的 `Database.TrustServerCertificate=true` 已可使用自簽 SQL 憑證，連線保持加密；不需要 Development 或額外放行參數。

本版需套用 `20261007040053_SystemDiagnostics`，保留 app 外的診斷 journal，不可與 `logs/stdout` 混用。先建立 diagnostics 目錄及 app pool Modify ACL，按組織政策確認30天診斷／14天已補送檔案／365天稽核預設。Windows emergency source 註冊、SQL離線補送、查證代碼驗收與原生日誌查證見 [DIAGNOSTICS](../../docs/DIAGNOSTICS.md)。此日誌機制不改變目前單 worker 的聊天部署限制。

## 1. 部署目錄與各檔用途

```text
D:\CoreProject\AiNexus\
├─ app\                         ← IIS 網站實體路徑，只指這一層
│  ├─ AiNexus.Api.dll / deps.json / runtimeconfig.json / 依賴套件
│  ├─ appsettings.json          ← 發版預設值，不放真實帳密
│  ├─ web.config                ← ANCM、Production、外部設定位置
│  └─ wwwroot\                  ← 已編譯 Angular；不需在主機啟動 npm
├─ config\
│  ├─ appsettings.Production.json  ← SQL／AD 位址、Host、模型與限額
│  └─ appsettings.Secrets.json     ← SQL 帳密、AD 服務密碼、Google key
├─ keys\                        ← cookie／antiforgery 的 Data Protection 金鑰
├─ data\attachments\            ← 原檔持久儲存，不隨 app 發版替換
├─ data\diagnostics\            ← 每日／大小輪替 JSONL、SQL補送 checkpoint
├─ logs\                        ← 暫時開啟的 ANCM 啟動日誌
└─ app.previous-時間\            ← 選用的上一版檔案，不對外提供
```

不要把 IIS 指到整個 `AiNexus` 根目錄，也不要把 config／keys／data 放入 app 或映射成 IIS 虛擬目錄。`keys` 不是 AD 角色資料；角色與授權在 SQL。更新 app 時保留 keys，可避免所有人因金鑰遺失而被登出。Windows 使用執行身分的 DPAPI 加密金鑰，因此須固定 application pool 身分並載入 user profile。更換主機／身分不能只複製加密後的 XML 就假設可解密；單台可重新建立 key ring 並讓使用者重新登入，多台負載平衡另需規劃共用金鑰、憑證保護與會話路由。

## 2. 主機先備條件

使用支援 .NET 10 的 Windows Server，先啟用 IIS，再安裝 **.NET 10 Hosting Bundle x64**（只裝 SDK 或 Runtime 不足以保證有 ASP.NET Core Module V2）。若 Hosting Bundle 早於 IIS 安裝，重新修復安裝。檢查 `dotnet --list-runtimes` 是否包含 `Microsoft.NETCore.App 10.x` 與 `Microsoft.AspNetCore.App 10.x`。安裝／修復後安排 IIS 重啟。Hosting Bundle、ANCM 與 in-process 的要求見 [Microsoft IIS 指南](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0)。

主機需可連 SQL `192.168.2.95` 的實際 TCP port、AD 的 LDAPS 636 或 LDAP StartTLS 389，以及選用的 Ollama 主機 11434。Ollama 使用 localhost 只適用 IIS 和 GPU 在同台。Google 模式另需可連官方 HTTPS 443；完全本地模式不需 Google key。

網站需公司信任的 HTTPS 憑證、正確 DNS 與 IIS HTTPS binding。這是**網站**憑證，與 SQL 自簽憑證放行分開。Production 的登入與 CSRF cookie 固定 Secure；只用 HTTP 即使看得到頁面，也無法正確維持登入。不要以切 Development 解決。

## 3. 在開發機產生完整套件

```powershell
Set-Location D:\GitProject\ai-nexus
pwsh -NoProfile -File scripts/Verify.ps1
pwsh -NoProfile -File scripts/Publish-IIS.ps1 -SkipBuild -PublishDirectory artifacts/verification
```

套件放 `artifacts/iis/<時間>/`，包含 app／config 範本／空 keys／logs、`Verify-IIS.ps1`、db migration／描述腳本與 docs／deploy 維運文件，不預設攜帶秘密。輸出的 `app` 才是發版成品；已包含前端與後端，IIS 主機不用 Node.js。`PublishDirectory` 預設仍為 `artifacts/publish`；執行完整 Verify 後應明確封裝其 `artifacts/verification` 產物。

若是在受控環境製作含本機設定的內部移轉套件，可以使用 `-IncludeLocalConfig`；這會複製秘密，套件必須全程受 ACL 保護並在移轉完成後依公司政策清理。預設不複製現有 key ring。`-DestinationPath` 指**全新且空的套件 app 目錄**，不是正在運行的網站；腳本拒絕覆蓋非空目錄。既有 config／keys 也不會被這個封裝流程覆蓋。

## 4. 準備外部設定

首次部署將套件 app 複製到 `D:\CoreProject\AiNexus\app`。使用套件 config 範本或保留既有 config，依 [CONFIGURATION](../../docs/CONFIGURATION.md) 填妥。先將 v1／v2 設定遷移至 v3，切勿用空範本覆蓋已填的秘密：

```powershell
pwsh -NoProfile -File scripts/Migrate-Settings.ps1 `
  -SettingsPath 'D:\CoreProject\AiNexus\config\appsettings.Production.json' `
  -SecretsPath 'D:\CoreProject\AiNexus\config\appsettings.Secrets.json'
```

遷移工具在原檔旁留受相同 ACL 保護的原版本備份，保留自訂值及秘密。套件 `scripts/` 已含 `Migrate-Settings.ps1`、`Local-Settings.ps1`、`Settings-Schema.ps1`、`settings-layout.json`；在主機執行時明確指定兩個外部檔案，不使用維運工具目錄的預設 `.local`。

一般檔至少確認：

| 欄位                                                       | 本環境要填                                                                                       |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `ConfigurationVersion`                                     | 3                                                                                                |
| `AllowedHosts`                                             | 實際 IIS DNS Host，例如 `ai.company.internal`；多個用 `;`，不要含 https／port                    |
| `Database.Server`／`Name`                                  | `192.168.2.95`／`AiNexus`，非預設 SQL port 時填 `192.168.2.95,port`                              |
| `Database.TrustServerCertificate`                          | true（依目前內部 SQL 自簽憑證需求）                                                              |
| `AdAuthentication.Mode`                                    | Ldap                                                                                             |
| `AdAuthentication.Url`                                     | `ad.hanglong.com.tw/DC=hanglong,DC=com,DC=tw`，或經確認的 LDAPS 位址                             |
| `AdAuthentication.Domain`                                  | `hanglong.com.tw`                                                                                |
| `AdAuthentication.DnUser`                                  | `CN=hanglong,CN=Users,DC=hanglong,DC=com,DC=tw`                                                  |
| `AdAuthentication.AdAccountAttrName`                       | `sAMAccountName`，不要有尾端空白或 HTML entity                                                   |
| `Administration.BootstrapAdministrators`                   | `["a73013110"]`，一般帳號不會自動取得管理員                                                      |
| `Storage.ApplyMigrationsOnStartup`                         | false                                                                                            |
| `Inference.Providers.<provider>.Enabled`／`MaxConcurrency` | Google／Ollama 可同時啟用，各自並行 1–8；ModelPolicy.DefaultModelId 使用完整 provider/model 路由 |
| `Attachments.StoragePath`                                  | `D:\CoreProject\AiNexus\data\attachments`，必須在 app 外                                         |
| `Diagnostics.Directory`                                    | `D:\CoreProject\AiNexus\data\diagnostics`，實體本機目錄、app外、不可映射為網站URL                    |
| `Attachments.DefaultOwnerLimitBytes`                       | 5000000000（5 GB），個人 override 優先群組與預設                                                 |
| `Attachments.CleanupIntervalMinutes`／`DraftRetentionDays` | 60 分鐘／14 天；定期回收與失敗刪檔重試                                                           |
| `Knowledge.Embedding.Provider`                             | 與對話分開設定；離線使用 ollama 或暫用 none                                                      |

秘密檔填 `Database.User`、`Database.Password`、`AdAuthentication.DnPass`；Google 模式再填 `Inference.Providers.Google.ApiKey`。不要將使用者 AD 密碼保存到設定檔。來源系統另外使用專用唯讀帳號。

若秘密檔另有非空 `ConnectionStrings.Nexus`，它會優先於 Database 分項欄位；自簽 SQL 憑證需在完整字串也設定 `Encrypt=True;TrustServerCertificate=True`。`Encrypt=Strict` 仍會驗證憑證，這種情況不能只修改一般檔的 TrustServerCertificate。

## 5. web.config 的完整定位

以版本庫 `backend/src/AiNexus.Api/web.config` 為唯一範本，發版 SDK 會帶到 app。關鍵內容：

```xml
<aspNetCore processPath="dotnet" arguments=".\AiNexus.Api.dll"
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

## 6. IIS 網站與集區

建立專用 `AiNexus` application pool 與網站；網站 physical path 是 `D:\CoreProject\AiNexus\app`。

| 集區選項                   | 設定與用途                                          |
| -------------------------- | --------------------------------------------------- |
| .NET CLR Version           | No Managed Code（空字串），由 ANCM 載入 .NET        |
| Managed Pipeline           | Integrated                                          |
| Enable 32-Bit Applications | False                                               |
| Identity                   | 固定 ApplicationPoolIdentity 或經規劃的專用服務帳號 |
| Load User Profile          | True，維持 DPAPI 的執行身分                         |
| Maximum Worker Processes   | 1；目前生成佇列與部分寫入鎖在程序內                 |
| Start Mode                 | AlwaysRunning                                       |
| Idle Time-out              | 0，避免使用者閱讀時或背景索引時被閒置停止           |
| Disable Overlapped Recycle | True，部署／回收期間避免舊版與新版並存              |

啟用 IIS Application Initialization，網站／application 設 `preloadEnabled=true`，讓 AlwaysRunning 的集區載入應用。排程回收安排於離峰，先確認沒有生成或索引任務；回收仍會中斷正在生成的回答，租約到期後保留部分輸出並可手動重新生成，不會自動再次付費呼叫模型。

若用 LDAP：**Anonymous Authentication 啟用，Windows Authentication 停用**，由網站登入表單驗證 AD。若改 `Mode=Windows`，需安裝 IIS Windows Authentication role service、啟用 Windows Authentication並設定瀏覽器 Intranet 信任；先確認 `/auth/session` 能拿到身分。每個 in-process 網站使用獨立集區。

在主機管理員的 **Windows PowerShell 5.1** 可設定既有集區（不要在開發機照跑）：

```powershell
Import-Module WebAdministration
$taskPool = 'IIS:\AppPools\AiNexus'
Set-ItemProperty $taskPool managedRuntimeVersion ''
Set-ItemProperty $taskPool enable32BitAppOnWin64 $false
Set-ItemProperty $taskPool processModel.loadUserProfile $true
Set-ItemProperty $taskPool processModel.maxProcesses 1
Set-ItemProperty $taskPool processModel.idleTimeout ([TimeSpan]::Zero)
Set-ItemProperty $taskPool startMode AlwaysRunning
Set-ItemProperty $taskPool recycling.disallowOverlappingRotation $true
```

## 7. 檔案與 SQL 最小權限

集區身分 app 只需 Read／Execute，config 只需 Read，keys／logs／data/attachments 需 Modify（含原檔刪除）。不要讓 Web 身分改寫應用 DLL 或秘密設定。先建立集區，以下以主機管理員執行：

```powershell
New-Item -ItemType Directory -Path 'D:\CoreProject\AiNexus\data\attachments' -Force | Out-Null
$taskPrincipal = 'IIS AppPool\AiNexus'
icacls 'D:\CoreProject\AiNexus\app' /grant:r "${taskPrincipal}:(OI)(CI)RX"
icacls 'D:\CoreProject\AiNexus\config' /inheritance:r `
  /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${taskPrincipal}:(OI)(CI)R"
icacls 'D:\CoreProject\AiNexus\keys' /inheritance:r `
  /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${taskPrincipal}:(OI)(CI)M"
icacls 'D:\CoreProject\AiNexus\logs' /inheritance:r `
  /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${taskPrincipal}:(OI)(CI)M"
icacls 'D:\CoreProject\AiNexus\data\attachments' /inheritance:r `
  /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${taskPrincipal}:(OI)(CI)M"
```

`/grant:r` 只替換指定身分的 grant；若既有檔案有其他**顯式**讀取規則，仍須在 NTFS「進階安全性」檢查並移除未授權的 Users／Everyone，確認兩個 JSON 與備份也繼承正確規則。從開發機複製的 Secrets 檔可能限制為開發者／SYSTEM／Administrators；因此要另給 IIS 集區讀取權，而不是使用本機的 `Protect-NexusSecrets` 重設主機 ACL。

應用 SQL 帳號需專用資料庫的資料讀寫（各功能會 Insert／Update／Delete），不要只給 Users 表讀取權，也不要常態 db_owner。DDL／建庫用 DBA 部署帳號或維運階段的暫時權限。外部公文／校務來源只能給核准 view 的 SELECT，`ApplicationIntent=ReadOnly` 本身不能取代 SQL 權限。

## 8. 空資料庫初始化

本版 migrations 已合併為單一 `20261005171400_InitialCreate`，包含全部 schema、種子、索引、描述、原檔 metadata／配額、provider 與耗時欄位。只適用全新空資料庫；無舊版 binary 遷移或舊 DB 相容。既有其他基線的 history 會被拒絕，初始化不會 DROP、清空資料或重寫 history。日後新增 migration 應延續此基線。

先停止所有共用此 DB 的 host，確認 DBA 準備的 AiNexus 是空庫或不存在。使用有建庫／DDL 權限的維運身分，在主機 PowerShell 7 執行：

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet 'D:\CoreProject\AiNexus\app\AiNexus.Api.dll' `
  --contentRoot 'D:\CoreProject\AiNexus\app' `
  --LocalConfigPath '..\config\appsettings.Production.json' `
  --SecretsConfigPath '..\config\appsettings.Secrets.json' `
  --InitializeDatabase true
if ($LASTEXITCODE -ne 0) { throw '資料庫初始化未完成，先不要啟動網站。' }
```

工具只建立不存在的 AiNexus，套用 InitialCreate 並檢查模型／snapshot 一致與物件描述。同一基線重跑不會重建；DBA 亦可審閱套用 `db/migrations.sql`。完成後移除 runtime 身分的 DDL 權限。SQL Server 2025 額外建立 VECTOR(768)／VECTOR(1024)，舊 SQL 使用 portable 路徑。

runtime 需要 13 個業務 schema 的 DML 與 history 的 SELECT。每個 app 使用一個程序；provider 可各自多個推論 worker，MaxConcurrency 只限制本程序的聊天／模型任務，沒有跨主機全域 GPU 限制。生成租約每 15 秒續期、兩分鐘有效，主機時鐘須同步。

## 9. 分層驗收：確定設定正確

先執行套件附的靜態檢查（appcmd 檢查需管理員權限）：

```powershell
pwsh -NoProfile -File 'D:\Packages\AiNexus\Verify-IIS.ps1' `
  -AppPath 'D:\CoreProject\AiNexus\app' -AppPool AiNexus `
  -BaseUrl 'https://你的實際主機名稱'
```

它檢查必要檔案、Production、外部 v3 設定位置、金鑰及站外原檔目錄、Host、集區與 HTTPS session；不會顯示秘密，也不聲稱讀得到檔案就代表集區身分可讀。

再做 **SQL／設定與原檔 IO 驗證**，不啟動背景 workers、不登入 AD、不產生 AI 回答（僅讀取模型清單）：

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet 'D:\CoreProject\AiNexus\app\AiNexus.Api.dll' `
  --contentRoot 'D:\CoreProject\AiNexus\app' `
  --LocalConfigPath '..\config\appsettings.Production.json' `
  --SecretsConfigPath '..\config\appsettings.Secrets.json' `
  --VerifyDeployment true
```

輸出應有 `environment=Production`、`sqlConnected=true`、`pendingMigrations=0`、`providerAvailable=true`、`availableModelCount>0`、`sqlEncrypted=true`、`trustsSqlCertificate=true`、`adConfigured=true`、各 providers 狀態、模型數、keyRingPath、正確 attachmentStoragePath 與 attachmentStorageWritable=true、`ready=true`。SQL 版本落後或其他基線會輸出原因並退出，模型服務或指定模型不可用會列出 `modelNotice`。檢查會讀取各 provider 模型清單，不產生回答，並寫入／讀取／刪除一個短暫合成原檔 probe；`ready` 是平台狀態，個別群組仍需模型授權。切換 provider 或模型後，於管理頁確認各群組允許的模型，既有白名單不會自動清空或放寬。退出碼 0 才通過。此指令用**目前維運 shell 身分**讀檔，IIS 身分仍需實際網站驗證。

輸出也顯示 embedding provider／維度、webSearchEnabled 與 giteaEnabled，並驗證這些 optional tools 的參數範圍。`ready=true` 只表示核心設定、SQL 與原檔 probe 通過，不代表 Gitea token、搜尋 API 或 embedding 品質已實測通過。

最後用新的無痕瀏覽器驗收：

1. 直接開 `/chat`、`/projects`：未登入會到 `/login?returnUrl=...`，沒有先出現私人工作區。
2. AD 登入後能開對話，重新整理仍登入；登出後私人 URL 再次要求登入。
3. 短回答與「圖解傅立葉轉換」能串流到完成；停止／重新生成都保留歷史。
4. 上傳合成文字檔及圖片，實際模型能力正確；文字模型不應允許圖片。
5. 網路面板 `/api/v1/status` 是 ready；`/health/live` 只有存活，不代表 SQL 或模型就緒。
6. 一般帳號不可開管理 API；管理員查看對話會留稽核。專案／知識權限隔離正常。
7. 回收集區後重新登入／生成可用；config／keys／logs／data 的 URL 無法讀取。
8. 啟用 Google 與 Ollama 時，兩個使用者可同時使用不同 provider；停用其中一台服務仍可選其餘模型，沒有自動 provider fallback。
9. 上傳後 SQL 沒有 bytes、站外出現 opaque .blob；已用／上限／剩餘一致，草稿計入，多處重用不重複計算。管理個人上限調高能超過群組上限，null 恢復繼承。引用中的原檔刪除回應 409，移除引用及刪檔後實體檔與 metadata 消失。
10. 完成、取消及失敗回答可展開耗時／排隊／執行與 tokens，重新整理仍保留；管理者同樣可讀並留下稽核。
11. 總覽在個人／平台範圍、日期與不同幣別間正確切換；先設定測試模型價格，再確認單次／全對話金額及管理 CSV。平台查閱應有稽核。
12. 啟用 Gitea 後用唯讀個人 token 連線、讀取固定 commit 檔案並帶入草稿；回收集區後仍可解密 token。`keys` 同時保護此 token，不能在更新時清空。
13. 如啟用連網搜尋，先完成 [SearXNG／Brave 設定](../../docs/WEB_SEARCH.md)，再測 opt-in、來源與每日配額；未設定時入口停用。自架搜尋仍會向外部搜尋引擎送出公開提問。
14. 若切到本地 embedding，先確認 Ollama 可達、指定模型已安裝且回傳維度正確，再重新索引合成文件及檢查引用；不要把核心 ready 當成向量驗收。

如需真實 AI、AD TLS 與 EF／Dapper 寫入探測，再於受控環境跑 `--VerifyConnections true --VerificationOutput <logs內檔案>`。該工具會發送合成資料並呼叫真實模型；與上面的 VerifyDeployment 模型清單及原檔 IO 探測分開執行。

## 10. 更新與回復

維運窗口先等待背景任務結束，停止網站／集區，保存同一時點的 SQL＋原檔備份組、config、keys 和上一版 app。不要在程序仍鎖住 DLL 時直接覆蓋，也不要刪整個 AiNexus 根目錄。用全新的 release app 資料夾替換舊 app，保持外部 config／keys／logs／data。需移動目錄時先確認 `Resolve-Path` 真的是 `D:\CoreProject\AiNexus\app`，不是 junction 或其他位置。

首次依序：確認空 DB → 外部 v3 設定 → 新 app → 建立站外目錄／ACL → InitialCreate → 部署驗證 → 啟動 IIS → 瀏覽器驗收。日後更新先完成 [SQL＋原檔一致性備份](../../docs/BACKUP.md) 並停止全部 host，再套用相同基線的 migration。日後沒有 schema 變更時省略 migration。使用 `app_offline.htm` 亦可讓 ANCM 停止應用，移除後重啟；不要把該檔留在發版套件。[ANCM 的部署與啟動診斷](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module?view=aspnetcore-10.0) 說明了此機制。

失敗時先停止新版，回復相符的 app／config／SQL／原檔備份組。舊 binary 架構與本版站外原檔架構不相容，不可只回退 DLL 再指向本版 DB。不要未確認就執行 migration Down，也不要混用不同時點的 SQL 和附件；完整還原程序見 [備份與還原](../../docs/BACKUP.md)。keys 保留原位置與保護身分。

## 11. 常見錯誤與診斷

一般操作先用前端 NX 查證代碼在「系統日誌」查詢；必要時擴大時間範圍並檢查補送健康狀態。SQL離線可能使授權與查閱稽核無法保存，此時由授權維運者查站外JSONL、Windows Application `AiNexus.Diagnostics` 與SQL ERRORLOG，不能繞過管理授權。`VerifyDeployment`新增 diagnosticStoragePath、diagnosticStorageWritable、diagnosticCapacityBytes、diagnosticMaxSqlRows與diagnosticOtlpEnabled；它只以呼叫shell身分測試短期IO，IIS帳號權限需另驗。

| 現象                    | 檢查                                                                                                                                                              |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| HTTP 500.30             | Windows Application event log；確認 JSON／外部路徑／ACL／options／DPAPI。暫時把 stdoutLogEnabled 改 true、重現一次，讀 `logs/stdout*.log` 後立即關回 false        |
| 500.31／502.5           | Hosting Bundle、.NET10 runtime、x64、ANCM 與 DLL 完整性                                                                                                           |
| 500.19                  | web.config XML、AspNetCoreModuleV2、IIS 區段鎖定；不要在 app/web.config 強行設被鎖的 authentication 區段                                                          |
| 403／Host 不符          | HTTPS Host 名稱與 AllowedHosts、IIS binding、實際入口是否走 IP                                                                                                    |
| 登入成功又回登入        | 只用 HTTP、Secure cookie 未送出、keys ACL／執行身分變更、多台 key ring 不一致；不是 SQL TrustServerCertificate 問題                                               |
| SQL 憑證錯誤            | 外部 Production 是否真的載入；若完整 ConnectionStrings.Nexus 非空，該字串優先，須自行含 Encrypt=True;TrustServerCertificate=True                                  |
| 模型未就緒              | SQL migrations、服務啟動、Ollama Endpoint 指到哪台、模型已安裝與 profile Id 一致、Google key／quota                                                               |
| 生成中斷，executor_lost | 執行主機停止、心跳逾期、時鐘不同步；確認共用 DB 的所有主機都已更新到租約版本                                                                                      |
| 附件／PDF 匯出錯誤      | Attachments.StoragePath／環境覆寫、站外目錄 Modify ACL／磁碟剩餘、容量／引用／待刪重試；PDF 匯出需該執行身分可用的 Edge／Chromium，伺服器未安裝時改成已部署瀏覽器 |

日誌可能包含部署資訊，只開放維運人員，stdout 不能長期開啟且無內建輪替。一般 request 錯誤帶 `traceId`；生成錯誤另有 run ID／error code 可對照稽核。上線設定與金鑰不是可公開的成果，不上傳 Git。
