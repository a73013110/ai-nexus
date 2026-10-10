# SQL 與附件備份、還原

新增診斷後，備份還需包含站外 `Diagnostics.Directory` 的 journal／cursor，以及SQL中的DiagnosticEvents／AuditEvents。SQL離線時尚未補送檔案是唯一保存副本，不能在發版或備份時刪除。已清理的segment無法補回較早SQL復原點；LogId只能去重，不能重建遺失資料。日誌備份權限、期限與恢復步驟見 [DIAGNOSTICS](DIAGNOSTICS.md#部署migration與備份)。

完整備份是一組相同時點的 **AiNexus SQL、站外附件目錄、外部設定與 Data Protection keys**。SQL 只保存附件 metadata、StorageKey、引用、權限及衍生搜尋資料；單獨 SQL `.bak` 無法還原原檔，聊天 JSON 文字備份也不包含原檔。

正式原檔預設 `D:\CoreProject\AiNexus\data\attachments`，以外部 JSON 與 web.config 的最終 `Attachments.StoragePath` 為準。不要只複製 IIS `app`，不要在更新時清空 `data`。備份及包含秘密的 config／keys 限維運身分讀取，另保留於不同磁碟或備份系統。

## 一致性備份

目前採停機一致性備份；先完成維運窗口，等待生成／索引工作結束並停止所有共用此 DB／附件目錄的 app，包括 IIS、本機與其他主機。停止服務同時停止草稿回收及刪檔工作，確認沒有檔案寫入後再進行。只停止 IIS HTTP 流量而讓背景工作繼續運行不足以保證一致性。

1. 保存版本 commit、migration history、備份時間、設定版本及實際附件路徑。每個備份組使用新的空目錄與相同識別碼。
2. DBA 在 SQL Server 執行完整備份，開啟 checksum；臨時全備份可用 `COPY_ONLY` 避免打斷原差異備份基線。範例路徑是 **SQL Server 主機**的磁碟，不是 IIS 主機：

   ```sql
   BACKUP DATABASE [AiNexus]
     TO DISK = N'E:\SqlBackups\AiNexus\20261006\AiNexus.bak'
     WITH COPY_ONLY, CHECKSUM;
   RESTORE VERIFYONLY
     FROM DISK = N'E:\SqlBackups\AiNexus\20261006\AiNexus.bak'
     WITH CHECKSUM;
   ```

3. 在 IIS／檔案主機複製整個附件樹，保留 `.blob` 與可能存在的 `.upload`；不依檔案名稱重新命名，不修改 StorageKey。可將已核對的來源與新的空備份目錄交給備份工具；例如 PowerShell：

   ```powershell
   $taskSource = 'D:\CoreProject\AiNexus\data\attachments'
   $taskBackup = 'E:\AiNexusBackups\20261006\attachments'
   # 執行前核對來源是實際站外目錄，備份目錄為新的空目錄。
   robocopy $taskSource $taskBackup /E /COPY:DAT /DCOPY:DAT /R:2 /W:3
   if ($LASTEXITCODE -ge 8) { throw '附件備份失敗，保留停機狀態並排查。' }
   ```

4. 同一備份組保存 config／keys、附件檔案清單、大小及備份工具校驗結果。config／keys 必須另施加受控 ACL，檔案資料複製不等於保留安全邊界。
5. SQL 與檔案都成功才標記整組完成並重啟服務；其中一份失敗不得當成可完整還原的備份。

Recovery model 與完整／差異／log 排程由 DBA 維護。若需要在線備份或任意時間點還原，必須先設計資料庫與檔案的共同快照／版本保留策略；不能把任意 SQL log 還原時間點與最新附件目錄直接拼接，因為實體刪檔可能已發生。

## 還原與驗證

先在隔離環境演練，使用同一 app 版本與相同 migration 基線；正式還原由維運／DBA 執行，停止所有相關 host。還原到新的空附件目錄，避免與失敗環境中的其他檔案混合；由 DBA 還原同一備份組的 SQL，不使用空庫初始化取代資料還原。

1. 設定 `Attachments.StoragePath` 指向還原的站外目錄，恢復外部設定並核對 web.config 的環境變數覆寫。設定與 `__EFMigrationsHistory` 須來自同一版本。
2. 恢復目錄 ACL：app／config 只讀，原檔目錄須讓固定 IIS 集區身分讀／寫／刪除。磁碟預留足夠空間；不要讓 Users／Everyone 取得原檔讀取權或將目錄掛為 IIS 虛擬目錄。
3. 還原 keys 需同時具備其 DPAPI 保護身分；換主機／身分不能只複製 XML 就假設可解密。若無法恢復保護身分，依維運政策重建 key ring、重新登入與重新連線 Gitea，保留既有業務資料。
4. 在啟動 workers 前執行主機指令 `verify deployment`：核對 schema／snapshot、provider 設定及站外原檔讀寫刪 probe。用 SQL 查核 ready 檔案的清單：

   ```sql
   SELECT [Id], [StorageKey], [Size]
   FROM [attachments].[Attachments]
   WHERE [StorageState] = N'ready';
   SELECT [OwnerId], SUM([Size]) AS [UsedBytes]
   FROM [attachments].[Attachments]
   GROUP BY [OwnerId];
   ```

   每個 ready 記錄須有對應 `<前2碼>/<次2碼>/<StorageKey>.blob` 且大小一致。metadata 缺檔或大小不符會回應 503，不能用空檔補齊。`pending`／`deleting` 由生命週期工作恢復，這些容量仍計入已用。

5. 啟動後以合成資料驗證不同帳號登入、角色／ACL、訊息分支、token／耗時、原檔下載、知識引用、專案及具名分享；個人容量與 SQL Size 合計一致，多處引用不重複計算。演練一次刪檔後確認磁碟、metadata 及容量同步。

只有實際還原與授權／原檔驗證都通過，才能認定備份可用。部署與目錄權限見 [IIS](../deploy/iis/README.md)，刪檔、配額及草稿規則見 [附件](ATTACHMENTS.md)。
