# 共用介面模式

結論：新頁面組合 `shared/ui` 與 `core/layout` 的現有元件，不自建側欄、提示、表格、抽屜或切換樣式；同一種 UI 第二次出現就抽成共用元件（[共用元件](shared-components.md)）。系統日誌是緊湊資料介面的基準頁；聊天與文件正文保留個人閱讀設定。元件用法見原始碼與 [`/admin/design`](design-system.md#元件工作臺-design)。

## 頁面版面

- 功能頁用 `FeaturePage`（預設 compact、wide）。頁首只放標題、一句說明與動作，不加眉標或其他功能的入口。
- 全頁只有 `.feature-main` 一個捲動區；`.feature-content` 統一內容上限與對齊線。
- 固定欄位用表格（`layoutMode="data"`、列狀態、查詢與分頁見 [資料工作區](data-workspace.md)），面板裡的小表格也用 `DataTable`；差異大的文件、報告、進度用卡片或專屬閱讀介面。
- 卡片用 `nxCard`（保留原生語意），沒有資料用 `EmptyState`；頁首摘要數字用 `StatGrid`＋`StatTile`。資源的列表與詳細頁見 [資源集合頁](resource-pages.md)。

## 導覽

- 登入後的路由掛在 `WorkspaceShell` 下（側欄只建立一次，換頁保留狀態）；頁面只提供主區，側欄內容用 `<ng-template nxSidebarPanel>` 宣告，標題列放 `WorkspaceMenuButton`。
- 列表與詳細路徑用 `optionalId` 合成一條路由；麵包屑依網址查功能目錄。側欄一格就是一頁，頁內不放同模組其他頁的連結列。
- `WorkspaceLayout` 分開管理桌面收合（`desktopCompact`，跨路由維持）與 `narrow` 以下的抽屜（`mobileOpen`，見 [手機](mobile.md#斷點)）。
- 功能清單只由 `WorkspaceNavigation` 呈現（釘選＋全部功能，行為見 [個人設定](../features/settings.md#側欄)）。`Ctrl + K` 是 shell 的 `CommandPalette`，頁面用 `WorkspaceCommands.provide()` 加入動作。
- 長清單共用一個 `MenuPanel`（不每列建 `ActionMenu`），列加 `content-visibility: auto`。
- 目前功能的每個入口都標 `aria-current="page"`（詳細路由仍屬該功能）；導覽溢位用 `ScrollArea` 的箭頭提示。
- `ViewportInset` 讓工作區與抽屜避開全域提示和測試身分列。
- 文件閱讀連結用 `ReaderLink`：來源放 URL（可重新整理、開新分頁；只收本站路徑），閱讀位置放 history state，返回聊天時保留草稿、捲動與焦點。登入後的返回位置只收白名單路徑。
- 操作導覽用 `ProductTourDefinition`（依可見功能過濾、不代替送出，重播提示自動加），入口是 `FeaturePage`、`DialogHeader`、`DetailDrawer` 的 `tour`；條件步驟寫 `x && {…}`。dialog 內也可用，Escape 只關導覽。

## 表單

欄位、段落排版、選人與日期見 [表單](forms.md)；共用元件何時抽、放哪裡見 [共用元件](shared-components.md)。

## 對話框與抽屜

- 用原生 `<dialog class="platform-dialog" nxCompactDialog size="…">`：`sm` 確認與單欄、`md` 表單（預設）、`lg` 清單與檢視器、`full`。不接管 focus、Escape 或業務狀態。
- 結構：`<nx-dialog-header heading detail>`（關閉鈕也經 `cancel`，忙碌與未儲存的攔截照常生效）＋ `.dialog-body`（唯一捲動區，欄位免寫 CSS；清單、檢視器加 `.dialog-body-flush`）＋ 選用的 `.dialog-foot`；表單可用 `form.dialog-form` 包住三者。開啟單筆紀錄（表單、單據）時標題列加 `icon` 與 `dialog-subtitle`（種類、單號、時間各一個子元素，分隔點由樣式畫）；單據日期一律帶年。
- 標題列只放標題與視窗層級動作（下載、新分頁、放大、關閉；選取器可加搜尋欄）。檢視器的模式、搜尋、頁碼、縮放放 `FloatingToolbar`：浮在內容上、閒置淡出，不佔版面。
- 檢視器外層加 `nxIdleChrome`：工具列與放大後（`maximizable`）浮在內容上的標題列共用一套出現與淡出規則（`shared/ui/idle-chrome.ts`），鍵盤焦點或展開的選單在內不淡出。
- 分隔線與內距隨尺寸（`lg`、`full` 才切三段），元件不重寫外框、padding 或寬度。
- `.dialog-note` 放頁尾左側的說明；取消在左、主要動作在右。編輯已儲存的設定，頁尾用 `DialogSaveBar`（說明｜還原｜儲存，未變更時兩鈕停用並寫明；測試身分不能寫入時 `note` 用 `WorkspaceSession.testingNote`），關閉前確認未儲存的變更；新增時為「取消｜建立」。標題自我說明（「編輯角色：成員」），不加眉標。
- `ConfirmDialog` 初始焦點在「取消」，Esc／關閉視為取消，結束後返回先前焦點。危險動作一律經過確認。
- 詳情用 `DetailDrawer`：桌面 modeless（可繼續操作列表），`tablet` 斷點以下改為原生 modal。
- 抽屜內容由數個區塊（提示、卡片、帶標題的段落）組成時包在 `.ui-drawer-sections`：區塊間距只靠它的 gap，區塊不自帶外距；否則有提示時疊兩份外距、沒提示時又貼在一起。
- 多頁籤視窗：`Tabs` 在標題區（標題列與選用的摘要）下、tabpanel 上，不隨內容捲動；tabpanel 是 `.dialog-body`，各頁籤分開儲存時是含 body 與 `DialogSaveBar` 的面板。有未儲存變更的頁籤設 `TabItem.dirty`：標籤後加圓點，名稱加「（有未儲存的變更）」，不必切過去才知道。tabpanel 掛 `nxViewMotion`、不固定高度（[動效](motion.md#對話框)）。
- 只在開啟時建立昂貴內容（`CompactDialog.opened`），關閉時銷毀閱讀器、圖表與 Blob URL。
- 檔案一律用 `FileList`（點卡片進閱讀器、右側下載）：附件、分享與其他系統的檔案共用，呼叫端只負責組 `FileListItem`；其他系統的檔案以 `ReaderTarget.remote` 預覽；唯讀單據加 `stacked` 一檔一列。簽核流程用 `ApprovalTimeline`（`summary` 給進度）。

## 提示、載入與錯誤

- 失敗、風險與要留著讀的資訊用 `Notice`；`message` 附可複製的查證代碼，動作放 `notice-actions`。剛完成的操作用 `ToastService`；composer 與檢視器的狀態收斂在[動態島](motion.md#動態島)。
- 讀取錯誤與重試見 [讀取與寫入](frontend-boundaries.md#讀取與寫入)；載入、重讀與送出中見 [載入與送出中](loading.md)。
- 進度只顯示真實階段（`JobProgress`），未知總量用不定進度；未知金額或用量寫明未知，不顯示為零。
- 規則與計算方式收進 `InfoPopover`：短說明用 `.form-note-list`；長說明加 `heading`，內容用 `styles/popovers.scss` 的 `.info-*`。
- 狀態用 `StatusBadge`／`CountBadge`：文字或圓點與顏色並用。數量徽章是裝飾，脈絡由父按鈕的名稱提供，只由一個 contextual status 播報。

## 鍵盤與焦點

- 浮層（選單、`InfoPopover`、Select）用原生 top layer（不用會被裁切的 `<details>`），依空間翻轉並限高；開關、重新定位與 manual 浮層的點外關閉用 `shared/browser/anchored-popover.ts`。
- 共用 UI 的 DOM ID 每個實例唯一。關閉浮層、對話框或抽屜後焦點回到觸發元素；Escape 一層一層關閉巢狀浮層。
- 按下後會被移除的按鈕要把焦點交給接手的控制項，不讓它掉到 `<body>`。
- 群組控制（ViewSwitch、Tabs、ActionMenu、表格事件按鈕、長清單列）用方向鍵／Home／End 移動，跳過停用項目。
- 操作不能只靠 hover 出現；focus、停用、錯誤、斷線都要可辨識。

## 手機

觸控尺寸、底部 sheet、抽屜、滑動關閉與螢幕鍵盤見 [手機](mobile.md)。
