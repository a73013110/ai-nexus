# AI Nexus：IIS 發版與驗證

此文件對應 `D:\CoreProject\AiNexus\app`、`config`、`keys` 的現有配置。正式環境使用 **Production**，`config/appsettings.Production.json` 的 `Database.TrustServerCertificate=true` 已可使用自簽 SQL 憑證，連線保持加密；不需要 Development 或額外放行參數。

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
├─ logs\                        ← 暫時開啟的 ANCM 啟動日誌
└─ app.previous-時間\            ← 選用的上一版檔案，不對外提供
```

不要把 IIS 指到整個 `AiNexus` 根目錄，也不要把 config／keys 放入 wwwroot。`keys` 不是 AD 角色資料；角色與授權在 SQL。更新 app 時保留 keys，可避免所有人因金鑰遺失而被登出。Windows 使用執行身分的 DPAPI 加密金鑰，因此須固定 application pool 身分並載入 user profile。更換主機／身分不能只複製加密後的 XML 就假設可解密；單台可重新建立 key ring 並讓使用者重新登入，多台負載平衡另需規劃共用金鑰、憑證保護與會話路由。

## 2. 主機先備條件

使用支援 .NET 10 的 Windows Server，先啟用 IIS，再安裝 **.NET 10 Hosting Bundle x64**（只裝 SDK 或 Runtime 不足以保證有 ASP.NET Core Module V2）。若 Hosting Bundle 早於 IIS 安裝，重新修復安裝。檢查 `dotnet --list-runtimes` 是否包含 `Microsoft.NETCore.App 10.x` 與 `Microsoft.AspNetCore.App 10.x`。安裝／修復後安排 IIS 重啟。Hosting Bundle、ANCM 與 in-process 的要求見 [Microsoft IIS 指南](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0)。

主機需可連 SQL `192.168.2.95` 的實際 TCP port、AD 的 LDAPS 636 或 LDAP StartTLS 389，以及選用的 Ollama 主機 11434。Ollama 使用 localhost 只適用 IIS 和 GPU 在同台。Google 模式另需可連官方 HTTPS 443；完全本地模式不需 Google key。

網站需公司信任的 HTTPS 憑證、正確 DNS 與 IIS HTTPS binding。這是**網站**憑證，與 SQL 自簽憑證放行分開。Production 的登入與 CSRF cookie 固定 Secure；只用 HTTP 即使看得到頁面，也無法正確維持登入。不要以切 Development 解決。

## 3. 在開發機產生完整套件

```powershell
Set-Location D:\GitProject\ai-nexus
pwsh -NoProfile -File scripts/Verify.ps1
pwsh -NoProfile -File scripts/Publish-IIS.ps1 -SkipBuild
```

套件放 `artifacts/iis/<時間>/`，包含 app／config 範本／空 keys／logs 與 `Verify-IIS.ps1`，不預設攜帶秘密。輸出的 `app` 才是發版成品；已包含前端與後端，IIS 主機不用 Node.js。

若是在受控環境製作含本機設定的內部移轉套件，可以使用 `-IncludeLocalConfig`；這會複製秘密，套件必須全程受 ACL 保護並在移轉完成後依公司政策清理。預設不複製現有 key ring。`-DestinationPath` 指**全新且空的套件 app 目錄**，不是正在運行的網站；腳本拒絕覆蓋非空目錄。既有 config／keys 也不會被這個封裝流程覆蓋。

## 4. 準備外部設定

首次部署將套件 app 複製到 `D:\CoreProject\AiNexus\app`。使用套件 config 範本或保留既有 config，依 [CONFIGURATION](../../docs/CONFIGURATION.md) 填妥。先將 v1 檔遷移，切勿用空範本覆蓋已填的秘密：

```powershell
pwsh -NoProfile -File scripts/Migrate-Settings.ps1 `
  -SettingsPath 'D:\CoreProject\AiNexus\config\appsettings.Production.json' `
  -SecretsPath 'D:\CoreProject\AiNexus\config\appsettings.Secrets.json'
```

遷移工具在原檔旁留受相同 ACL 保護的 v1 備份，保留自訂值及秘密。主機未放專案 scripts 時，可從開發機移轉 `Migrate-Settings.ps1`、`Local-Settings.ps1`、`Settings-Schema.ps1`、`settings-layout.json` 到維運工具目錄，再明確指定兩個外部檔案。

一般檔至少確認：

| 欄位                                     | 本環境要填                                                                    |
| ---------------------------------------- | ----------------------------------------------------------------------------- |
| `ConfigurationVersion`                   | 2                                                                             |
| `AllowedHosts`                           | 實際 IIS DNS Host，例如 `ai.company.internal`；多個用 `;`，不要含 https／port |
| `Database.Server`／`Name`                | `192.168.2.95`／`AiNexus`，非預設 SQL port 時填 `192.168.2.95,port`           |
| `Database.TrustServerCertificate`        | true（依目前內部 SQL 自簽憑證需求）                                           |
| `AdAuthentication.Mode`                  | Ldap                                                                          |
| `AdAuthentication.Url`                   | `ad.hanglong.com.tw/DC=hanglong,DC=com,DC=tw`，或經確認的 LDAPS 位址          |
| `AdAuthentication.Domain`                | `hanglong.com.tw`                                                             |
| `AdAuthentication.DnUser`                | `CN=hanglong,CN=Users,DC=hanglong,DC=com,DC=tw`                               |
| `AdAuthentication.AdAccountAttrName`     | `sAMAccountName`，不要有尾端空白或 HTML entity                                |
| `Administration.BootstrapAdministrators` | `["a73013110"]`，一般帳號不會自動取得管理員                                   |
| `Storage.ApplyMigrationsOnStartup`       | false                                                                         |
| `Inference.Provider`                     | 現行 google，切本地再改 ollama；供應商的 DefaultModelId／Models 要一致        |
| `Knowledge.Embedding.Provider`           | 與對話分開設定；離線使用 ollama 或暫用 none                                   |

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
  </environmentVariables>
</aspNetCore>
```

相對位置由 app content root 解析，不依 shell 的工作目錄。明確指定的設定檔缺失、JSON 錯誤或無讀取權限會讓啟動失敗；修檔後回收集區。ASP.NET Core 不會自動把 app 外的同名 Production 檔當作環境檔，這裡由 AI Nexus 的外部設定載入器處理。不要同時保留 app 內另一份帶秘密的環境檔或在集區設重複環境變數。

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

集區身分 app 只需 Read／Execute，config 只需 Read，keys／logs 需 Modify。不要讓 Web 身分改寫應用 DLL 或秘密設定。先建立集區，以下以主機管理員執行：

```powershell
$taskPrincipal = 'IIS AppPool\AiNexus'
icacls 'D:\CoreProject\AiNexus\app' /grant:r "${taskPrincipal}:(OI)(CI)RX"
icacls 'D:\CoreProject\AiNexus\config' /inheritance:r `
  /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${taskPrincipal}:(OI)(CI)R"
icacls 'D:\CoreProject\AiNexus\keys' /inheritance:r `
  /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${taskPrincipal}:(OI)(CI)M"
icacls 'D:\CoreProject\AiNexus\logs' /inheritance:r `
  /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' "${taskPrincipal}:(OI)(CI)M"
```

`/grant:r` 只替換指定身分的 grant；若既有檔案有其他**顯式**讀取規則，仍須在 NTFS「進階安全性」檢查並移除未授權的 Users／Everyone，確認兩個 JSON 與備份也繼承正確規則。從開發機複製的 Secrets 檔可能限制為開發者／SYSTEM／Administrators；因此要另給 IIS 集區讀取權，而不是使用本機的 `Protect-NexusSecrets` 重設主機 ACL。

應用 SQL 帳號需專用資料庫的資料讀寫（各功能會 Insert／Update／Delete），不要只給 Users 表讀取權，也不要常態 db_owner。DDL／建庫用 DBA 部署帳號或維運階段的暫時權限。外部公文／校務來源只能給核准 view 的 SELECT，`ApplicationIntent=ReadOnly` 本身不能取代 SQL 權限。

## 8. 套用資料庫更新

先暫停所有共用這個 AiNexus 資料庫的舊版 app（含本機），備份資料庫。舊版復原器不認生成租約，不能與新版同時執行。

此版的累積 migrations 包含：

- `GenerationExecutorLeases`：`inference.GenerationRuns.ExecutorId`、`LeaseExpiresAt` 與索引。
- `ModelSpendAndConnectedWorkspace`：`inference.ModelPrices`、`ModelCharges`、`workspace.RepositoryConnections`、`research.WebSearches`、`knowledge.RepositoryImports`，以及 SQL Server 2025 的 `knowledge.Chunks.EmbeddingVector1024`。保留既有 768 維欄位與資料。
- 功能種子新增 `dashboard`／`repositories`，基本工作區群組可用；Gitea 遠端權限仍由個人 token 決定。

資料庫現有 13 個業務 schema。若 runtime 帳號採逐 schema 授權，務必將新 `workspace`／`research` schema 的資料讀寫加入原有授權；DDL 仍只給部署身分。價格在管理介面新增，不寫在公開 JSON；升級前的呼叫保持「早期呼叫尚無價格紀錄」，不推測重算。

使用有 DDL 權限的維運身分，在主機 PowerShell 7 執行：

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet 'D:\CoreProject\AiNexus\app\AiNexus.Api.dll' `
  --contentRoot 'D:\CoreProject\AiNexus\app' `
  --LocalConfigPath '..\config\appsettings.Production.json' `
  --SecretsConfigPath '..\config\appsettings.Secrets.json' `
  --InitializeDatabase true
if ($LASTEXITCODE -ne 0) { throw '資料庫更新未完成，先不要啟動網站。' }
```

工具只建立／更新專用 AiNexus，套用未執行的 migrations，不清空既有資料。新欄位允許 null，舊的未完成生成會被回收為 executor_lost；歷史回答保留。完成後移除運行帳號的 DDL 權限，再啟動集區。多台新版 app 的生成租約每 15 秒續期，有效期兩分鐘；只有過期的任務能被復原，主機時鐘須同步。租約不是全域 GPU 並行控制，仍由 Ollama 與各 app 的佇列配置管理負載。

## 9. 分層驗收：確定設定正確

先執行套件附的靜態檢查（appcmd 檢查需管理員權限）：

```powershell
pwsh -NoProfile -File 'D:\Packages\AiNexus\Verify-IIS.ps1' `
  -AppPath 'D:\CoreProject\AiNexus\app' -AppPool AiNexus `
  -BaseUrl 'https://你的實際主機名稱'
```

它檢查必要檔案、Production、外部 v2 設定位置、金鑰目錄、Host、集區與 HTTPS session；不會顯示秘密，也不聲稱讀得到檔案就代表集區身分可讀。

再做**唯讀 SQL 與設定驗證**，不啟動背景 workers、不登入 AD、不呼叫 Google／Ollama：

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet 'D:\CoreProject\AiNexus\app\AiNexus.Api.dll' `
  --contentRoot 'D:\CoreProject\AiNexus\app' `
  --LocalConfigPath '..\config\appsettings.Production.json' `
  --SecretsConfigPath '..\config\appsettings.Secrets.json' `
  --VerifyDeployment true
```

輸出應有 `environment=Production`、`sqlConnected=true`、`pendingMigrations=0`、`sqlEncrypted=true`、`trustsSqlCertificate=true`、`adConfigured=true`、正確 provider、模型數與 keyRingPath、`ready=true`。退出碼 0 才通過。此指令用**目前維運 shell 身分**讀檔，IIS 身分仍需實際網站驗證。

輸出也顯示 embedding provider／維度、webSearchEnabled 與 giteaEnabled，並驗證這些 optional tools 的參數範圍。`ready=true` 只表示核心設定與 SQL 可用，不代表 Gitea token、搜尋 API 或 embedding 品質已實測通過。

最後用新的無痕瀏覽器驗收：

1. 直接開 `/chat`、`/projects`：未登入會到 `/login?returnUrl=...`，沒有先出現私人工作區。
2. AD 登入後能開對話，重新整理仍登入；登出後私人 URL 再次要求登入。
3. 短回答與「圖解傅立葉轉換」能串流到完成；停止／重新生成都保留歷史。
4. 上傳合成文字檔及圖片，實際模型能力正確；文字模型不應允許圖片。
5. 網路面板 `/api/v1/status` 是 ready；`/health/live` 只有存活，不代表 SQL 或模型就緒。
6. 一般帳號不可開管理 API；管理員查看對話會留稽核。專案／知識權限隔離正常。
7. 回收集區後重新登入／生成可用；config／keys／logs 的 URL 無法讀取。
8. 總覽在個人／平台範圍、日期與不同幣別間正確切換；先設定測試模型價格，再確認單次／全對話金額及管理 CSV。平台查閱應有稽核。
9. 啟用 Gitea 後用唯讀個人 token 連線、讀取固定 commit 檔案並帶入草稿；回收集區後仍可解密 token。`keys` 同時保護此 token，不能在更新時清空。
10. 如啟用連網搜尋，先完成 [SearXNG／Brave 設定](../../docs/WEB_SEARCH.md)，再測 opt-in、來源與每日配額；未設定時入口停用。自架搜尋仍會向外部搜尋引擎送出公開提問。
11. 若切到本地 embedding，先確認 Ollama 可達、指定模型已安裝且回傳維度正確，再重新索引合成文件及檢查引用；不要把核心 ready 當成向量驗收。

如需真實 AI、AD TLS 與 EF／Dapper 寫入探測，再於受控環境跑 `--VerifyConnections true --VerificationOutput <logs內檔案>`。該工具會發送合成資料並呼叫真實模型；不是上面唯讀 VerifyDeployment 的一部分。

## 10. 更新與回復

維運窗口先等待背景任務結束，停止網站／集區，保存 SQL 備份、config、keys 和上一版 app。不要在程序仍鎖住 DLL 時直接覆蓋，也不要刪整個 AiNexus 根目錄。用全新的 release app 資料夾替換舊 app，保持外部 config／keys／logs。需移動目錄時先確認 `Resolve-Path` 真的是 `D:\CoreProject\AiNexus\app`，不是 junction 或其他位置。

首次依序：備份 → 停止全部舊版 → 外部設定遷移 → 新 app → DB migration → ACL → 唯讀驗證 → 啟動 IIS → 瀏覽器驗收。日後沒有 schema 變更時省略 migration。使用 `app_offline.htm` 亦可讓 ANCM 停止應用，移除後重啟；不要把該檔留在發版套件。[ANCM 的部署與啟動診斷](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module?view=aspnetcore-10.0) 說明了此機制。

失敗時先停止新版，還原上一版 app 與相符 config。資料庫 migration 不應未確認就 Down；新版追加的欄位／表通常可保留，但仍需先檢查舊版是否相容。回退後舊版不會記錄新的費用或 connector 功能；原價格與費用表保留，不刪計量證據。只有舊版執行，避免舊復原器誤殺新版。key ring 留原位置，固定身分；若換身分就接受重新登入／連線 Gitea，或另行規劃金鑰重保護。

## 11. 常見錯誤與診斷

| 現象                    | 檢查                                                                                                                                                       |
| ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| HTTP 500.30             | Windows Application event log；確認 JSON／外部路徑／ACL／options／DPAPI。暫時把 stdoutLogEnabled 改 true、重現一次，讀 `logs/stdout*.log` 後立即關回 false |
| 500.31／502.5           | Hosting Bundle、.NET10 runtime、x64、ANCM 與 DLL 完整性                                                                                                    |
| 500.19                  | web.config XML、AspNetCoreModuleV2、IIS 區段鎖定；不要在 app/web.config 強行設被鎖的 authentication 區段                                                   |
| 403／Host 不符          | HTTPS Host 名稱與 AllowedHosts、IIS binding、實際入口是否走 IP                                                                                             |
| 登入成功又回登入        | 只用 HTTP、Secure cookie 未送出、keys ACL／執行身分變更、多台 key ring 不一致；不是 SQL TrustServerCertificate 問題                                        |
| SQL 憑證錯誤            | 外部 Production 是否真的載入；若完整 ConnectionStrings.Nexus 非空，該字串優先，須自行含 Encrypt=True;TrustServerCertificate=True                           |
| 模型未就緒              | SQL migrations、服務啟動、Ollama Endpoint 指到哪台、模型已安裝與 profile Id 一致、Google key／quota                                                        |
| 生成中斷，executor_lost | 執行主機停止、心跳逾期、時鐘不同步；確認共用 DB 的所有主機都已更新到租約版本                                                                               |
| 附件／PDF 匯出錯誤      | 上傳上限、集區權限、PDF 匯出需該執行身分可用的 Edge／Chromium，伺服器未安裝時改成已部署瀏覽器                                                              |

日誌可能包含部署資訊，只開放維運人員，stdout 不能長期開啟且無內建輪替。一般 request 錯誤帶 `traceId`；生成錯誤另有 run ID／error code 可對照稽核。上線設定與金鑰不是可公開的成果，不上傳 Git。
