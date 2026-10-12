# 共用介面模式

結論：新頁面組合 `shared/ui` 與 `core/layout` 的現有元件，不自建側欄、提示、表格、抽屜或切換樣式。系統日誌是緊湊資料介面的基準頁；聊天與文件正文保留個人閱讀設定。元件的輸入與用法看原始碼與管理員的 `/design` 頁（見 [設計系統](design-system.md#元件工作臺-design)）。

## 頁面版面

- 功能頁用 `FeaturePage`：預設 compact 與 wide，只改內容密度；側欄與閱讀正文有各自尺度。需要時才明確指定 comfortable 或窄版。頁首只放標題、一句說明與動作，不加眉標或其他功能的入口。
- 全頁只有 `.feature-main` 一個捲動區；`.feature-content` 統一內容上限與對齊線。
- `layoutMode="data"` 讓資料表填滿剩餘高度；篩選展開很多時外層仍可捲動。
- 表格比較固定欄位；內容差異大的文件、報告、進度用卡片或專屬閱讀介面。
- 卡片用 `nxCard`（保留原生語意），空白與載入內容用 `EmptyState`。資源的列表與詳細頁見 [資源集合頁](resource-pages.md)。

## 導覽

- 登入後的路由都掛在 `WorkspaceShell` 下：側欄只建立一次，換頁保留狀態；頁面只提供主區，側欄內容（例：對話歷史）用 `<ng-template nxSidebarPanel>` 宣告，標題列放 `WorkspaceMenuButton`。
- 同一頁的列表與詳細路徑用 `optionalId('<path>')` 合成一條路由，id 改變時沿用元件。
- `WorkspaceLayout` 分開管理 `desktopCompact` 與 `mobileOpen`：寬於 `narrow` 斷點（[手機](mobile.md#斷點)）時側欄可收合為圖示欄（寬度由側欄過渡，grid 欄跟隨），`narrow` 以下是覆蓋式抽屜。收合狀態跨路由維持。
- 功能清單只由 `WorkspaceNavigation` 呈現（釘選＋全部功能，行為見 [個人設定](../features/settings.md#側欄)）。`Ctrl + K` 是 shell 的 `CommandPalette`（含全部功能），頁面用 `WorkspaceCommands.provide()` 加入動作。
- 長清單共用一個 `MenuPanel`，不每列建 `ActionMenu`；列加 `content-visibility: auto`。
- 選取的功能依 Router UrlTree 的 segment 取最深的可見功能（不看 query、fragment）；只有一項 `aria-current="page"`，詳細路由仍屬於該功能。
- 功能導覽用 `ScrollArea`：隱藏捲軸，只在溢位方向顯示可點的漸層箭頭，高對比模式恢復原生捲軸。
- `ViewportInset` 量測全域提示與測試身分列的實際高度，工作區與抽屜共用，移除提示時清除佔位。
- 附件、引用、知識庫與專案文件的閱讀連結用 `ReaderLink`：來源放 URL（重新整理、新分頁仍可返回，只接受本站工作區路徑），閱讀位置放 history state；返回聊天時保留草稿、捲動與焦點。登入後的返回位置只接受白名單路徑。
- 操作導覽提供 `ProductTourDefinition`：可略過、可重播、依可見功能過濾步驟、不代替送出；觸發點只在頁首最右（`FeaturePage` 的 `tour`）。

## 表單

- 原生 input／select／textarea 用 `nxField`，搜尋用 `SearchField`（對話框列內用 `appearance="bare"`），互斥檢視用 `ViewSwitch`（group＋aria-pressed；`line` 頁面檢視、`segment` 緊湊篩選，`fill` 平分寬度）；連結 tabpanel 的內容才用 `Tabs`。共用元件不接管查詢、驗證、草稿或 API。
- 欄位的驗證說明靠近欄位，不放頁面提示容器。
- 日期一律用 `DateTimePicker` 與 `shared/browser/format.ts`（`formatDate`、`parseDateTimeInput`）：以台北 wall-clock 輸入與顯示、送出時轉 UTC，瀏覽器不得自行把日期正規化成另一天。民國曆只影響顯示與輸入。
- `DateTimePicker` 預設必填；選填要指定 `[required]="false"`，清空時發出空值。選填日期無效時暫停自動查詢，清空則移除條件。
- 有未儲存變更的頁面實作 `canLeave()`，路由加 `pendingChanges` guard。
- 文字輸入：IME 組字中（含 keyCode 229）的 Enter 不送出，Shift+Enter 換行；長草稿自動增高，上下限讀 CSS token。

## 對話框與抽屜

- 用原生 `<dialog>`，加 `nxCompactDialog` 套用共用密度；沒有可存取名稱時由 h2 建立，不接管 focus、Escape 或業務狀態。
- 外框只負責圓角、top layer、焦點與關閉；表單放 `.dialog-scroll`。選取器、清單用 `.dialog-flush`：`.dialog-bar`（標題或搜尋＋關閉，不加眉標）、`.dialog-body`、必要時的 `.dialog-foot`。
- `ConfirmDialog` 初始焦點在「取消」，Esc／關閉視為取消，結束後返回先前焦點。危險動作一律經過確認。
- 詳情用 `DetailDrawer`：桌面 modeless（可繼續操作列表、選下一列），`tablet` 斷點以下改為原生 modal。
- 多頁籤視窗各頁籤維持相同寬高，切換不改外框；確實需要不同尺寸的 dialog 才用 `DialogMotion` 過渡寬高。內容切換用 `ViewMotion`。
- 只在開啟時建立昂貴內容（`CompactDialog.opened`），關閉時銷毀閱讀器、圖表與 Blob URL。

## 資料工作區

- 表格用 `DataTable` 系列：獨立捲動、固定表頭、欄位顯示偏好（`preferenceKey`）、整列點選但保留巢狀控制項與文字選取。排序、分頁的資料與查詢由業務頁負責。
- 長文字與非必要資訊放到選取後的詳情，保持列高一致；原文可由 title 與鍵盤可開啟的詳情取得。
- 查詢快照與尚未送出的篩選欄位分開：換頁、匯出、排序、每頁筆數都沿用已送出的快照，並重設游標。
- 游標分頁不推測未知的總筆數；翻頁重設列表位置並關閉舊詳情。
- 詳情與關聯資料分別載入、分別顯示失敗；關閉、改查詢或切換時取消舊請求並阻擋遲到回應。
- 完整資料以文字呈現，不當 HTML。權限不足時不顯示詳情／匯出操作。
- 沒有逐筆詳情的資料不加空抽屜。稽核與日誌的分頁、匯出見 [活動稽核](../features/activity-audit.md) 與 [系統日誌](../features/system-logs.md)。
- 參考 [Carbon Data Table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines)；日期選擇參考 [WAI APG](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/)，但改用非 modal popover 保留 Tab 順序。

## 提示、載入與錯誤

- 失敗、風險與要留著讀的資訊用 `Notice`（錯誤為 `alert`，其他為 `status`）；`message` 附可複製的查證代碼，動作放 `notice-actions`。剛完成的操作用 `ToastService`；composer 的提示收斂在動態島（見 [動效](motion.md)）。
- 讀取的載入／錯誤／重試寫法見 [讀取與寫入](frontend-boundaries.md#讀取與寫入)。形狀已知的內容用 `.skeleton`（`frontend/src/styles/skeleton.scss`）排出佔位，容器保留 `role="status"` 與可讀的文字。
- 進度只顯示真實階段（`JobProgress`）；未知總量用不定進度，不做假百分比。未知金額或未取得的用量顯示說明文字，不顯示為零。
- 狀態用 `StatusBadge`／`CountBadge`：文字或圓點與顏色並用，不只靠顏色。數量徽章是裝飾，完整脈絡由父按鈕的名稱提供，只由一個 contextual status 播報更新。

## 鍵盤與焦點

- 浮層（選單、`InfoPopover`、Select）用原生 top layer（不用會被 overflow 裁切的 `<details>`）；依空間翻轉並限制高度，圖示欄的浮層往旁邊開。開關、`opened`、捲動與縮放時重新定位、manual 浮層的點外關閉都用 `shared/browser/anchored-popover.ts`；元件只寫定位、焦點與鍵盤。
- 共用 UI 的 DOM ID 每個實例唯一。關閉浮層、對話框或抽屜後焦點回到觸發元素；Escape 一層一層關閉巢狀浮層。
- 群組控制（ViewSwitch、Tabs、ActionMenu、表格事件按鈕、長清單列）用方向鍵／Home／End 移動，跳過停用項目。
- 操作不能只靠 hover 出現；focus、停用、錯誤、斷線都要可辨識。

## 手機

觸控尺寸、底部 sheet、抽屜、滑動關閉與螢幕鍵盤見 [手機](mobile.md)。

