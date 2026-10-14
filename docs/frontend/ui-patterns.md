# 共用介面模式

結論：新頁面組合 `shared/ui` 與 `core/layout` 的現有元件，不自建側欄、提示、表格、抽屜或切換樣式。系統日誌是緊湊資料介面的基準頁；聊天與文件正文保留個人閱讀設定。元件用法見原始碼與 [`/design`](design-system.md#元件工作臺-design)。

## 頁面版面

- 功能頁用 `FeaturePage`（預設 compact、wide，只改內容密度；需要時才指定 comfortable 或窄版）。頁首只放標題、一句說明與動作，不加眉標或其他功能的入口。
- 全頁只有 `.feature-main` 一個捲動區；`.feature-content` 統一內容上限與對齊線。
- `layoutMode="data"` 讓資料表填滿剩餘高度；篩選展開很多時外層仍可捲動。
- 表格用於固定欄位（表格、查詢與分頁見 [資料工作區](data-workspace.md)）；差異大的文件、報告、進度用卡片或專屬閱讀介面。
- 卡片用 `nxCard`（保留原生語意），空白與載入內容用 `EmptyState`。資源的列表與詳細頁見 [資源集合頁](resource-pages.md)。

## 導覽

- 登入後的路由掛在 `WorkspaceShell` 下（側欄只建立一次，換頁保留狀態）；頁面只提供主區，側欄內容用 `<ng-template nxSidebarPanel>` 宣告，標題列放 `WorkspaceMenuButton`。
- 同一頁的列表與詳細路徑用 `optionalId('<path>')` 合成一條路由，換 id 時沿用元件。
- `WorkspaceLayout` 分開管理 `desktopCompact`（`narrow` 以上收合為圖示欄，跨路由維持）與 `mobileOpen`（`narrow` 以下的覆蓋式抽屜，見 [手機](mobile.md#斷點)）。
- 功能清單只由 `WorkspaceNavigation` 呈現（釘選＋全部功能，行為見 [個人設定](../features/settings.md#側欄)）。`Ctrl + K` 是 shell 的 `CommandPalette`，頁面用 `WorkspaceCommands.provide()` 加入動作。
- 長清單共用一個 `MenuPanel`，不每列建 `ActionMenu`；列加 `content-visibility: auto`。
- 選取的功能取 UrlTree segment 最深的可見功能（不看 query、fragment）；只有一項 `aria-current="page"`，詳細路由仍屬該功能。
- 功能導覽用 `ScrollArea`：隱藏捲軸，以可點的漸層箭頭提示溢位。
- `ViewportInset` 讓工作區與抽屜避開全域提示和測試身分列。
- 附件、引用、知識庫與專案文件的閱讀連結用 `ReaderLink`：來源放 URL（可重新整理、開新分頁；只收本站路徑），閱讀位置放 history state，返回聊天時保留草稿、捲動與焦點。登入後的返回位置只收白名單路徑。
- 操作導覽提供 `ProductTourDefinition`：可略過、重播，依可見功能過濾步驟，不代替送出；觸發點只在頁首最右（`FeaturePage` 的 `tour`）。

## 表單

- 原生 input／select／textarea 用 `nxField`，搜尋用 `SearchField`（對話框列內 `appearance="bare"`）。互斥篩選或版面用 `ViewSwitch`；切換一塊內容（含對話框分類）用 `Tabs` 並連 tabpanel。共用元件不接管查詢、驗證、草稿或 API。
- 驗證說明靠近欄位，不放頁面提示容器。
- 段落用單層輕量外框（legend 在框線上），框內不疊框。多選清單用 `ChoiceGroups`：每類一張有框卡片（標題、已選數、全選；無分類就一張），不包總 fieldset。`Checkbox` 勾選只靠方框，列不填色。
- 設定表單（例：使用者管理、角色與功能群組）用 `.form-sections`（`frontend/src/styles/platform.scss`）：段落以細線分隔、不加框，標題下最多一句說明，寬時標題在左欄、欄位限寬在右欄。填欄位需要的限制寫在欄位旁，其餘規則收進標題旁的 `InfoPopover`；勾選框對齊欄位左緣，依附欄位用 `.form-subfields`，短欄位加 `.form-field-short`。
- 日期用 `DateTimePicker` 與 `shared/browser/format.ts`：台北 wall-clock 輸入與顯示、送出轉 UTC，不讓瀏覽器把日期正規化成另一天；民國曆只影響顯示。選填（`[required]="false"`）日期無效時暫停自動查詢。
- 有未儲存變更的頁面實作 `canLeave()`，路由加 `pendingChanges` guard；對話框切頁籤或關閉前用 `ConfirmDialog` 確認。
- 文字輸入：IME 組字中（含 keyCode 229）的 Enter 不送出，Shift+Enter 換行；長草稿自動增高，上下限讀 CSS token。

## 對話框與抽屜

- 用原生 `<dialog class="platform-dialog" nxCompactDialog size="…">`：`sm` 確認與單欄、`md` 表單（預設）、`lg` 清單與檢視器、`full`。不接管 focus、Escape 或業務狀態。
- 結構：`<nx-dialog-header heading detail>`（自帶關閉鈕，經 `cancel` 關閉，忙碌與未存檔的攔截照常生效）＋ `.dialog-body`（唯一捲動區，欄位免寫 CSS；清單、檢視器加 `.dialog-body-flush`）＋ 選用的 `.dialog-foot`；表單可用 `form.dialog-form` 包住三者。
- 標題列只放標題與視窗層級動作（下載、新分頁、放大、關閉；選取器可加搜尋欄）。檢視器的模式、搜尋、頁碼、縮放放 `FloatingToolbar`：浮在內容上、閒置淡出，不佔版面。
- 密度隨尺寸：`sm`、`md` 無分隔線；`lg`、`full` 以分隔線切開三段。不在元件內重寫外框、padding 或寬度。
- `.dialog-note` 放頁尾左側的說明；取消在左、主要動作在右。編輯已儲存的設定，頁尾用 `DialogSaveBar`（說明｜還原｜儲存，未變更時兩鈕停用並寫明），關閉前確認未儲存的變更；新增時為「取消｜建立」。標題自我說明（「編輯角色：成員」），不加眉標。
- `ConfirmDialog` 初始焦點在「取消」，Esc／關閉視為取消，結束後返回先前焦點。危險動作一律經過確認。
- 詳情用 `DetailDrawer`：桌面 modeless（可繼續操作列表），`tablet` 斷點以下改為原生 modal。
- 多頁籤視窗：`Tabs` 在標題區（標題列與選用的摘要）下、tabpanel 上，不隨內容捲動；tabpanel 是 `.dialog-body`，各頁籤分開儲存時是含 body 與 `DialogSaveBar` 的面板。一次儲存全部頁籤時高度固定不跳動；各頁籤分開儲存或要換寬度（例：使用者「對話」）時用 `DialogMotion` 過渡尺寸。內容用 `ViewMotion`。
- 只在開啟時建立昂貴內容（`CompactDialog.opened`），關閉時銷毀閱讀器、圖表與 Blob URL。

## 提示、載入與錯誤

- 失敗、風險與要留著讀的資訊用 `Notice`；`message` 附可複製的查證代碼，動作放 `notice-actions`。剛完成的操作用 `ToastService`；composer 的提示收斂在動態島（見 [動效](motion.md)）。
- 讀取的載入／錯誤／重試見 [讀取與寫入](frontend-boundaries.md#讀取與寫入)。形狀已知的內容用 `.skeleton` 佔位，容器保留 `role="status"` 與可讀文字。
- 進度只顯示真實階段（`JobProgress`）；未知總量用不定進度，不做假百分比。未知金額或用量顯示說明文字，不顯示為零。
- 狀態用 `StatusBadge`／`CountBadge`：文字或圓點與顏色並用。數量徽章是裝飾，脈絡由父按鈕的名稱提供，只由一個 contextual status 播報。

## 鍵盤與焦點

- 浮層（選單、`InfoPopover`、Select）用原生 top layer（不用會被裁切的 `<details>`），依空間翻轉並限高，圖示欄的浮層往旁邊開；開關、重新定位與 manual 浮層的點外關閉用 `shared/browser/anchored-popover.ts`。
- 共用 UI 的 DOM ID 每個實例唯一。關閉浮層、對話框或抽屜後焦點回到觸發元素；Escape 一層一層關閉巢狀浮層。
- 按下後會被移除的按鈕（例：HTML 指令碼提示的「檢視原始碼」）要把焦點交給接手的控制項，不讓它掉到 `<body>`。
- 群組控制（ViewSwitch、Tabs、ActionMenu、表格事件按鈕、長清單列）用方向鍵／Home／End 移動，跳過停用項目。
- 操作不能只靠 hover 出現；focus、停用、錯誤、斷線都要可辨識。

## 手機

觸控尺寸、底部 sheet、抽屜、滑動關閉與螢幕鍵盤見 [手機](mobile.md)。

