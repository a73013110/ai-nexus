# 表單

結論：兩組以上欄位的表單（設定頁、`md` 表單對話框）一律用 `.form-sections` 分段，間距、分隔線與寬度交給共用樣式，不在元件裡寫 margin。範本是卡號的 [`assignment-dialog.ts`](../../frontend/src/app/features/office/attendance/cards/assignment-dialog.ts)，即時範例在 `/admin/design`「表單段落」。

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
- 要從整份名單挑人（看到才認得出來，例如把部門同事加入單位）用 `PersonBrowser`：依分組列出、文字與分組篩選、可多選，名單由呼叫端載入。已知道要找誰時用 `PersonPicker`。
- 已加入的人（成員、授權對象）一人一列：`ul.person-list` ＞ `li.person-row`（頭像＋`.person-row-text`＋右側勾選或下拉、移除），樣式在 `platform.scss`，不在元件裡另寫。
- 從階層選一個（單位、選單分群）用 `TreeSelect`，不用 `Select`：
  - 單位的節點由 `unitNodes()`（`features/admin/organization/unit-tree.ts`）產生；節點名稱只寫自己，不要把路徑接進名稱。觸發鈕顯示「上層路徑 › 名稱」，放不下先截路徑；搜尋時改成平面清單並標出所屬路徑。
  - 「所有單位」「最上層」這類值為空的選項用 `allLabel`，不放進節點；不能選的用節點的 `disabled`／`allDisabled`（列出但不能選，例如放不下的位置），整個排除用 `unitNodes()` 的 `include`（下層一併排除）。
  - 可選的狀態由開發者用 `status` 指定，使用者不能改：`any`（預設；停用的收起，面板底部「顯示停用的單位」可列出）、`active`（只列啟用的，例如指派新成員）、`inactive`（只能選停用的，啟用的上層只當路徑）。篩選歷史資料用 `any`。
- 選項的狀態（停用、已隱藏、尚未綁帳號）放 `Select` 選項或 `TreeSelect` 節點的 `tags`，用 `optionTags()`（`frontend/src/app/shared/ui/option-tags.ts`）組淡灰標籤，不接進名稱或說明；說明只寫帳號、代碼這類識別資訊。`TreeSelect` 的 `inactive` 自動標「停用」。
- 部門、檔案等其他對象沿用同一套互動；還沒有共用元件時先抽一個，見 [共用元件](shared-components.md)。

## 控制項

- 原生 input／select／textarea 用 `nxField`，搜尋用 `SearchField`。互斥篩選或版面用 `ViewSwitch`；切換一塊內容（含對話框分類）用 `Tabs` 並連 tabpanel。共用元件不接管查詢、驗證、草稿或 API。
- 多選清單用 `ChoiceGroups`（每類一張卡片），不包總 fieldset。`Checkbox` 勾選只靠方框，列不填色；不用原生勾選框的外觀，對齊見 [並排對齊](alignment.md)。
- 日期用 `DateTimePicker` 與 `shared/browser/format.ts`：台北 wall-clock 輸入與顯示、送出轉 UTC，不讓瀏覽器把日期正規化成另一天；民國曆只影響顯示。選填（`[required]="false"`）日期無效時暫停自動查詢。
- 文字輸入：IME 組字中（含 keyCode 229）的 Enter 不送出，Shift+Enter 換行；長草稿自動增高。

## 未儲存的變更

有未儲存變更的頁面實作 `canLeave()`，路由加 `pendingChanges` guard；對話框切頁籤或關閉前用 `ConfirmDialog` 確認。
