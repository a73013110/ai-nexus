# 共用介面模式

結論：新頁面組合 `shared/ui` 與 `core/layout` 的現有元件，不自建側欄、提示、表格、抽屜或切換樣式。系統日誌是緊湊資料介面的基準頁；聊天與文件正文保留個人閱讀設定。元件的輸入與用法看原始碼與管理員的 `/design` 頁（見 [設計系統](design-system.md#元件工作臺-design)）。

## 頁面版面

- 功能頁用 `FeaturePage`：預設 compact 與 wide，只改內容密度；側欄與閱讀正文有各自尺度。需要時才明確指定 comfortable 或窄版。
- 全頁只有 `.feature-main` 一個捲動區；`.feature-content` 統一內容上限與對齊線。
- `layoutMode="data"` 讓資料表填滿剩餘高度；篩選展開很多時外層仍可捲動。
- 依資料用途選版型：表格比較固定欄位；內容差異大的文件、報告、進度用資源清單、卡片或專屬閱讀介面。
- 卡片用 `nxCard`（保留原生語意），空白與載入內容用 `EmptyState`。

## 導覽

- 登入後的路由都掛在 `WorkspaceShell` 下：側欄只建立一次，換頁保留狀態；頁面只提供主區，側欄內容（例：對話歷史）用 `<ng-template nxSidebarPanel>` 宣告，標題列放 `WorkspaceMenuButton`。
- 同一頁的列表與詳細路徑用 `optionalId('<path>')` 合成一條路由，id 改變時沿用元件。
- `WorkspaceLayout` 分開管理 `desktopCompact` 與 `mobileOpen`：860px 以上側欄可收合為圖示欄（寬度由側欄過渡，grid 欄跟隨），859px 以下是覆蓋式抽屜。收合狀態跨路由維持。
- 功能清單只由 `WorkspaceNavigation` 呈現（釘選＋全部功能，行為見 [個人設定](../features/settings.md#側欄)）。`Ctrl + K` 是 shell 的 `CommandPalette`（含全部功能），頁面用 `WorkspaceCommands.provide()` 加入動作。
- 長清單（例：對話歷史）共用一個 `MenuPanel`，不每列建 `ActionMenu`；列加 `content-visibility: auto` 略過畫面外繪製，DOM 仍留給焦點與頁內尋找。
- 選取的功能依 Router UrlTree 的 segment 取最深的可見功能，不看 query、fragment、matrix parameters；只有一項 `aria-current="page"`，詳細路由仍屬於該功能。
- 功能導覽用 `ScrollArea`：隱藏捲軸，只在溢位方向顯示可點的漸層箭頭，高對比模式恢復原生捲軸。理由見 [MDN scrollbar-width 無障礙說明](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/scrollbar-width#accessibility)。
- `ViewportInset` 量測全域提示與測試身分列的實際高度，工作區與抽屜共用，移除提示時清除佔位。
- 附件、引用、知識庫與專案文件的閱讀連結用 `ReaderLink`：來源放 URL（重新整理、新分頁仍可返回，只接受本站工作區路徑），閱讀位置放 history state；返回聊天時保留草稿、捲動與焦點。登入後的返回位置只接受白名單路徑。
- 新頁面的操作導覽提供 `ProductTourDefinition`，沿用 `ProductTourButton`／`ProductTour`：可略過、可重播，依可見功能過濾步驟，不代替使用者送出。

## 表單

- 原生 input／select／textarea 用 `nxField`，搜尋用 `SearchField`，互斥檢視用 `ViewSwitch`（group＋aria-pressed）；連結 tabpanel 的內容才用 `Tabs`。共用元件不接管查詢、驗證、草稿或 API。
- 欄位的驗證說明靠近欄位，不放頁面提示容器。
- 日期一律用 `DateTimePicker` 與 `shared/browser/format.ts`（`formatDate`、`parseDateTimeInput`）：以台北 wall-clock 輸入與顯示、送出時轉 UTC，瀏覽器不得自行把日期正規化成另一天。民國曆只影響顯示與輸入。
- `DateTimePicker` 預設必填；選填要指定 `[required]="false"`，清空時發出空值。選填日期無效時暫停自動查詢，清空則移除條件。
- 有未儲存變更的頁面實作 `canLeave()`，路由加 `pendingChanges` guard。
- 文字輸入：IME 組字中（含 keyCode 229）的 Enter 不送出，Shift+Enter 換行；長草稿自動增高，上下限讀 CSS token。

## 對話框與抽屜

- 用原生 `<dialog>`，加 `nxCompactDialog` 套用共用密度；沒有可存取名稱時由 h2 建立，不接管 focus、Escape 或業務狀態。
- 外框只負責圓角、top layer、焦點與關閉；內容放 `.dialog-scroll`，捲動與最大高度在內層。
- `ConfirmDialog` 初始焦點在「取消」，Esc／關閉視為取消，結束後返回先前焦點。危險動作一律經過確認。
- 詳情用 `DetailDrawer`：桌面 modeless（可繼續操作列表、選下一列），1023px 以下改為原生 modal。
- 多頁籤視窗（例：使用者活動）各頁籤維持相同寬高，切換不改外框；確實需要不同尺寸的 dialog 才用 `DialogMotion` 過渡寬高。內容切換用 `ViewMotion`。
- 只在開啟時建立昂貴內容（`CompactDialog.opened`），關閉時銷毀閱讀器、圖表與 Blob URL。

## 資料工作區

- 表格用 `DataTable` 系列：獨立捲動、固定表頭、欄位顯示偏好（`preferenceKey`）、整列點選但保留巢狀控制項與文字選取。排序、分頁的資料與查詢由業務頁負責。
- 長文字與非必要資訊放到選取後的詳情，保持列高一致；原文可由 title 與鍵盤可開啟的詳情取得。
- 查詢快照與尚未送出的篩選欄位分開：換頁、匯出、排序、每頁筆數都沿用已送出的快照，並重設游標。
- 游標分頁不推測未知的總筆數；翻頁重設列表位置並關閉舊詳情。
- 詳情與關聯資料分別載入、分別顯示失敗；關閉、改查詢或切換時取消舊請求並阻擋遲到回應。
- 完整資料以文字呈現，不當 HTML。權限不足時不顯示詳情／匯出操作。
- 沒有逐筆詳情的資料（例：用量報表）不加空抽屜。稽核與日誌的分頁、匯出規則見 [活動稽核](../features/activity-audit.md) 與 [系統日誌](../features/system-logs.md)。
- 參考 [Carbon Data Table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines)；日期選擇參考 [WAI APG](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/)，但改用非 modal popover 保留 Tab 順序。

## 提示、載入與錯誤

- 失敗、風險、資訊與完成回饋一律用 `Notice`（錯誤為 `alert`，其他為 `status`）；不另建提示容器；composer 的提示收斂在 `NexusIsland`（[動效](design-system.md#動效)）。`message` 會附上可複製的查證代碼，動作放 `notice-actions`。
- 讀取的載入／錯誤／重試寫法見 [讀取與寫入](frontend-boundaries.md#讀取與寫入)。
- 進度只顯示真實階段（`JobProgress`）；未知總量用不定進度，不做假百分比。未知金額或未取得的用量顯示說明文字，不顯示為零。
- 狀態用 `StatusBadge`／`CountBadge`：文字或圓點與顏色並用，不只靠顏色。數量徽章是裝飾，完整脈絡由父按鈕的名稱提供，只由一個 contextual status 播報更新。

## 鍵盤與焦點

- 浮層（選單、popover、Select）用原生 top layer，避免被裁切；依可用空間翻轉並限制高度。
- 共用 UI 的 DOM ID 每個實例唯一。關閉浮層、對話框或抽屜後焦點回到觸發元素；Escape 一層一層關閉巢狀浮層。
- 群組控制（ViewSwitch、Tabs、ActionMenu、表格事件按鈕、長清單列）用方向鍵／Home／End 移動，跳過停用項目。
- 操作不能只靠 hover 出現；focus、停用、錯誤、斷線都要可辨識。

## 手機

- 桌面控制項 34px；640px 以下與觸控指標至少 44px（[WCAG 2.2 target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum)），手機輸入字級至少 16px（避免 iOS 自動放大）。
- 抽屜背景 inert、Tab 留在抽屜，Escape 或點遮罩關閉並還原焦點。
- 窄容器用 container query 調整（例：附件列表縮圖與單行省略），完整名稱保留在提示與可存取名稱。
- 頁面在 375px 寬不得出現橫向捲動；長內容在自己的區域內捲動。
