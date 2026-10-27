# 手動排序

結論：由管理者決定先後的資料（使用者、選單）存一個可為 null 的整數 `SortOrder`，值之間留間隔，一次移動通常只寫被移動的那一筆；畫面上直接在列表上拖曳、按方向鍵或輸入編號，不另開排序對話框。

## 資料

- 欄位 `SortOrder INT NULL`，加 `ck_<表>_SortOrder` 限制範圍（使用者：0 至 1,000,000，程式常數 `UserOrder.MaxSortOrder`）。不加唯一約束：同值時依後面的條件排，不會出錯。
- 順序規則：有值的在前、依值；null（未排序）在後；再依名稱（ordinal）、帳號（不分大小寫）、Id。程式用 [`UserOrder.InUserOrder`](../../backend/src/AiNexus.Data/App/Identity/UserOrder.cs)，SQL 用 `COLLATE Latin1_General_100_BIN2`（見 `operations.usp_ListAdminUsers`），兩邊結果必須一致。
- 新資料接在最後一位已排序者之後（同一套值計算），不混進依名稱排列的未排序區；還沒有人排序時維持 null，否則新資料會變成唯一已排序的而排到最前面。使用者的管理員新增、從 AD 匯入、AD 首次登入都走 [`NewUserOrder`](../../backend/src/AiNexus.Features/Admin/Identity/Ordering/NewUserOrder.cs)。清除排序就是全部設回 null。

## 值怎麼算

同一套規則有兩份實作，測試用同一組情境：伺服器 [`ManualOrder`](../../backend/src/AiNexus.Platform/Data/ManualOrder.cs)、前端 [`sort-order.ts`](../../frontend/src/app/shared/browser/sort-order.ts)（`sortWrites`）。

- 落在已排序範圍內：取前後值的中間；最前面取下一個減 10，最後面取上一個加 10。
- 沒有空間（相鄰整數）時把已排序的整段重新編號為 10、20、30…；上限不夠時縮小間隔。
- 落在未排序者之間或之後：它上方的未排序者依畫面順序接在最後一個值之後，一起變成已排序。畫面上看到的順序就是存下來的順序，它下方的人維持未排序。
- 原地放下不寫入，未排序的人也不會因此變成已排序。

## 誰算

- 清單會分頁或搜尋（使用者）：前端看不到完整順序，只送「移到第幾位」（`PUT /api/v1/admin/users/{id}/position`，從 1 起算、超過人數放最後）；伺服器讀整份名單，在交易內重算一次，與交易前算的不同（期間有人改了順序或刪了人）就拒絕 `user_order_stale`，不寫半套。
- 整份清單都在畫面上（選單）：前端用 `sortWrites` 算出要寫的值，一次送出。
- 列表回傳每列在整份清單的位置（`position`）與已排序人數（`sorted`），搜尋時編號仍是整份清單的位置。

## 畫面

共用 [`sortable-rows.ts`](../../frontend/src/app/shared/ui/sortable-rows.ts)，範例在 `/admin/design` 的「手動排序」。

- `tbody`（或 `ol`）加 `nxSortableRows`，列標 `data-sort-id`，第一欄放 `nx-sort-handle`（把手）與 `nx-sort-rank`（位置編號）。
- 拖曳：列浮起跟著指標，其他列以彈簧讓位；放手後送出 `sortMove`，呼叫端同步換序（樂觀更新），指令以 FLIP 讓列從放手時的位置滑到新格子。Esc 取消並放回原位。
- 鍵盤：在把手上按 ↑↓ 移一位、PageUp／PageDown 十位、Home／End 到兩端；焦點留在把手，列閃一下，報讀「移到第 N 列」。
- 輸入編號：點編號換成輸入框，Enter 移動、Esc 取消；搜尋中也能用。
- 搜尋或寫入中給 `sortDisabled`：把手停用並說明原因（看不到鄰居，拖曳沒有意義）。寫入期間不能再移動，避免第二次移動以舊位置計算。
- 未排序的編號淡化；第一個未排序的列標 `is-first-unsorted`，上緣畫虛線當分界。表頭計數寫「已排序 N 位」，有排序時才顯示「清除順序」（確認後執行）。
- 失敗時換回原本的列、顯示錯誤；成功後重讀本頁取得伺服器的值與編號。
- 減少動態時不播讓位與落定動畫。

## 稽核

- 使用者：`identity.user.moved`（一次移動，快照記改到的人的 `id`、`sortOrder`）、`identity.user.reordered`（清除，快照記清除前所有已排序的人，誤按時可據以還原）。原地放下不寫入，但請求照樣留一筆沒有 `before` 的稽核（每個請求都記，見 [ADR 0023](../decisions/0023-audit-standard.md)）。

## 用在別的資料時

1. 資料表加 `SortOrder INT NULL` 與範圍約束，列表的 procedure 或查詢照上面的順序規則排。
2. 會分頁或搜尋：做「移到第幾位」端點，用 `ManualOrder.Plan` 算值、在交易內重算並比對；不會分頁：前端用 `sortWrites`。
3. 畫面用 `nxSortableRows`＋`nx-sort-handle`＋`nx-sort-rank`，不寫新的拖曳程式。
4. 依該資料分組的列表（例：出勤紀錄依人）改用同一個順序規則，並在功能文件註明。
