# 測試資料庫

整合測試連 LocalDB（`(localdb)\MSSQLLocalDB`，SQL Server 2025），每個 host 一個從範本複製的資料庫；為什麼這樣做見 [ADR 0015](../decisions/0015-integration-tests-on-localdb.md)，實作在 `backend/tests/AiNexus.IntegrationTests/Support/TestDatabase.cs`。

## 範本與每個 host 的資料庫

- `TestDatabase` 第一次被用到時，以 `database/App` 的 dacpac 部署範本（約 25 秒），再執行 `Seed/`；範本 detach 後快取在 `%TEMP%\ainexus-test-db`，dacpac 結構與腳本不變時下次直接沿用。dacpac 要先 build（`Verify.ps1` 會做）：

  ```powershell
  dotnet build database/App
  ```

- 每個 host 複製範本檔再 attach 成 `AiNexus_Test_<guid>`，釋放時在背景刪除；中途結束留下的資料庫與檔案，6 小時後由下一次測試清掉。
- 不需要 host 的資料庫測試用 `TestDatabase.WithContextAsync`。
- LocalDB 沒有全文元件。完全依賴全文的測試開頭寫 `Assert.SkipUnless(TestDatabase.FullTextInstalled, TestDatabase.NoFullText)`；其他測試不得略過。hybrid 在沒有全文時降為 vector，斷言用 `KnowledgeApi.HybridMode`。
- EF 組態不宣告索引與約束，唯一性、filtered index 這類保證要對資料庫測（例如 `CreateRunTests`、`JobLifecycleTests` 的唯一索引測試）。
- 程式呼叫的每支 procedure 都要有整合測試以名稱呼叫它（`ProcedureCoverageTests` 檢查）。

## LocalDB 無法啟動

症狀：建範本時拋出「無法連線 (localdb)\MSSQLLocalDB」，或 `sqllocaldb start` 回報 `SQL Server process failed to start`（事件檢視器的 SQLLocalDB 528、錯誤 575 或 0x89c5010a）。依序試：

1. 先看是否已經在跑：`sqllocaldb info MSSQLLocalDB` 顯示「執行中」就直接重跑測試。在沙箱化的終端機（例如 AI 助手的 Bash）啟動常會失敗，改在一般 PowerShell 執行 `sqllocaldb start MSSQLLocalDB`。
2. 重新啟動執行個體：`sqllocaldb stop MSSQLLocalDB -k`，再 `sqllocaldb start MSSQLLocalDB`。
3. 看 `%LOCALAPPDATA%\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\error.log` 最後幾行；常見是磁碟空間不足或系統資料庫檔被鎖住。
4. 重建執行個體：先確認上面沒有自己的開發庫（有的話之後要重新 attach，檔案不會被刪），再`sqllocaldb stop MSSQLLocalDB -k`、`sqllocaldb delete MSSQLLocalDB`、`sqllocaldb create MSSQLLocalDB -s`，再刪除 `%TEMP%\ainexus-test-db` 讓範本重建。
5. 仍失敗就重新安裝 SQL Server 2025 Express 的 LocalDB；`sqllocaldb versions` 要列出 17.x。

