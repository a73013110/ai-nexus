# 診斷日誌維運

- 日誌存在站外 `Diagnostics.Directory` 的 JSONL，背景補送到 SQL；SQL 離線時尚未補送的檔案是唯一副本，發版、備份或清理時都不能刪。
- 預設保存：SQL 診斷 30 天、已補送的檔案 14 天、稽核 365 天。這只是可設定的預設，組織保存期限與法定保留仍待確認（見文末）。
- 架構與故障時的行為見 [診斷日誌架構](../architecture/DIAGNOSTICS.md)；查詢頁操作見 [系統日誌](../features/SYSTEM_LOGS.md)。

## 目錄與權限

- 空字串時用 `%ProgramData%\AiNexus\diagnostics`；正式主機設成站外資料根目錄下的 `diagnostics`（`Publish-IIS.ps1 -DataRoot` 會寫進套件 config）。
- 目錄必須是絕對路徑、在 app 外、主機本地，不可經 junction／symlink／UNC，不得映射成 IIS 虛擬目錄。
- application pool 身分需要 Modify；維運人員依組織核准給唯讀。套件的 `logs/` 只給 ANCM stdout，不能與應用 journal 混用。
- 主機指令 `verify deployment` 會輸出 diagnosticStoragePath、diagnosticStorageWritable、容量與 OTLP 狀態，並做短暫寫入 probe；它用維運 shell 的身分，IIS 帳號仍要實際網站驗證。

## 設定與容量

`Diagnostics` 區段的鍵、預設值與合法範圍在 `backend/src/AiNexus.Platform/Diagnostics/DiagnosticOptions.cs`；載入順序見 [設定](../development/CONFIGURATION.md)。最低等級以 `Diagnostics` 為準，不受 `Logging.LogLevel` 影響。修改後重啟。

- `MaxDiskBytes`（預設 2 GiB）是整個目錄的上限；`MaxSqlRows` 由 metadata 估算，是近似的 soft limit。到上限不會提前刪除未過期資料，要擴容或調整保存政策。
- 檔案只清理已確認匯入 SQL、非活動且到期的 segment。**未補送的資料不會為了 14 天政策而刪除**，所以 SQL 長期離線時可能先用完磁碟上限。
- SQL 診斷與稽核各自分批 autocommit 清理（單次 command 最多 5 秒、背景總預算約 20 秒），每 5 分鐘排程一次。
- 佇列以筆數計，記憶體用量取決於實際事件大小；不要把大佇列當成廉價容量。
- 日誌設定的安全摘要變更會寫一筆 `system.diagnostics.configuration` 稽核，不含路徑或 exporter URL；主機設定檔的 ACL 與變更管理另行治理。

## Windows 緊急事件來源

緊急通道在來源已註冊時寫 Windows Application event 9010。應用程式不自行取得管理權限，由維運管理員首次安裝時註冊：

```powershell
# Windows PowerShell 5.1，提升權限的安裝 shell；不是日常執行身分
if (-not [Diagnostics.EventLog]::SourceExists('AiNexus.Diagnostics')) {
  New-EventLog -LogName Application -Source 'AiNexus.Diagnostics'
}
```

未註冊且 stdout 關閉時，緊急記錄只剩 stderr，應另以 metrics 監控。

## 健康資訊

系統日誌頁的健康資訊是程序生命週期計數：佇列深度、accepted／written／replayed、lost／corrupt／sampled、寫入與 OTLP 失敗、磁碟與待補送 bytes、最後成功寫入檔案與 SQL 的時間。補送中也可能顯示 degraded；重啟後計數歸零，趨勢要靠外部 metrics。SQL 故障時授權與讀取稽核無法保存，健康 API 也可能打不開，不能為此繞過權限。

## 故障排除

| 現象 | 查證順序 |
| --- | --- |
| NX 暫時查不到 | 時區與範圍 → 待補送 bytes／最後 SQL 寫入 → 實際的 Directory → 檔案內是否有同代碼 → SQL 匯入失敗或容量 |
| `sql_import_failed_*` | 保留 journal 與 cursor；查 SQL 服務、TLS、帳號、migration、空間。恢復後看待補送下降。不要刪 cursor（會重播，SQL 去重） |
| `journal_access_denied` | 以真正的 app pool 身分確認目錄與父目錄的 Modify ACL |
| `journal_disk_full`／`journal_capacity_exhausted` | 看磁碟剩餘、`MaxDiskBytes`、未補送大小；修 SQL 或核准擴容，不刪未確認的檔案 |
| `important_queue_full`、Lost 增加 | 查 IO／SQL 積壓與峰值；可對低等級採樣，重要事件不得採樣 |
| `journal_corrupt_record` | 保留原檔供鑑識；補送會繼續處理有效資料 |
| IIS 500.30／啟動失敗 | Windows Application／ANCM 事件 → 啟動 Critical 的檔案記錄 → 外部設定、ACL、migration、options |
| 500.31／500.19／502.5／崩潰 | Hosting Bundle、runtime、web.config／ANCM、WAS、.NET Runtime、Application Error、必要時 crash dump |

應用程式收不到 managed 入口之前的失敗與 native crash。IIS access log、WAS、ANCM stdout、SQL Server ERRORLOG 與 Extended Events 都要另查；stdout 只在啟動診斷時短期開啟。

## SQL 完全離線時

管理頁可能因授權或稽核失敗打不開。授權的維運 shell 可直接讀已遮罩的 JSONL：每行是 `{checksum,event}`，event 欄位用 .NET 屬性名（`IssueCode`、`TraceId`、`ExceptionDetail`），依最新的 UTC 日期檔逐步讀取。先備份，不要改 owner lock、checksum 或 cursor；修復後用同一個 Directory 重啟，舊 owner 的檔案會自動補送。

## 備份與還原

備份包含 SQL（含 `AuditEvents`、`DiagnosticEvents`）、尚未補送的 journal 與 cursor、外部設定與 keys。把 SQL 還原到較早時間點時，仍在的 journal 會補回保留窗口內的資料（`LogId` 去重），已清理的 segment 補不回來。日誌備份不取代 [SQL＋附件的一致性備份](BACKUP.md)。

## 待組織確認

保存期限與 legal hold、誰可以 query／detail／export、使用者識別的保存政策、備份期限、磁碟與 SQL 告警門檻、外部 collector 的 TLS 與保存邊界、設定檔變更管理、是否需要 WORM 與主機 crash 收集。完成日誌功能不代表符合所有資安法規。
