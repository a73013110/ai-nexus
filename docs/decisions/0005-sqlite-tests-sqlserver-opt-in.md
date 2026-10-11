# 0005 測試用 SQLite，SQL Server 測試為選用

- **狀態**：採用（2026-10）
- **決定**：整合測試每個 host 用自己的 SQLite 檔；向量、全文與交易行為的 SQL Server 測試只在設定 `AINEXUS_SQLSERVER_TEST` 時執行，由開發者在本機 LocalDB／SQL Server 跑。不用 Testcontainers。

## 原因

- 開發與 AI 助手的環境不能跑 Docker，Testcontainers 無法使用。
- SQLite 讓每個測試完全隔離又便宜（host 約 0.1 秒），預設測試集約 50 秒，可以每次 PR 都跑。
- SQL Server 專屬的差異（原生向量、全文索引）集中在 `Persistence/SqliteModel.cs` 與少數 opt-in 測試，影響範圍可控。

## 代價

SQL Server 的 migration、全文與向量行為不會在每次 `Verify.ps1` 驗證，改由 `Test-Environment.ps1 -SqlOnly`、opt-in 測試與部署驗收補上。見 [測試](../development/testing.md)。
