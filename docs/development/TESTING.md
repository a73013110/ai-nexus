# 測試

- `./scripts/Verify.ps1` 跑全部預設測試：腳本（Pester）、後端（架構＋整合）、前端 lint 與單元測試；預設後端測試集在 4 核心 Linux 容器約 50 秒。
- 每個後端整合測試有自己的 host 與 SQLite 檔，彼此不共用狀態；host 很便宜（約 0.1 秒），所以不需要共用 host 與清表。
- 真實瀏覽器、效能與 SQL Server 測試要另外開啟（見下表），AD、真模型與正式 IIS 由 `Test-Environment.ps1` 與 [部署驗收](../operations/IIS_VERIFICATION.md) 驗證。

## 測試在哪裡

| 位置 | 內容 |
| --- | --- |
| `backend/tests/AiNexus.ArchitectureTests` | 專案依賴方向、模組無循環、slice 形狀、檔名＝型別名、`[LoggerMessage]` EventId 唯一 |
| `backend/tests/AiNexus.Tests` | 整合測試（`NexusFactory`）、OpenAPI 合約、端點慣例、資料庫模型、設定範本 |
| `scripts/tests` | 腳本慣例、共用模組函式、文件連結與長度（`Docs.Tests.ps1`） |
| `frontend/src/**/*.spec.ts` | 前端單元測試（Vitest，`npm --prefix frontend test`） |
| `tests/e2e` | Playwright 端到端測試，對發布後的網站執行 |

## 執行後端測試

測試專案是 xUnit v3，`global.json` 指定 `dotnet test` 走 Microsoft.Testing.Platform：路徑用 `--project` 或 `--solution`，結果檔用 `--report-xunit-trx`；`--filter` 仍是 VSTest 語法。

```powershell
dotnet test --solution backend/AiNexus.slnx --filter "Category!=Performance&Category!=Browser"
dotnet test --project backend/tests/AiNexus.Tests --filter "FullyQualifiedName~ChatApiTests"
```

| Trait／條件 | 何時執行 |
| --- | --- |
| `Category=Browser` | 設定 `AINEXUS_TEST_BROWSER`（channel `msedge`／`chrome`，或瀏覽器執行檔絕對路徑）；`Verify.ps1 -Browser` 會設定，Linux 用 `-BrowserTarget /opt/pw-browsers/chromium`。未設定時 `Assert.Skip` |
| `Category=Performance` | 只在 `Verify.ps1 -Performance`，結果寫在 `artifacts/` |
| SQL Server 測試 | 設定 `AINEXUS_SQLSERVER_TEST` 連線字串；未設定時略過 |

## `NexusFactory` 的取捨

- **資料庫**：schema 每個測試程序只建一次範本檔，每個 host 複製一份。不要在測試裡呼叫 `EnsureCreated`。換 provider 時要像 `NexusFactory` 一樣呼叫 `AddNexusInterceptors(scope)`，否則 domain event 不會分派、稽核列不會補值。
- **背景工作**：只有生成 worker 會跑。其他 hosted worker 用 `workers: [typeof(DiagnosticWorker)]` 選用；週期性工作由測試直接呼叫，例如 `ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services).ProcessNextAsync(...)`。原因：背景工作和測試搶同一個 SQLite 檔的寫入鎖，會造成偶發失敗。
- **時間**：需要逾時或週期的測試傳 `clock: new FakeTimeProvider(DateTimeOffset.UtcNow)` 再 `Advance`；正式程式碼的計時器與 `CancellationTokenSource` 逾時都要吃 `TimeProvider` 才能這樣測。
- **密碼**：Argon2 用 `NexusFactory.PasswordCost`（1 MiB、1 次）；正式環境固定 `Argon2Cost.Recommended`。
- 測試替身只存在 `backend/tests` 與 `tests/e2e`，正式程式不接受測試身分 header。

## 等待非同步結果

不要用 `Task.Delay` 輪詢。現有的明確訊號：

| 要等的事 | 用法 |
| --- | --- |
| 生成結束 | `ChatApiTests.WaitForTerminal`（跟隨 run 事件串流到結束） |
| 第一段串流文字 | `ChatApiTests.WaitForFirstDelta` |
| 模型被呼叫 | `factory.Provider.WhenCalledAsync(n)` |

仍保留的短等待：診斷事件寫入 SQL（`DiagnosticTests.WaitForIssue`）、「某件事不應發生」的負向檢查（100–200 ms）、模型目錄的背景刷新。

## host 啟動為何便宜

Minimal API 的端點委派由 Request Delegate Generator 在編譯期產生（`backend/src/Directory.Build.props`）。關掉它會讓每個 host 多花約 1 秒編譯約 300 個端點，整套測試回到 5 分鐘以上。

## 真實 SQL Server

向量、全文與交易行為只能在真實 SQL Server 驗證；容器內不能跑 Docker，所以這組測試由開發者在本機 LocalDB／SQL Server 執行（[ADR 0005](../decisions/0005-sqlite-tests-sqlserver-opt-in.md)）。登入要能建立與刪除資料庫和全文 catalog；每項測試建立並清理自己的 `AINexus_Retrieval_Test_` 資料庫。

```powershell
$env:AINEXUS_SQLSERVER_TEST = 'Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true'
dotnet test --project backend/tests/AiNexus.Tests --filter "FullyQualifiedName~SqlServerRetrievalTests"
```

## 瀏覽器測試

- Playwright（`tests/e2e`）由 `Start-BrowserTest.ps1` 啟動編譯好的網站（5180），使用 `artifacts/` 下專用的空設定、金鑰、附件與日誌，不讀開發機的 `.local`；API 由測試替身提供。
- `Verify.ps1 -Browser` 設定 `NEXUS_E2E_PUBLISH_DIRECTORY=artifacts/verification`；單獨執行 npm 測試預設用 `artifacts/publish`。
- 覆蓋鍵盤、中文組字、版本分支、斷線、Markdown 安全、模型政策、Context、字體與窄螢幕；結果、trace 與畫面在 `artifacts/`。API fixture 回歸刻意不配置資料庫，不能當成 SQL Server 的功能或效能驗證。
