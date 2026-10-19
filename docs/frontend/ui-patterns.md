# 共用介面模式

結論：新頁面組合 `shared/ui` 與 `core/layout` 的現有元件，不自建側欄、提示、表格、抽屜或切換樣式。系統日誌是緊湊資料介面的基準頁；聊天與文件正文保留個人閱讀設定。元件用法見原始碼與 [`/admin/design`](design-system.md#元件工作臺-design)。

## 頁面版面

- 功能頁用 `FeaturePage`（預設 compact、wide）。頁首只放標題、一句說明與動作，不加眉標或其他功能的入口。
- 全頁只有 `.feature-main` 一個捲動區；`.feature-content` 統一內容上限與對齊線。
- 固定欄位用表格（`layoutMode="data"`、列狀態、查詢與分頁見 [資料工作區](data-workspace.md)），面板裡的小表格也用 `DataTable`；差異大的文件、報告、進度用卡片或專屬閱讀介面。
- 卡片用 `nxCard`（保留原生語意），空白與載入內容用 `EmptyState`；頁首摘要數字用 `StatGrid`＋`StatTile`。資源的列表與詳細頁見 [資源集合頁](resource-pages.md)。

## 導覽

- 登入後的路由掛在 `WorkspaceShell` 下（側欄只建立一次，換頁保留狀態）；頁面只提供主區，側欄內容用 `<ng-template nxSidebarPanel>` 宣告，標題列放 `WorkspaceMenuButton`。
- 列表與詳細路徑用 `optionalId` 合成一條路由；麵包屑與 `ModuleNavigation` 依網址查功能目錄。
- `WorkspaceLayout` 分開管理桌面收合（`desktopCompact`，跨路由維持）與 `narrow` 以下的抽屜（`mobileOpen`，見 [手機](mobile.md#斷點)）。
- 功能清單只由 `WorkspaceNavigation` 呈現（釘選＋全部功能，行為見 [個人設定](../features/settings.md#側欄)）。`Ctrl + K` 是 shell 的 `CommandPalette`，頁面用 `WorkspaceCommands.provide()` 加入動作。
- 長清單共用一個 `MenuPanel`（不每列建 `ActionMenu`），列加 `content-visibility: auto`。
- 目前功能只標一項 `aria-current="page"`（詳細路由仍屬該功能）；導覽溢位用 `ScrollArea` 的箭頭提示。
- `ViewportInset` 讓工作區與抽屜避開全域提示和測試身分列。
- 文件閱讀連結用 `ReaderLink`：來源放 URL（可重新整理、開新分頁；只收本站路徑），閱讀位置放 history state，返回聊天時保留草稿、捲動與焦點。登入後的返回位置只收白名單路徑。
- 操作導覽用 `ProductTourDefinition`（可略過、重播，依可見功能過濾，不代替送出），入口只在 `FeaturePage` 的 `tour`。「可隨時重播」的提示由 `ProductTour` 加在最後一個顯示的步驟，定義裡不必寫。

## 表單

- 原生 input／select／textarea 用 `nxField`，搜尋用 `SearchField`。互斥篩選或版面用 `ViewSwitch`；切換一塊內容（含對話框分類）用 `Tabs` 並連 tabpanel。共用元件不接管查詢、驗證、草稿或 API。
- 驗證說明靠近欄位，不放頁面提示。
- 段落用單層輕量外框（legend 在框線上），框內不疊框。多選清單用 `ChoiceGroups`（每類一張卡片），不包總 fieldset。`Checkbox` 勾選只靠方框，列不填色。
- 設定表單（例：使用者管理、角色與功能群組）用 `.form-sections`（`frontend/src/styles/platform.scss`）：段落以細線分隔、不加框，標題下最多一句說明。填欄位需要的限制寫在欄位旁，其餘規則收進標題旁的 `InfoPopover`；勾選框對齊欄位左緣，依附欄位用 `.form-subfields`，短欄位加 `.form-field-short`。
- 日期用 `DateTimePicker` 與 `shared/browser/format.ts`：台北 wall-clock 輸入與顯示、送出轉 UTC，不讓瀏覽器把日期正規化成另一天；民國曆只影響顯示。選填（`[required]="false"`）日期無效時暫停自動查詢。
- 有未儲存變更的頁面實作 `canLeave()`，路由加 `pendingChanges` guard；對話框切頁籤或關閉前用 `ConfirmDialog` 確認。
- 文字輸入：IME 組字中（含 keyCode 229）的 Enter 不送出，Shift+Enter 換行；長草稿自動增高。

## 對話框與抽屜

- 用原生 `<dialog class="platform-dialog" nxCompactDialog size="…">`：`sm` 確認與單欄、`md` 表單（預設）、`lg` 清單與檢視器、`full`。不接管 focus、Escape 或業務狀態。
- 結構：`<nx-dialog-header heading detail>`（關閉鈕也經 `cancel`，忙碌與未儲存的攔截照常生效）＋ `.dialog-body`（唯一捲動區，欄位免寫 CSS；清單、檢視器加 `.dialog-body-flush`）＋ 選用的 `.dialog-foot`；表單可用 `form.dialog-form` 包住三者。
- 標題列只放標題與視窗層級動作（下載、新分頁、放大、關閉；選取器可加搜尋欄）。檢視器的模式、搜尋、頁碼、縮放放 `FloatingToolbar`：浮在內容上、閒置淡出，不佔版面。
- 檢視器外層加 `nxIdleChrome`：工具列與放大後（`maximizable`）浮在內容上的標題列共用一套規則（`shared/ui/idle-chrome.ts`）。開啟、換文件時短暫出現；滑鼠只有移近或停在上面才出現，在內容上閱讀時移動不喚回；觸控點一下內容切換；Tab 喚回、鍵盤焦點或展開的選單在內不淡出；Esc 或閒置 2 秒淡出。
- 分隔線與內距隨尺寸（`lg`、`full` 才切三段），元件不重寫外框、padding 或寬度。
- `.dialog-note` 放頁尾左側的說明；取消在左、主要動作在右。編輯已儲存的設定，頁尾用 `DialogSaveBar`（說明｜還原｜儲存，未變更時兩鈕停用並寫明；測試身分不能寫入時 `note` 用 `WorkspaceSession.testingNote`），關閉前確認未儲存的變更；新增時為「取消｜建立」。標題自我說明（「編輯角色：成員」），不加眉標。
- `ConfirmDialog` 初始焦點在「取消」，Esc／關閉視為取消，結束後返回先前焦點。危險動作一律經過確認。
- 詳情用 `DetailDrawer`：桌面 modeless（可繼續操作列表），`tablet` 斷點以下改為原生 modal。
- 多頁籤視窗：`Tabs` 在標題區（標題列與選用的摘要）下、tabpanel 上，不隨內容捲動；tabpanel 是 `.dialog-body`，各頁籤分開儲存時是含 body 與 `DialogSaveBar` 的面板。有未儲存變更的頁籤設 `TabItem.dirty`：標籤後加圓點，名稱加「（有未儲存的變更）」，不必切過去才知道。tabpanel 掛 `nxViewMotion`、不固定高度（[動效](motion.md#對話框)）。
- 只在開啟時建立昂貴內容（`CompactDialog.opened`），關閉時銷毀閱讀器、圖表與 Blob URL。

## 提示、載入與錯誤

- 失敗、風險與要留著讀的資訊用 `Notice`；`message` 附可複製的查證代碼，動作放 `notice-actions`。剛完成的操作用 `ToastService`；composer 與檢視器的狀態收斂在[動態島](motion.md#動態島)。
- 讀取的載入／錯誤／重試見 [讀取與寫入](frontend-boundaries.md#讀取與寫入)。形狀已知的內容用 `.skeleton` 佔位，容器保留 `role="status"` 與可讀文字。
- 進度只顯示真實階段（`JobProgress`），未知總量用不定進度；未知金額或用量寫明未知，不顯示為零。
- 狀態用 `StatusBadge`／`CountBadge`：文字或圓點與顏色並用。數量徽章是裝飾，脈絡由父按鈕的名稱提供，只由一個 contextual status 播報。

## 鍵盤與焦點

- 浮層（選單、`InfoPopover`、Select）用原生 top layer（不用會被裁切的 `<details>`），依空間翻轉並限高；開關、重新定位與 manual 浮層的點外關閉用 `shared/browser/anchored-popover.ts`。
- 共用 UI 的 DOM ID 每個實例唯一。關閉浮層、對話框或抽屜後焦點回到觸發元素；Escape 一層一層關閉巢狀浮層。
- 按下後會被移除的按鈕要把焦點交給接手的控制項，不讓它掉到 `<body>`。
- 群組控制（ViewSwitch、Tabs、ActionMenu、表格事件按鈕、長清單列）用方向鍵／Home／End 移動，跳過停用項目。
- 操作不能只靠 hover 出現；focus、停用、錯誤、斷線都要可辨識。

## 手機

觸控尺寸、底部 sheet、抽屜、滑動關閉與螢幕鍵盤見 [手機](mobile.md)。

