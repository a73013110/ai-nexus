# 工作階段鎖定

結論：閒置、太久沒驗證或手動鎖定時只鎖定不登出，輸入密碼就回到剛才的畫面；判定在伺服器，前端計時只負責及時顯示。

## 期限

設定在 `Identity:Session`（[`SessionLifetimeOptions`](../../backend/src/AiNexus.Features/Admin/Identity/Sessions/SessionLifetimeOptions.cs)）。解鎖或重新登入後三個期限都重新起算。

| 設定 | 預設 | 到期後 |
| --- | --- | --- |
| `IdleLockMinutes` | 30 分鐘 | 沒有使用者操作：鎖定 |
| `ReauthenticationHours` | 8 小時 | 距上次輸入密碼：鎖定，有在操作也一樣 |
| `HardLimitHours` | 24 小時 | 距上次輸入密碼：cookie 失效，回登入頁 |

依據：OWASP Session Management（閒置 15–30 分鐘、絕對期限 4–8 小時，必須伺服器強制）；NIST 800-63B-4（AAL2 閒置 ≤1 小時、總時長 ≤24 小時，閒置後可用密碼重新驗證）。

手動鎖定：帳號列的鎖頭（`POST /auth/lock`）。cookie 記下鎖定，期限不變，只有輸入密碼解得開。

## 畫面上的倒數

- 帳號列顯示剩餘時間，最後 2 分鐘變色並從頂端落下倒數島，最後 20 秒展開「繼續使用」；到期時鎖定卡從島的位置撐開。
- 算操作的只有點擊、按鍵與滾輪（任何分頁都算），滑鼠移動不算。
- 本機操作當下就算進倒數；回報有節流，所以附上操作後經過的秒數，伺服器從操作當下起算，沒有操作時數字不會跳回去。
- 頂端中央由 `TopIslandLane` 協調：倒數島出現時，助理的圓鈕讓到旁邊。
- 8 小時重新驗證無法靠操作延長，所以島上是「現在驗證」。
- 閒置期限很短時，最多只在後半段提醒。

## 為什麼不靠前端倒數

Chrome 會節流背景分頁的計時器，隱藏 5 分鐘後最多每分鐘跑一次，倒數到期時間不可靠。

- 伺服器在 cookie 驗證時判定，鎖定中的 API 一律回 401 `session_locked`，資料不會再出去。
- 前端計時器只決定何時去問伺服器；切回分頁時立刻檢查一次。
- 輪詢不算操作：只有 `POST /auth/activity` 會延後閒置鎖定，前端把操作節流成每分鐘最多回報一次（不超過閒置期限的一半），並用 `BroadcastChannel` 同步各分頁。

## 鎖定時保留什麼

鎖定不換路由，也不動 cookie 的身分（claims 不變，CSRF token 繼續有效）。

- 頁面、草稿留在記憶體；已開始的串流不中斷（長串流在 `CurrentUser` 重驗，不經 cookie 驗證）。
- 新請求停在 `ApiClient` 的閘門，解鎖後才送出；鎖定瞬間被拒絕的請求會原樣重送，伺服器在 handler 之前就拒絕，寫入不會重複。
- 鎖定中仍可用的端點只有查狀態、鎖定、解鎖與登出（端點標 `.AllowWhileLocked()`）。
- 背景是暗幕加重模糊：看得到原本的版面與顏色，看不出文字；瀏覽器不支援模糊時改為完全遮住。

## 小鎖

鎖定卡撐開後，一隻鎖頭造型的小角色落到卡片頂緣，讓等著解鎖的畫面不那麼乾：

- 拎起來丟：落地報飛行距離，最遠紀錄依帳號存在這台瀏覽器（`localStorage`），不上傳；摔到地上會自己跳回卡片。
- 跟著密碼欄：有字就遮住眼睛，按「顯示密碼」就轉身背對，大寫鎖定開著時閉眼舉牌。舉牌只是裝飾，給螢幕閱讀器的提示仍在密碼欄下方。
- 跟著鎖定動效：倒數島出現時掛在島下打哈欠，到期撐開成卡片時從島上掉下來。解鎖成功時跳起來，自己的鎖扣和膠囊裡的同一刻彈開；輸錯被震暈。
- 看時間換作息：早上醒來伸懶腰、中午拿便當嚼兩口、晚上睡覺蓋被子、被吵醒打哈欠。
- 節日換裝：12/24–25 聖誕帽、1/1 派對帽、除夕到初五拿紅包、中秋拿月餅。農曆用瀏覽器內建的曆法換算，時間看這台電腦。
- 滑過是摸摸、點一下會跳；6 秒沒動靜睡著。
- 純裝飾：`aria-hidden`、不能聚焦，按下時不搶走密碼欄的焦點。卡片上方放不下（矮視窗、螢幕鍵盤）時先躲起來。
- 減少動態時靜靜站著：表情與打扮照樣換，不能丟也不會跳。睡著時停掉逐幀更新，長時間鎖定不耗電。

## 例外

- Windows 整合驗證每個請求都由 Kerberos／NTLM 重驗，沒有 cookie，不會鎖定。
- 測試身分不鎖定（要輸入的是別人的密碼）：不顯示倒數也不能手動鎖定；閒置或到期就結束測試、回到管理者，管理者閒置時接著鎖定，用管理者自己的密碼解開。
- 密碼不可用（AD 不通、帳號被鎖）時，畫面上永遠有「改用其他帳號登入」。

## 已知限制

- 鎖定後已載入的畫面仍在瀏覽器記憶體，DevTools 看得到；拿不到新資料。要清掉就改用其他帳號登入（整頁重新載入）。
- 上線當下已閒置超過期限的 cookie 會被鎖一次（舊 cookie 以簽發時間起算），不需要資料庫變更。
- 手動鎖定記在無狀態的 cookie：另一個分頁正在途中的活動回報，若晚於鎖定回來，會換發沒有鎖定標記的 cookie。同分頁已先等回報結束；跨分頁的時間窗只有毫秒，畫面仍維持鎖定。

## 開發

- 簽發只走 [`SessionTicket`](../../backend/src/AiNexus.Features/Admin/Identity/Sessions/SessionTicket.cs)：剛驗過密碼用 `IssueAsync`，換 principal 用 `ReissueAsync`（沿用期限）。不要直接呼叫 `SignInAsync` 或設 `ShouldRenew`（會把到期時間往後滑）。
- 兩個時間點放在票證的 Items，不放 claims：防偽 token 綁定 claims。
- 畫面在 [`SessionLockScreen`](../../frontend/src/app/features/admin/identity/sessions/session-lock-screen.ts)，keyframes 留在元件樣式（只有它用，不進初始 bundle），程式用 `getAnimations()` 等會結束的動畫，長度只寫在樣式表。
- 小鎖的物理與情緒是純計算（[`lock-pet-physics.ts`](../../frontend/src/app/features/admin/identity/sessions/lock-pet-physics.ts)），作息與節日在 [`lock-pet-calendar.ts`](../../frontend/src/app/features/admin/identity/sessions/lock-pet-calendar.ts)。樣子是 [`LockPetArt`](../../frontend/src/app/features/admin/identity/sessions/lock-pet-art.ts)，卡片上會動的與島下掛著的共用；元件 [`SessionLockPet`](../../frontend/src/app/features/admin/identity/sessions/session-lock-pet.ts) 只量版面、接指標、寫樣式。角色色在 `tokens.scss` 的 `--mascot-*`。
- 解鎖時鎖扣彈開的時間點是鎖定畫面的 `--lock-shackle-at`，膠囊與小鎖共用；要調整只改這個值。
