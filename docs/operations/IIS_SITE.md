# IIS 網站、權限與資料庫初始化

## IIS 網站與集區

建立專用 `AiNexus` application pool 與網站；網站 physical path 是 `D:\CoreProject\AiNexus\app`。

| 集區選項 | 設定與用途 |
| --- | --- |
| .NET CLR Version | No Managed Code（空字串），由 ANCM 載入 .NET |
| Managed Pipeline | Integrated |
| Enable 32-Bit Applications | False |
| Identity | 固定 ApplicationPoolIdentity 或經規劃的專用服務帳號 |
| Load User Profile | True，維持 DPAPI 的執行身分 |
| Maximum Worker Processes | 1；目前生成佇列與部分寫入鎖在程序內 |
| Start Mode | AlwaysRunning |
| Idle Time-out | 0，避免使用者閱讀時或背景索引時被閒置停止 |
| Disable Overlapped Recycle | True，部署／回收期間避免舊版與新版並存 |

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

## 檔案與 SQL 最小權限

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

## 空資料庫初始化

migration 基線是單一 `InitialCreate`（2026-10-09 重建），包含全部 schema、種子、索引與資料表／欄位說明。2026-10-09 以前建立的資料庫無法升級，必須刪除後重新初始化；初始化不會 DROP、清空資料或重寫 history。日後新增 migration 延續此基線，細節見 [資料庫](../architecture/DATABASE.md)。

先停止所有共用此 DB 的 host，確認 DBA 準備的 AiNexus 是空庫或不存在。使用有建庫／DDL 權限的維運身分，在主機 PowerShell 7 執行：

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet 'D:\CoreProject\AiNexus\app\AiNexus.Host.dll' `
  --contentRoot 'D:\CoreProject\AiNexus\app' `
  --LocalConfigPath '..\config\appsettings.Production.json' `
  --SecretsConfigPath '..\config\appsettings.Secrets.json' `
  db init
if ($LASTEXITCODE -ne 0) { throw '資料庫初始化未完成，先不要啟動網站。' }
```

工具只建立不存在的 AiNexus，套用 InitialCreate 並檢查模型／snapshot 一致。重跑不會重建；DBA 亦可審閱套用套件的 `migrations.sql`。完成後移除 runtime 身分的 DDL 權限。SQL Server 2025 額外建立 VECTOR(768)／VECTOR(1024)，舊 SQL 使用 portable 路徑。

runtime 需要所有業務 schema（每模組一個，清單見 [資料庫](../architecture/DATABASE.md)）的 DML 與 history 的 SELECT。每個 app 使用一個程序；provider 可各自多個推論 worker，MaxConcurrency 只限制本程序的聊天／模型任務，沒有跨主機全域 GPU 限制。生成租約每 15 秒續期、兩分鐘有效，主機時鐘須同步。
