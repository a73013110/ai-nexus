# 表單

結論：兩組以上欄位的表單（設定頁、`md` 表單對話框）一律用 `.form-sections` 分段，間距、分隔線與寬度交給共用樣式，不在元件裡寫 margin。範本是卡號的 [`assignment-dialog.ts`](../../frontend/src/app/features/staff/attendance/cards/assignment-dialog.ts)，即時範例在 `/admin/design`「表單段落」。

## 段落排版

- 結構：`.form-sections` ＞ `section.form-section`（`role="group"` 指向標題）＞ `header.form-section-head`（`h3`＋最多一句 `.form-note`）＋ `.form-section-body`。樣式在 [`platform.scss`](../../frontend/src/styles/platform.scss)。
- 段落以細線分隔、不加框；容器夠寬時標題在左欄，窄時在上。段落依「填的人怎麼想」分：誰（持有人、收件者）、何時（期間）、怎麼存取（期限、附件）。
- 只有一兩個欄位、不分組的表單（命名、確認）不分段，欄位直接放 `.dialog-body`。
- 欄位間距只靠 `.form-section-body` 的 gap；不插空的容器（例如沒有內容時仍存在的 chip 列要用 `@if` 包住），不然會多出一段空白。
- 成對的短欄位（起訖時間、數量與單位）用 `.form-row` 並排，放不下時自動換成一欄；名稱、代碼等短欄位加 `.form-field-short`。
- 勾選框對齊欄位左緣，依附它的欄位用 `.form-subfields`。

## 欄位文字

- 標籤寫名詞；選填欄位寫「（選填）」，有預設意義的留空寫在標籤裡（「結束時間（留空＝持有中）」），不用另一段說明。
- placeholder 寫怎麼填或範例，不重複標籤。
- 填欄位需要的限制寫在欄位旁；其餘規則收進段落標題旁的 `InfoPopover`。驗證錯誤緊接在欄位下方，不放頁面提示。
- 會影響別人的結果在送出前寫明（「孔○○ 的持有期間會在開始時間結束」），送出後就不必再跳確認。

## 選擇對象

- 選人用 `PersonPicker`（搭配 `directorySearch`），結果浮在欄位下方。還沒有帳號、可以只記姓名時給 `nameHint`，最後一列是「使用姓名」。
- 只選一個對象時，選好後把搜尋框換成 `.form-choice` 一列（頭像＋名稱＋帳號＋「更換」），不留著已填的搜尋框；可選多個時，已選的放在搜尋框下方的 `.form-chips`／`.form-chip` 膠囊（頭像＋名稱，按下移除）。只有姓名、沒有帳號的人，頭像加 `.is-name`（虛線）。
- 部門、檔案等其他對象沿用同一套互動；還沒有共用元件時先抽一個，見 [共用元件](shared-components.md)。

## 控制項

- 原生 input／select／textarea 用 `nxField`，搜尋用 `SearchField`。互斥篩選或版面用 `ViewSwitch`；切換一塊內容（含對話框分類）用 `Tabs` 並連 tabpanel。共用元件不接管查詢、驗證、草稿或 API。
- 多選清單用 `ChoiceGroups`（每類一張卡片），不包總 fieldset。`Checkbox` 勾選只靠方框，列不填色。
- 日期用 `DateTimePicker` 與 `shared/browser/format.ts`：台北 wall-clock 輸入與顯示、送出轉 UTC，不讓瀏覽器把日期正規化成另一天；民國曆只影響顯示。選填（`[required]="false"`）日期無效時暫停自動查詢。
- 文字輸入：IME 組字中（含 keyCode 229）的 Enter 不送出，Shift+Enter 換行；長草稿自動增高。

## 未儲存的變更

有未儲存變更的頁面實作 `canLeave()`，路由加 `pendingChanges` guard；對話框切頁籤或關閉前用 `ConfirmDialog` 確認。
