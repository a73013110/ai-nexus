# 後端測試

結論：每個整合測試有自己的 host 與 SQLite 檔，彼此不共用狀態；host 便宜（約 0.1 秒），所以不需要共用 host 與清表。預設測試集在 4 核心 Linux 容器約 50 秒。

## 執行

測試專案是 xUnit v3，`global.json` 指定 `dotnet test` 走 Microsoft.Testing.Platform：路徑要用 `--project` 或 `--solution`，結果檔用 `--report-xunit-trx`；`--filter` 仍是原本的 VSTest 語法。

```powershell
dotnet test --solution backend/AiNexus.slnx --filter "Category!=Performance&Category!=Browser"
dotnet test --project backend/tests/AiNexus.Tests --filter "FullyQualifiedName~ChatApiTests"
```

| Trait／條件 | 何時執行 |
|---|---|
| `Category=Browser` | 設定 `AINEXUS_TEST_BROWSER`（見 [開發](DEVELOPMENT.md)）；未設定時以 `Assert.Skip` 略過 |
| `Category=Performance` | 只在 `Test-Diagnostics.ps1 -Performance` |
| SQL Server 測試 | 設定 `AINEXUS_SQLSERVER_TEST` 連線字串；未設定時略過 |

## `NexusFactory` 的取捨

- **資料庫**：schema 每個測試程序只建一次範本檔，每個 host 複製一份。不要在測試裡呼叫 `EnsureCreated`。換掉 provider 時要像 `NexusFactory` 一樣呼叫 `AddNexusInterceptors(scope)`，否則 domain event 不會分派、稽核列不會補值。
- **背景工作**：只有生成 worker 會跑。其他 hosted worker 用 `workers: [typeof(DiagnosticWorker)]` 選用；週期性工作（背景工作、清理）由測試直接呼叫，例如 `ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services).ProcessNextAsync(...)`。原因：背景工作和測試搶同一個 SQLite 檔的寫入鎖，會造成偶發失敗。
- **時間**：需要逾時或週期的測試傳 `clock: new FakeTimeProvider(DateTimeOffset.UtcNow)` 再 `Advance`；正式程式碼的計時器、`CancellationTokenSource` 逾時都要吃 `TimeProvider` 才能這樣測。
- **密碼**：Argon2 用 `NexusFactory.PasswordCost`（1 MiB、1 次）；正式環境固定 `Argon2Cost.Recommended`。

## 等待非同步結果

不要用 `Task.Delay` 輪詢。現有的明確訊號：

| 要等的事 | 用法 |
|---|---|
| 生成結束 | `ChatApiTests.WaitForTerminal`（跟隨 run 事件串流到結束） |
| 第一段串流文字 | `ChatApiTests.WaitForFirstDelta` |
| 模型被呼叫 | `factory.Provider.WhenCalledAsync(n)` |

仍保留的短等待：診斷事件寫入 SQL（`DiagnosticTests.WaitForIssue`，跨背景管線）、「某件事不應發生」的負向檢查（100–200 ms）、模型目錄的背景刷新。

## host 啟動為何便宜

Minimal API 的端點委派由 Request Delegate Generator 在編譯期產生（`backend/src/Directory.Build.props`）。關掉它會讓每個 host 多花約 1 秒編譯約 300 個端點，整套測試回到 5 分鐘以上。
