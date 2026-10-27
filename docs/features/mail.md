# 郵件寄送

結論：系統信一律先寫進 `mail.OutboundMessages`（outbox），由背景工作寄出並記下每次嘗試；「已寄出」只代表 SMTP relay 已接受，不代表已進收件匣。寫程式寄信見 [郵件 outbox](../architecture/mail-outbox.md)。

## 設定

`Mail` 區段（[`MailOptions`](../../backend/src/AiNexus.Features/Admin/Mail/MailOptions.cs)），改完重啟生效。密碼 `Mail:Smtp:Password` 不進 `appsettings.json`，放秘密檔或環境變數 `Mail__Smtp__Password`（[設定與秘密](../development/configuration.md)）。

| `Transport` | 用途 |
| --- | --- |
| 空值 | Development 補成 `Pickup`（寫到 `.local/mail`）；Testing 補成 `Disabled`；**其他環境啟動失敗** |
| `Smtp` | 必填 `Smtp.Host`、`From.Address`、`PublicBaseUrl` |
| `Pickup` | 寫成 `<Directory>/<yyyyMMdd>/<Id>.eml` 不寄出；可指向 IIS SMTP 的 pickup 目錄（站外絕對路徑） |
| `Disabled` | 只入列不寄出；改回 `Smtp` 後未過期的信會補寄 |

| 情境 | `Smtp` |
| --- | --- |
| 公司 IIS SMTP（只開 25） | `Port` 25、`Security`＝`StartTls`、`UserName`＋密碼（伺服器提供 AUTH LOGIN 即可，不需要 PLAIN）；匿名 relay 則 `UserName` 留空，由 mail server 允許 IIS 主機 IP |
| 公司 mail server（587／465） | 587＋`StartTls`，或 465＋`SslOnConnect` |
| Google Workspace SMTP relay | `smtp-relay.gmail.com`、587、`StartTls`、`LocalDomain`＝已登記網域；Admin Console 設 IP 允許或 SMTP AUTH |
| Gmail SMTP | `smtp.gmail.com`、587、兩步驟驗證＋App Password；每日約 2,000（個人 500）封，**必設 `MaxPerDay`** |

- 憑證：名稱、有效期與信任鏈完整驗證；只有「撤銷狀態查不到」（內部 CA 沒有 CRL／AIA）會容忍，已撤銷照樣拒絕。內部 CA 的根要裝在 IIS 主機的「受信任的根憑證授權單位」。
- `ServerCertificateThumbprint` 只給自簽或根不受信任的伺服器：把[連線診斷](#連線診斷)顯示的 SHA-256 指紋貼上即可；換憑證就要更新，所以能修信任鏈就不要釘選。沒有「略過驗證」；`Security=None` 只允許 loopback。
- `PublicBaseUrl` 是信內連結的 origin；`AllowedRecipientDomains` 在 UAT 設公司網域，避免誤寄外部。
- 上線前：寄件網域的 SPF、DKIM、DMARC 要與 `From.Address` 對齊。

## 連線診斷

寄送紀錄頁的「連線診斷」以目前設定同步跑一次 SMTP 對話：TCP → EHLO → STARTTLS → 憑證 → AUTH → MAIL FROM → RCPT TO → RSET，不送內容、不寄出、不經過 outbox。每一步顯示通過／注意／失敗與原因，並附憑證（主旨、簽發者、SAN、有效期、驗證結果、SHA-256 指紋）、伺服器能力與對話紀錄（AUTH 已遮罩）。收件地址選填，空白時以寄件地址檢查 RCPT。每人每分鐘 5 次，稽核 `mail.transport.diagnosed`（只記結果與失敗步驟）。

| 失敗在 | 通常是 |
| --- | --- |
| TCP 連線 | 主機或埠錯、防火牆 |
| EHLO／TLS | 伺服器沒提供 STARTTLS（改 `SslOnConnect` 或請 mail server 啟用 TLS）、TLS 版本不合 |
| 伺服器憑證 | 名稱不符（`Host` 要用憑證上的名稱）、根不受信任（裝根憑證或釘選指紋）、過期 |
| 帳密驗證 | 帳密錯、伺服器沒提供 AUTH 或只在 TLS 後提供 |
| 寄件者／收件者 | 帳號不能以 `From.Address` 寄信、relay 不允許這個收件網域 |

## 寄送與重試

- 背景工作每 2 秒認領一批，安全信（驗證碼）優先；`MaxConcurrency` 條連線、每分鐘最多 `MaxPerMinute`、過去 24 小時最多 `MaxPerDay` 封（0＝不限）。一封信一位收件者。

| 結果 | 例 | 自動重試開啟時 |
| --- | --- | --- |
| 暫時性 | 4xx、逾時、斷線 | 退避 1、5、15、30、60 分鐘，之後每小時，±20% |
| 永久性 | 550 收件者不存在、552 太大 | 失敗（`mail_rejected`），不再重試 |
| 本地拒絕 | 地址不合法、網域不允許、過了有效時間、超過 `GiveUpAfterHours` | 失敗（`mail_address_invalid`、`mail_recipient_blocked`、`mail_expired`、`mail_gave_up`） |
| 傳輸層故障 | 認證、TLS／憑證、relay 被拒、連續 3 次連不上 | 這封等到暫停結束；**暫停寄送 5 分鐘**，記 `mail.transport_unavailable` |

- 每次嘗試記下失敗的步驟（連線、TLS、認證、寄件者、收件者、內容）與遮罩後的伺服器回應或例外訊息；TLS 失敗另記憑證摘要與指紋。
- 傳輸恢復（暫停後第一封寄出）或網站重啟時，因連線或傳輸故障延後的信提前到現在；4xx 是伺服器要求稍後再試，照原退避。
- **立即重試**：詳情裡等待中的信可現在就寄（同一封、同一個 Message-ID），暫停中也一併結束暫停；稽核 `mail.message.retried`。已結束的信用「重寄」。
- **自動重試開關**：頁首「自動重試」存在 `mail.DeliverySettings`，下一輪就生效、不必重啟（`IOptions` 要重啟，所以不放 `MailOptions`）。關閉時暫時性與傳輸層失敗第一次就結束；斷路器仍會暫停，避免佇列其他信一起失敗；`GiveUpAfterHours` 只剩擋住排隊太久的信。稽核 `mail.settings.updated`。
- **可能重複**：relay 已接受但寫回前程序中止，租約（120 秒）到期後再寄一次，Message-ID 不變。

## 寄送紀錄頁

「郵件寄送紀錄」在 `/admin/mail/messages`，初始只授予 administrators。列表、詳情與頁首狀態在有進行中的信時每 3 秒更新、否則第一頁每 30 秒，狀態變化有動效，所以沒有「重新整理」：同條件再按「查詢」即重讀第一頁。頁面右上角與詳情抽屜可開啟操作導覽。

| 功能 | 能做的事 |
| --- | --- |
| `mail.messages` | 查詢、詳情、頁首狀態、連線診斷、寄信測試（每人每分鐘 3 封，稽核 `mail.test_message.sent`，不記收件地址與參數） |
| `mail.messages.resend` | 加上重寄（以原內容建立新信，`ResendOfId` 指向原信，稽核 `mail.message.sent`）、立即重試、自動重試開關 |

- 搜尋依格式判斷：完整 email（比對雜湊）、訊息或批次 ID、Message-ID、主旨開頭。新到舊、游標分頁。
- **寄信測試**：可選任一已登記的系統信（設定測試信、驗證碼、帳號安全通知），參數預填範例值、可改；照常排隊寄出，分類維持原值、主旨加「[測試]」（設定測試信除外）。「開發者用法」顯示以目前參數入列這封信的 C#。

## 查證有沒有寄出

1. 紀錄的 Message-ID 就是信件標頭的 Message-ID，拿去 mail server 或 Google Admin 的「電子郵件記錄搜尋」查投遞結果。
2. 非預期失敗另有查證代碼，可到 [系統日誌](system-logs.md) 查；退信（DSN）不回寫紀錄。

## 隱私與保存

- 列表只顯示遮罩收件者，另存小寫地址的 SHA-256 供搜尋（查詢鍵，不是匿名化）。
- 驗證碼等敏感信：內文以 Data Protection 加密，寄出或結束後刪除，不能檢視或重寄。日誌不記收件者、主旨與內文。
- `Retention`：內文 `ContentDays`（30）、收件原址 `RecipientDays`（90）、紀錄 `RecordDays`（365）；每小時清理已結束的信。
