# IIS 更新、回復與常見錯誤

## 更新與回復

維運窗口先等待背景任務結束，停止網站／集區，保存同一時點的 SQL＋原檔備份組、config、keys 和上一版 app。不要在程序仍鎖住 DLL 時直接覆蓋，也不要刪整個 AiNexus 根目錄。用全新的 release app 資料夾替換舊 app，保持外部 config／keys／logs／data。需移動目錄時先確認 `Resolve-Path` 真的是 `D:\CoreProject\AiNexus\app`，不是 junction 或其他位置。

更新前先完成 [SQL＋原檔一致性備份](BACKUP.md) 並停止全部 host，再以 [初始化指令](IIS_SITE.md#空資料庫初始化) 或 DBA 審閱套件的 `migrations.sql` 套用新的 migration；沒有 schema 變更時省略。使用 `app_offline.htm` 亦可讓 ANCM 停止應用，移除後重啟；不要把該檔留在發版套件。[ANCM 的部署與啟動診斷](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module?view=aspnetcore-10.0) 說明了此機制。

失敗時先停止新版，回復相符的 app／config／SQL／原檔備份組。app 版本必須與 SQL 的 migration 相符，不可只回退 DLL 再指向較新的資料庫。不要未確認就執行 migration Down，也不要混用不同時點的 SQL 和附件；完整還原程序見 [備份與還原](BACKUP.md)。keys 保留原位置與保護身分。

## 常見錯誤與診斷

一般操作先用前端 NX 查證代碼在「系統日誌」查詢；必要時擴大時間範圍並檢查補送健康狀態。SQL離線可能使授權與查閱稽核無法保存，此時由授權維運者查站外JSONL、Windows Application `AiNexus.Diagnostics` 與SQL ERRORLOG，不能繞過管理授權。`verify deployment`輸出 diagnosticStoragePath、diagnosticStorageWritable、diagnosticCapacityBytes、diagnosticMaxSqlRows與diagnosticOtlpEnabled；它只以呼叫shell身分測試短期IO，IIS帳號權限需另驗。

| 現象 | 檢查 |
| --- | --- |
| HTTP 500.30 | Windows Application event log；確認 JSON／外部路徑／ACL／options／DPAPI。暫時把 stdoutLogEnabled 改 true、重現一次，讀 `logs/stdout*.log` 後立即關回 false |
| 500.31／502.5 | Hosting Bundle、.NET10 runtime、x64、ANCM 與 DLL 完整性 |
| 500.19 | web.config XML、AspNetCoreModuleV2、IIS 區段鎖定；不要在 app/web.config 強行設被鎖的 authentication 區段 |
| 403／Host 不符 | HTTPS Host 名稱與 AllowedHosts、IIS binding、實際入口是否走 IP |
| 登入成功又回登入 | 只用 HTTP、Secure cookie 未送出、keys ACL／執行身分變更、多台 key ring 不一致；不是 SQL TrustServerCertificate 問題 |
| SQL 憑證錯誤 | 外部 Production 是否真的載入；若完整 ConnectionStrings.Nexus 非空，該字串優先，須自行含 Encrypt=True;TrustServerCertificate=True |
| 模型未就緒 | SQL migrations、服務啟動、Ollama Endpoint 指到哪台、模型已安裝與 profile Id 一致、Google key／quota |
| 生成中斷，executor_lost | 執行主機停止、心跳逾期、時鐘不同步；確認共用資料庫的所有主機是同一版本 |
| 附件／PDF 匯出錯誤 | Attachments.StoragePath／環境覆寫、站外目錄 Modify ACL／磁碟剩餘、容量／引用／待刪重試；PDF 匯出需該執行身分可用的 Edge／Chromium，伺服器未安裝時改成已部署瀏覽器 |

日誌可能包含部署資訊，只開放維運人員，stdout 不能長期開啟且無內建輪替。一般 request 錯誤帶 `traceId`；生成錯誤另有 run ID／error code 可對照稽核。上線設定與金鑰不是可公開的成果，不上傳 Git。
