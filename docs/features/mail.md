# 郵件寄送

結論：系統信（驗證碼、通知）一律先寫進 `mail.OutboundMessages`（outbox），由背景工作依序寄出並留下每次嘗試的結果；「已寄出」只代表 SMTP relay 已接受，不代表已投遞到收件匣。寫程式寄信的方法見 [郵件 outbox](../architecture/mail-outbox.md)。

## 傳輸設定

設定在 `Mail` 區段（[`MailOptions`](../../backend/src/AiNexus.Features/Admin/Mail/MailOptions.cs)），密碼 `Mail:Smtp:Password` 放秘密檔或環境變數 `Mail__Smtp__Password`（[設定與秘密](../development/configuration.md)）。

| `Transport` | 用途 |
| --- | --- |
| 空值 | Development 補成 `Pickup`（寫到 `.local/mail`、寄件者 `no-reply@ainexus.localhost`、連結 `https://localhost:5080`）；Testing 補成 `Disabled`；**其他環境啟動失敗**，必須明確設定 |
| `Smtp` | 公司 mail server 或 Google；必填 `Smtp.Host`、`From.Address`、`PublicBaseUrl` |
| `Pickup` | 把信寫成 `<Directory>/<yyyyMMdd>/<Id>.eml`，不實際寄出；也可指向 IIS SMTP 的 pickup 目錄。非 Development 必須是站外絕對路徑 |
| `Disabled` | 只入列不寄出（管理頁顯示未啟用）；之後改成 `Smtp` 時，未過期的信會補寄 |

| 情境 | `Smtp` 設定 |
| --- | --- |
| 公司 mail server（匿名 relay） | `Host`＝主機、`Port` 25 或 587、`Security`＝`StartTls`，`UserName` 留空；由 mail server 允許 IIS 主機 IP relay |
| 公司 mail server（帳密） | 同上，加 `UserName`＋`Password` |
| Google Workspace SMTP relay | `smtp-relay.gmail.com`、587、`StartTls`、`LocalDomain`＝已登記網域；Admin Console 設 IP 允許或 SMTP AUTH。每使用者每日 10,000 收件者 |
| Gmail SMTP | `smtp.gmail.com`、587、帳號需兩步驟驗證＋App Password；Workspace 每日約 2,000、個人 500 封，**必設 `MaxPerDay`** |

- 憑證預設完整驗證；自簽的內部 server 以 `ServerCertificateThumbprint`（SHA-1 或 SHA-256）釘選，沒有「略過驗證」。`Security=None` 只允許 loopback。
- `PublicBaseUrl` 是信內連結的 origin，不從請求 Host 取；`AllowedRecipientDomains` 在 UAT 設公司網域，避免誤寄外部。
- 上線前：寄件網域的 SPF、DKIM、DMARC 要與 `From.Address` 對齊，否則容易被當垃圾信。

## 寄送與重試

- 背景工作每 2 秒認領一批，安全信（驗證碼、密碼變更通知）優先；`MaxConcurrency` 條連線同時寄，每分鐘最多 `MaxPerMinute` 封，過去 24 小時最多 `MaxPerDay` 封（0＝不限）。1000 封 @ 60／分約 17 分鐘。
- 一封信一位收件者；單封失敗不影響其他封。

| 結果 | 例 | 處理 |
| --- | --- | --- |
| 暫時性 | 4xx、逾時、斷線 | 退避 1、5、15、30、60 分鐘後再寄，之後每小時，±20% |
| 永久性 | 550 收件者不存在、552 太大 | 失敗（`mail_rejected`），不再重試 |
| 本地拒絕 | 地址不合法、網域不允許、過了有效時間、入列超過 `GiveUpAfterHours` | 失敗（`mail_address_invalid`、`mail_recipient_blocked`、`mail_expired`、`mail_gave_up`） |
| 傳輸層故障 | 認證失敗、TLS／憑證、relay 被拒、連續 3 次連不上 | 這封退回佇列，**暫停寄送 5 分鐘**，記 `mail.transport_unavailable`；監控顯示異常，恢復後記 `mail.transport_restored` |

- 放棄以時間為準（預設 24 小時），不以次數為準；傳輸層故障期間其他信不會耗盡重試。
- **可能重複**：relay 已接受但寫回紀錄前程序中止，租約（120 秒）到期後會再寄一次，紀錄多一筆「中止」嘗試。Message-ID 不變，收件端有機會去重。

## 寄送紀錄頁

「郵件寄送紀錄」在 `/admin/mail/messages`（功能 `mail.messages`，初始只授予 administrators），選單在「系統管理」的即時監控之後。

| 功能 | 能做的事 |
| --- | --- |
| `mail.messages` | 查詢、詳情、頁首狀態、寄測試信（每人每分鐘 3 封，稽核 `admin.mail_test`，不記收件地址） |
| `mail.messages.resend` | 加上 `mail.messages`，才能重寄：以原內容建立新信（`ResendOfId` 指向原信），原紀錄不變，稽核 `admin.mail_resend` |

- 依狀態切換；搜尋依格式判斷：完整 email（比對雜湊）、訊息或批次 ID、Message-ID、主旨開頭。新到舊、游標分頁，不算總數。
- 詳情依序是重寄、每次嘗試（SMTP 狀態、遮罩後回應、查證代碼）、純文字內文（以 `<pre>` 顯示，HTML 不渲染）、基本資訊與可複製的 ID。
- 傳輸停用或暫停寄送時頁首提示並連到監控；測試身分可以查看，不能重寄或寄測試信。
- 測試信目前要輸入收件地址；使用者信箱上線後改為寄到操作者已驗證的信箱。

## 查證「到底有沒有寄出」

1. 紀錄的 Message-ID（`<訊息 ID@網域>`）就是信件標頭的 Message-ID；拿它到 mail server 或 Google Admin 的「電子郵件記錄搜尋」查投遞結果。
2. 每次嘗試保存 SMTP 狀態碼、擴充狀態碼與遮罩後的回應；非預期失敗另有查證代碼，可到 [系統日誌](system-logs.md) 查。
3. 退信（DSN）不會回寫紀錄：「已寄出」之後被收件端退回，要看寄件信箱或 mail server。

## 隱私與保存

- 列表只顯示遮罩後的收件者（`c***n@company.com`），另存小寫地址的 SHA-256 供搜尋；公司地址可被猜出，所以這是查詢鍵，不是匿名化。
- 含驗證碼的信標為敏感：內文以 Data Protection 加密保存，寄出或結束後立即刪除；管理端看不到內文，也不能重寄。金鑰圈遺失時這類信會失敗（`mail_content_unavailable`）。
- 日誌只記訊息 ID、分類代碼與例外型別，不記收件者、主旨與內文。
- `Retention`：內文 `ContentDays`（30）、收件原址 `RecipientDays`（90，之後不能重寄）、紀錄 `RecordDays`（365）；每小時清理一次，只處理已結束的信。
