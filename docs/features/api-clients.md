# 外部系統的 AD 帳密驗證

結論：其他系統（公文系統等）用 AiNexus 發的金鑰呼叫 `POST /api/v1/credential-verifications`，AiNexus 只回答「這組 AD 帳密是誰」；登入後的授權、帳號狀態都由呼叫端自己決定。

- 不建立 AiNexus 帳號、不看 AiNexus 的帳號狀態與角色，也不限制哪些人能登入哪個系統。
- 一個系統一把金鑰。管理頁在「外部系統」（功能 `identity.api-clients`，與使用者管理分開授權）。

## 管理金鑰

- **新增**：輸入系統名稱（唯一）與聯絡人（選填），畫面顯示一次金鑰 `ainexus_…`（51 字元）。伺服器只存 SHA-256，關閉後無法再查看，請當場交給對方存進設定或秘密庫。
- **重新產生**：舊金鑰在同一刻失效、沒有重疊期。定期更換時先請對方準備好，按下後立刻換上新金鑰。產生超過一年的金鑰，日期會以警示色提醒。
- **停用**：金鑰不變但一律回 401，重新啟用後對方不必改設定；暫時封鎖或排查問題時用它，不要用刪除。
- **編輯**名稱與聯絡人不換金鑰；**刪除**後金鑰立即失效、無法復原。
- 「最近使用」與「近 30 天」次數取自帳密驗證的稽核（`identity.credentials.verified`，對象是該系統，不論結果），由只收這個動作的 filtered 索引 `ix_AuditEvents_ResourceId_At` 計算；長期未使用的系統應刪除。
- 查詢列可依狀態篩選，搜尋名稱、聯絡人或金鑰末四碼（對方回報末四碼時反查系統）。
- 新增、修改（含停用、啟用）、重新產生與刪除都寫稽核；金鑰本身不進稽核或日誌，只留末四碼。

## 呼叫方式

```http
POST /api/v1/credential-verifications
Authorization: Bearer ainexus_…
Content-Type: application/json

{ "account": "bob", "password": "…" }
```

- `account` 可寫 `帳號`、`網域\帳號` 或允許的 `帳號@網域`。
- 不需要 cookie 或 CSRF token；這個端點也不接受登入的 cookie，金鑰也不能呼叫其他端點。
- 一律走 HTTPS，並驗證伺服器憑證；不要停用 TLS 驗證、不要把密碼或金鑰寫進日誌。

| 狀態 | 代碼 | 呼叫端怎麼做 |
|---|---|---|
| 200 | — | 回傳 `sid`、`account`、`displayName`、`email`（可能為 null）。以 `sid` 對應自己的使用者：帳號改名也不變 |
| 422 | `credentials_rejected` | 帳號或密碼錯、帳號在 AD 停用都回這個，不透露是哪一種；顯示「帳號或密碼不正確」 |
| 429 | `login_rate_limited` | 同一帳號失敗太多次（與網站登入共用計數），依 `Retry-After` 秒數後再試；系統整體超過每分鐘 300 次也回 429 |
| 401 | `authentication_required` | 金鑰缺少、錯誤、已失效或系統已停用（帶 `WWW-Authenticate: Bearer`）；是設定問題，不要當成密碼錯 |
| 503 | — | AD 連不上；回應帶查證代碼，請對方提供給管理員查日誌。不要當成密碼錯 |

完整結構見 [`contracts/openapi.json`](../../contracts/openapi.json) 的 `VerifyCredentials`。實作在 `backend/src/AiNexus.Features/Admin/Identity/Authentication/VerifyCredentials.cs`，金鑰驗證與每個系統的限流在 `backend/src/AiNexus.Features/Admin/Identity/ApiClients/ApiClientAccess.cs`。

## 為什麼這樣設計

- **金鑰只存雜湊、不快取**：資料庫外洩拿不到可用的金鑰；停用、重新產生或刪除立即生效。
- **錯誤分成三類**：帳密錯（422）、呼叫端設定錯（401）、上游故障（503），呼叫端才不會把 AD 斷線當成密碼錯而鎖住使用者。
- **共用失敗計數**：換個入口不能多猜幾輪；被擋時不再查 AD，也不會讓 AD 因此鎖住帳號。
