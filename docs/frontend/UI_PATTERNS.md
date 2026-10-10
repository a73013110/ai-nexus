# 共用介面模式

## 響應式導覽

`WorkspaceLayout` 分開管理 `desktopCompact` 與 `mobileOpen`。寬度達 860px 時，側邊欄可收合為 52px 圖示欄，由共用 `--sidebar-rail-width` 同時控制側欄與主內容欄位；859px 以下使用覆蓋式抽屜，收合時不佔主內容寬度。手機抽屜保留至少 48px 的遮罩區域，提供返回入口、Esc、背景點擊、焦點限制與關閉後焦點還原。

功能導覽使用共用 `ScrollArea`，保留原生滑鼠滾輪、觸控與鍵盤捲動，隱藏捲軸以保持寬度。ResizeObserver 同時量測 viewport 與內容，scroll 事件以 animation frame 合併；只有實際溢位時顯示可點擊的上下漸層箭頭，在頂部只顯示下方、底部只顯示上方、內容全部可見時隱藏兩者。焦點進入時將控制項移到提示區域內側，減少動態效果時直接捲動，高對比模式恢復原生捲軸並保留有邊框的方向控制。使用情境及替代操作依 [MDN scrollbar-width 無障礙說明](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/scrollbar-width#accessibility)。

Chat、`FeaturePage` 及 `DocumentReader` 使用同一個 `WorkspaceMenuButton`。新增工作區請沿用這些共用入口及佈局，不要各自保存手機側欄狀態。`ViewportInset` 統一量測全域提示及測試身分列，讓工作區與抽屜同步使用實際高度，移除提示時也會清除佔位。

此設計參考 [Material UI 的響應式抽屜](https://mui.com/material-ui/react-drawer/#responsive-drawer)，依 AI Nexus 的對話、歷史與工作區結構採用桌機常駐、手機暫時覆蓋的模式。

## 操作導覽

Chat 的操作導覽可從工具列的問號說明圖示或新對話畫面的「操作導覽」啟動，提供聚光遮罩、步驟卡片、進度、上一步、下一步與跳過。共用按鈕使用 `tour` 語意圖示、操作名稱及 tooltip，載入時顯示等待圖示。導覽以現有操作為目標；依使用者可見功能過濾步驟，不代替使用者提交訊息。關閉時還原焦點，離開路由時清理遮罩，並遵守系統與個人減少動態效果的設定。

新頁面透過 `ProductTourDefinition` 提供目標 selector、標題、說明與位置，再沿用 `ProductTourButton` 與 `ProductTour`。字串會先跳脫 HTML；生命週期與主題由共用服務管理。Driver.js 的 ESM 與本機 CSS 在首次啟動時才載入，避免影響首次進入頁面的速度。

選型參考 [IBM Carbon 的情境式 onboarding](https://www.carbondesignsystem.com/articles/tools-for-designing-onboarding-experiences) 與 [Driver.js 官方設定](https://driverjs.com/docs/configuration)。導覽使用可略過、可重播的 coachmark 模式，保留使用者對操作節奏的控制。

## 提示

操作失敗、風險提醒、資訊與完成回饋一律使用 `Notice`。頁面、對話框、輸入區、背景任務、圖表與全域錯誤共用同一個元件，避免新增 `error-banner` 或自行定義提示容器。

```html
<nx-notice [message]="error()" [dismissible]="true" (dismissed)="error.set('')" />
<nx-notice tone="warning" message="內容接近 Context 上限。" />
<nx-notice tone="info" message="來源尚未載入。">
  <button notice-actions type="button" (click)="reload()">重新載入</button>
</nx-notice>
```

`message` 會自動提供經驗證的問題代碼複製功能；動作放入 `notice-actions`。錯誤使用 `alert`，其他提示使用 `status`，搭配 icon 與文字辨識語意。桌機使用 13px 字級與緊湊操作，觸控裝置維持 44px 操作範圍。欄位的驗證說明仍靠近原欄位，不套用頁面提示的容器。元件工作區可直接預覽所有提示色彩。

## 建置與監控元件

監控頁沿用 `Card`、共用表格、`CountBadge`、`StatusBadge`；工作階段清單與詳情有各自的元件責任。`GraphNode` 負責使用者與服務節點的共用呈現，拓樸元件只管理排列與連線。避免用全域樣式搬移或提高預算來掩蓋元件樣式過大的問題。

Mermaid 延後載入套件內的官方瀏覽器 ESM 發行檔 `mermaid/dist/mermaid.esm.min.mjs`，保留所有圖表能力與原有安全設定。該版本已封裝上游的 CommonJS 內部相依項目，不需 `allowedCommonJsDependencies` 白名單。參考 [Mermaid 官方部署方式](https://mermaid.js.org/intro/#mermaid-api) 與 [Angular 建置指引](https://angular.dev/tools/cli/build#configuring-commonjs-dependencies)。通知中心的原生對話框維持即時開關與焦點處理，通知清單延後載入，避免網路載入速度影響 Esc 關閉。
