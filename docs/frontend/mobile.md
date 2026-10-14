# 手機

結論：640px 以下以拇指區為準：對話框是底部 sheet、側欄是覆蓋式抽屜、composer 貼底；sheet 與抽屜可以滑動關閉，貼底的內容永遠在螢幕鍵盤之上，頁面不跳動也不橫向捲動。

## 尺寸與版面

- 桌面控制項 34px；640px 以下與觸控指標至少 44px（[WCAG 2.2 target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum)），手機輸入字級至少 16px（避免 iOS 自動放大）。
- 抽屜背景 inert、Tab 留在抽屜，Escape 或點遮罩關閉並還原焦點；遮罩隨抽屜邊緣移動並淡入。
- 640px 以下置中對話框改為底部 sheet（快捷指令例外，`--dialog-sheet: 0`）：從底緣整片升起、不回彈（回彈會讓底邊離開畫面）。
- 頁面在 375px 寬不得出現橫向捲動；長內容在自己的區域內捲動。
- 欄位多的表格在 `narrow` 以下加 `DataTable` 的 `stacked`，每列改為卡片（opt-in，其他表格不變）：儲存格直排，`data-stack="end"` 放右上（列選單或編輯），`data-stack="hide"` 留給詳情，`data-label` 標示需要欄名的值；可點的卡片（`nxTableRow`）整張是觸控目標，名稱連結不另撐 44px；表格最小寬度寫在 `--table-min-width`。範例見 `/design`「資料工作區」。
- 頁首把畫面讓給內容：640px 以下 `nx-feature-page` 只留一列（側欄按鈕、返回、單行標題、頁面動作），說明隱藏，帶圖示的動作按鈕只顯示圖示（文字仍是可存取名稱，所以動作按鈕一律放圖示）；桌面版標題與說明同一列。只有「篩選」字樣的篩選標題在手機上同樣省略。

## 斷點

- 視窗寬度只分五級，值是該級的最大寬度：`compact` 520px（卡片單欄）、`phone` 640px（sheet、觸控尺寸）、`narrow` 859px（側欄改為抽屜）、`tablet` 1100px（並排窗格收合、詳細抽屜改為 modal）、`wide` 1200px（密集格線少一欄）。定義在 `frontend/src/styles/breakpoints.scss`，TS 對應 `frontend/src/app/shared/browser/breakpoints.ts`。
- SCSS：`@use 'breakpoints' as bp;`，寫 `@include bp.below(phone) { … }`；互補條件用 `bp.above(narrow)`（range 語法，兩者不重疊也不留縫）。高度條件在 mixin 內巢狀 `@media (max-height: …)`，「或」用 `$or: '(pointer: coarse)'`。不寫 `@media (max-width: …)` 字面值。
- TS：`mediaQuery(below('phone'))` 回傳 `Signal<boolean>`，在注入情境呼叫、隨擁有者解除監聽；跨越時要做的事寫成 effect，不自己接 `matchMedia`。
- 元件自身寬度決定的版面（附件列表、輸入區工具列）用 container query，不用視窗斷點：側欄展開時 860px 視窗的內容只剩約 550px，視窗寬度量不準。完整名稱留在提示與可存取名稱。

## 滑動關閉

- 共用 `shared/browser/swipe.ts`：`SwipeTracker` 算位移與放開時的速度，`bindSwipe` 只在把手上接手。toast 向右滑、sheet 向下、抽屜向左都用它。
- 只從把手開始：sheet 是標題區（`.dialog-bar`、`header`，其他標題加 `data-sheet-handle`）與外框留白；抽屜是標題列與遮罩。把手設 `touch-action: none`，內容的捲動與文字選取仍交給瀏覽器；輸入欄位不會開始拖曳。
- 先移動 6px 且方向以拖曳軸為主才接手，所以把手上的按鈕仍可點；拖曳後的那次 click 會被吞掉。
- 放開時超過距離（sheet：高度 1/3，最多 120px；抽屜：寬度 1/3）或快速甩動（約 0.45 px/ms）就關閉，否則以 `--ease-spring` 彈回；反方向拖曳有阻力。遮罩隨拖曳變淡。
- sheet 關閉走 Escape 的路：送出可取消的 `cancel`，擁有者擋下（忙碌、未儲存）就彈回，所以不必逐一改對話框。sheet 綁在 document 上（`shared/ui/sheet-swipe.ts`），由 `--dialog-sheet: 1` 判斷是否為 sheet 版面。
- 彈回可能越過原位，sheet 下方與抽屜左側各有一段同色陰影補上空隙。
- 減少動態：拖曳時仍跟著手指（直接操作），放開後的彈回與退場由全域 mixin 移除 transition，直接到位。

## 螢幕鍵盤

- `index.html` 的 viewport 加 `interactive-widget=resizes-content`：Chromium（Android）在鍵盤出現時縮小 layout viewport，`100dvh` 的工作區跟著變矮，composer 自然在鍵盤上方。
- Safari 不支援，只縮小 visual viewport。`core/layout/keyboard-inset.ts` 以 `innerHeight − visualViewport.height` 算出被蓋住的高度寫入 `--keyboard-inset`（小於 80px 視為捲軸或工具列，不算；縮放時不算），並把 Safari 為了露出欄位而捲動的文件捲回 0。
- 使用 `--keyboard-inset`：`.workbench` 高度扣掉它；sheet 以 `margin-block-end` 坐在鍵盤上、最大高度同步扣除；composer 與 sheet 的 home indicator 留白在鍵盤出現時歸零。
- 新增貼底或滿高的版面用 `dvh` 並扣 `--keyboard-inset`，不要用 `100vh` 或監聽 `resize` 自行調整。
