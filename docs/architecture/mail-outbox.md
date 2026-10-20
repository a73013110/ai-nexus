# 郵件 outbox

結論：其他模組寄信只用 `MailOutbox`（`AiNexus.Features.Admin.Mail`），在自己的交易內入列、自己 `SaveChangesAsync`；不要直接呼叫 SMTP，也不要用 `BackgroundJobs` 寄信。設定、重試與保存見 [郵件寄送](../features/mail.md)。

## 寫一封信

1. 在呼叫模組的 slice 旁寫範本：繼承 [`MailTemplate<TModel>`](../../backend/src/AiNexus.Features/Admin/Mail/MailTemplate.cs)，宣告 `Category`（小寫點分，例 `account.email-change-code`）、`Priority`、`Sensitive`、`Subject` 與 `Body`。
2. `Body` 只能用 [`MailBody`](../../backend/src/AiNexus.Features/Admin/Mail/MailBody.cs) 的語意區塊（Heading、Paragraph、Code、Button、Note、KeyValue），同時產生 HTML 與純文字，值一律編碼；範本無法插入原始 HTML。
3. 連結用 `body.Button(label, FeatureCatalog.RouteOf(...))`：只接受站內路徑，origin 由 `Mail:PublicBaseUrl` 補上。
4. 入列：

```csharp
var enqueued = await outbox.EnqueueAsync(new EmailChangeCodeMail(), model, new(address, user.DisplayName, user.Id),
    new(IdempotencyKey: $"account.email-change-code:{challenge.Id}", ExpiresAt: challenge.ExpiresAt, RequestedBy: user.Id), ct);
if (!enqueued.IsSuccess) return enqueued.Error;   // mail_address_invalid／mail_recipient_blocked
await db.SaveChangesAsync(ct);                   // 業務資料與信件一起提交
```

## 規則

- **交易**：`EnqueueAsync`／`EnqueueManyAsync` 只 `db.Add`，不儲存；業務失敗或回滾就不會寄出，寄信也不會早於業務提交。範本在入列時渲染，紀錄保存的就是寄出的內容。
- **冪等鍵**：`<category>:<業務 ID>[:<收件者 UserId>]`。同一交易未儲存的列與資料庫都會檢查，唯一索引再擋並行；重複時回傳既有那封（`Duplicate=true`）。
- **敏感信**（`Sensitive=true`，驗證碼）：內文加密、寄出或結束後刪除、不能重寄。秘密不可放主旨（主旨不加密）。驗證碼信設 `ExpiresAt`，過期就不寄；被新驗證碼取代時呼叫 `CancelAsync`。
- **傳輸停用**：`MailOutbox.Available` 為 false 時仍可入列但不會寄出；使用者正在等信的流程應先檢查並回 `MailErrors.NotConfigured`（503）。
- **狀態**：`GetDeliveryAsync` 回傳 queued／sending／sent／failed／cancelled 與錯誤代碼，不含收件者與內文，供「寄送中／已寄出」提示。
- **大量**：`EnqueueManyAsync` 一次入列同一 `BatchId`，不合法的收件者寫成 failed 列，不讓整批失敗；約 2000 封以上由呼叫端以 durable job 分段入列（冪等鍵讓重試安全）。優先序用 `MailPriority.Bulk`，安全信不會卡在後面。

## 寄送端（Mail 模組內部）

- `Delivery/MailDispatcher`：單一 `BackgroundService`（ADR 0001），以 `UPDLOCK, READPAST` 條件式 UPDATE 批次認領並加租約，結果以 `LeaseToken` fencing 寫回、每次嘗試一列 `DeliveryAttempts`；節流與斷路器只在記憶體。
- `Delivery/MailFailures` 把 MailKit 例外分成暫時、永久與傳輸層故障；新增分類時同步更新 [郵件寄送](../features/mail.md) 的表與前端標籤。傳輸把例外包成 `MailSendException` 帶出失敗步驟（`MailAttemptStages`）與憑證摘要，分類仍看原始例外。
- `IMailTransport`：`SmtpMailTransport`（MailKit）、`PickupMailTransport`、`DisabledMailTransport`；測試以 `RecordingMailTransport` 取代，並直接呼叫 `MailDispatcher.DispatchAsync`、`MailRetentionWorker.PurgeAsync`。
- 寄送與連線診斷（`Transport/SmtpProbe`）共用 `SmtpConnector` 與 `SmtpCertificate`：逾時、EHLO 網域與憑證規則只有一份，診斷通過就代表寄送走同一條路。SMTP 行為的單元測試用 `FakeSmtpServer`（loopback，可開 STARTTLS）。
- 執行期開關放 `mail.DeliverySettings`（`MailDeliverySettings`），dispatcher 每輪讀一次；要和秘密一起部署的設定才放 `MailOptions`。
- Gmail API／OAuth（XOAUTH2）未實作：組織禁用 SMTP AUTH 時，在 `SmtpMailTransport` 加 OAuth2 驗證即可，outbox 不必改。
