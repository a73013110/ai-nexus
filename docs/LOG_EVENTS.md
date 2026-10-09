# 日誌事件

應用程式自己的日誌一律寫成 `[LoggerMessage]` 方法，每個事件有固定且唯一的 EventId 與 EventName。系統日誌頁、匯出與 OTLP 都依這組識別查詢；儲存、遮罩與保存政策見 [DIAGNOSTICS](DIAGNOSTICS.md)。

## 規則

- `backend/src` 內呼叫 `LogInformation`／`LogWarning` 等擴充方法會因 CA1848 編譯失敗（`.editorconfig`）。改在記錄的類別內宣告 `private static partial void LogXxx(ILogger logger, ...)`，類別加 `partial`。
- 每個 `[LoggerMessage]` 要有 `EventId`（正整數）與以點分隔的 `EventName`；兩者在全部組件內不可重複，由 `LoggingTests` 檢查。
- 模板只放受核准的屬性名（`DiagnosticRedactor.Fields`）；例外型別用 `{ErrorType}`，不要放例外訊息。
- 範圍：1xxx HTTP 與錯誤、2xxx 檢索、3xxx 背景工作與生成、4xxx 前端回報、5xxx 服務生命週期、6xxx 檔案與分享。新事件取該區下一個號碼，並補進下表。
- 等級不由事件決定時（`Issues.Report`），方法加 `LogLevel level` 參數、屬性不設 `Level`。被診斷 provider 刻意保留在最低等級以下的事件（`http.rejected`）要設 `SkipEnabledCheck = true`，否則產生的程式會先檢查 `IsEnabled` 而把它丟掉。
- 框架或套件自己的日誌沒有 EventId 時，provider 以 Category＋模板的 SHA-256 產生穩定 ID；這不是業務識別，追查時同時看 Category。

## 事件表

| EventId | EventName | 位置 |
| --- | --- | --- |
| 1000 / 1001 / 1002 | `http.completed` / `operation.failed` / `http.rejected` | `Platform/Diagnostics/Issues.cs` |
| 2001 / 2002 | `retrieval.degraded` / `retrieval.retry` | `Knowledge/Retrieval/RetrievalDiagnostics.cs`、`Knowledge/Embeddings/RetrievalHttp.cs` |
| 2003 | `embedding.bootstrap_deferred` | `Knowledge/Embeddings/EmbeddingBootstrapWorker.cs` |
| 3000 / 3001 | `job.started` / `job.finished` | `Jobs/BackgroundJobWorker.cs` |
| 3002 / 3003 | `job.queue_unavailable` / `job.lease_renewal_failed` | 同上 |
| 3004 | `replay.cleanup_failed` | `Chat/RunEventRetentionWorker.cs` |
| 3100 / 3101 | `generation.started` / `generation.finished` | `Chat/GenerationWorker.cs`、`RunService.cs` |
| 3102 / 3103 / 3104 | `generation.disabled` / `generation.storage_unavailable` / `generation.startup_failed` | `Chat/GenerationWorker.cs` |
| 3105 / 3106 | `generation.recovery_pending` / `generation.recovery_postponed` | `GenerationWorker.cs`、`RunRecoveryWorker.cs` |
| 4001 | `client.unhandled` | `Diagnostics/ReportClientIssue.cs` |
| 5000 / 5001 / 5002 | `service.started` / `service.stopping` / `service.startup.failed` | `Host/Program.cs`、`DiagnosticStartup.cs` |
| 6001 / 6002 / 6003 | `attachment.delete_deferred` / `attachment.untracked_delete_deferred` / `attachment.cleanup_deferred` | `Attachments/AttachmentLifecycle.cs` |
| 6101 | `share.cleanup_deferred` | `Sharing/ShareService.cs` |

`service.startup.failed` 不經 logger，由 `DiagnosticStartup` 直接寫入 journal。

## 新模組加入方式

不需引用檔案或 SQL sink：

```csharp
public sealed partial class ConnectorClient(ILogger<ConnectorClient> logger)
{
    private async Task RetryAsync(Exception transient, int attempt)
    {
        // ID 來自已授權的業務物件；不要使用使用者傳入的身分或 trace 值。
        using var scope = logger.BeginScope(new Dictionary<string, object?> {
            ["ResourceId"] = authorizedResource.Id, ["ExternalService"] = "approved-connector" });
        LogRetry(logger, transient, attempt);
    }

    [LoggerMessage(EventId = 6201, EventName = "connector.retry", Level = LogLevel.Warning, Message = "External operation will retry at attempt {Attempt}.")]
    private static partial void LogRetry(ILogger logger, Exception exception, int attempt);
}
```

- RPC 重試或降級後成功時記一次 Warning，保留穩定的 typed reason。最終失敗直接 throw 給共用的 HTTP／job／run 邊界，不要每層重複記 Error；同一例外跨層拋出時，`Issues.Report` 只記一次。
- `IBackgroundJobHandler` 自動帶有 Job／Attempt 關聯；不要把 request scope 帶進佇列。
- 新增子 Activity 時用標準 ActivitySource、只用核准標籤，parent 用共享 Source 或 `DiagnosticTrace.Start` 的可信 parent。
- 新增 metadata 先更新白名單、欄位上限與敏感資料測試。新增公開錯誤代碼的 4xx 提示同步更新前後端 catalog 與契約；例外 Message 不能當提示。
- 授權、管理設定或特權資料讀取用既有交易內的 AuditEvent，保存失敗就取消操作；logger 不能取代稽核。
