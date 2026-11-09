# 工作階段鎖定

結論：閒置或太久沒驗證時只鎖定不登出，輸入密碼就回到剛才的畫面；判定在伺服器，前端計時只負責及時顯示。

## 期限

設定在 `Identity:Session`（[`SessionLifetimeOptions`](../../backend/src/AiNexus.Features/Admin/Identity/Sessions/SessionLifetimeOptions.cs)）。解鎖或重新登入後三個期限都重新起算。

| 設定 | 預設 | 到期後 |
| --- | --- | --- |
| `IdleLockMinutes` | 30 分鐘 | 沒有使用者操作：鎖定 |
| `ReauthenticationHours` | 8 小時 | 距上次輸入密碼：鎖定，有在操作也一樣 |
| `HardLimitHours` | 24 小時 | 距上次輸入密碼：cookie 失效，回登入頁 |

依據：OWASP Session Management（閒置 15–30 分鐘、絕對期限 4–8 小時，必須伺服器強制）；NIST 800-63B-4（AAL2 閒置 ≤1 小時、總時長 ≤24 小時，閒置後可用密碼重新驗證）。

## 為什麼不靠前端倒數

Chrome 會節流背景分頁的計時器，隱藏 5 分鐘後最多每分鐘跑一次，倒數到期時間不可靠。

- 伺服器在 cookie 驗證時判定，鎖定中的 API 一律回 401 `session_locked`，資料不會再出去。
- 前端計時器只決定何時去問伺服器；切回分頁時立刻檢查一次。
- 輪詢不算操作：只有 `POST /auth/activity` 會延後閒置鎖定，前端把操作節流成每分鐘最多回報一次（不超過閒置期限的一半），並用 `BroadcastChannel` 同步各分頁。

## 鎖定時保留什麼

鎖定不換路由，也不動 cookie 的身分（claims 不變，CSRF token 繼續有效）。

- 頁面、草稿留在記憶體；已開始的串流不中斷（長串流在 `CurrentUser` 重驗，不經 cookie 驗證）。
- 新請求停在 `ApiClient` 的閘門，解鎖後才送出；鎖定瞬間被拒絕的請求會原樣重送，伺服器在 handler 之前就拒絕，寫入不會重複。
- 鎖定中仍可用的端點只有查狀態、解鎖與登出（端點標 `.AllowWhileLocked()`）。

## 例外

- Windows 整合驗證每個請求都由 Kerberos／NTLM 重驗，沒有 cookie，不會鎖定。
- 測試身分不鎖定（要輸入的是別人的密碼）：閒置或到期就結束測試、回到管理者，管理者閒置時接著鎖定，用管理者自己的密碼解開。
- 密碼不可用（AD 不通、帳號被鎖）時，畫面上永遠有「改用其他帳號登入」。

## 已知限制

- 鎖定後已載入的畫面仍在瀏覽器記憶體，DevTools 看得到；拿不到新資料。要清掉就改用其他帳號登入（整頁重新載入）。
- 上線當下已閒置超過期限的 cookie 會被鎖一次（舊 cookie 以簽發時間起算），不需要資料庫變更。

## 開發

- 簽發只走 [`SessionTicket`](../../backend/src/AiNexus.Features/Admin/Identity/Sessions/SessionTicket.cs)：剛驗過密碼用 `IssueAsync`，換 principal 用 `ReissueAsync`（沿用期限）。不要直接呼叫 `SignInAsync` 或設 `ShouldRenew`（會把到期時間往後滑）。
- 兩個時間點放在票證的 Items，不放 claims：防偽 token 綁定 claims。
- 畫面在 [`SessionLockScreen`](../../frontend/src/app/features/admin/identity/sessions/session-lock-screen.ts)，keyframes 留在元件樣式（只有它用，不進初始 bundle），程式用 `getAnimations()` 等動畫結束，長度只寫在樣式表。
