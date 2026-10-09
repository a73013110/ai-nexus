# EDoc 資料庫封裝的重用方式

來源為使用者提供的 `D:\CoreProject\EDoc_HL\src\EDoc.Core\Database`；原專案未修改。這裡保存第一版需要的最小原始碼，避免執行與部署依賴開發機的 D 槽外部專案。

`DbHelper.cs`、交易／多結果集封裝及相關介面、marker、enum、model 保持原始內容；原版的 `EfHelper`／`IEfHelper` 未保留，EF Core 一律直接注入 scoped `NexusDbContext`。逐檔來源與 SHA-256 見 [source-manifest.json](source-manifest.json)。Dapper 查詢使用參數繫結，回傳前關閉連線；需要交易時使用原本的 `BeginTransactionAsync`。

`NexusConnectionFactory` 是 AI Nexus 的 adapter；`INexusDatabase` 指向 AiNexus，初始化專用 `INexusBootstrapDatabase` 指向 master。模型、migrations 與業務寫入由 EF Core 管理；狀態檢查與建庫使用 DbHelper。

`scripts/Test-Connections.ps1` 會在 operations.AuditEvents 建立一筆隨機識別的 CLI 驗證記錄，確認 EF 提交後可由另一個 Dapper 連線讀取、Dapper 更新可由 EF 讀回，最後只刪除這一筆驗證記錄並確認清理完成；不建立模擬登入身分。
