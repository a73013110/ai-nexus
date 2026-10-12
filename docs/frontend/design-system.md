# 設計系統

結論：介面以閱讀長對話為主，中性色表面、細線分隔與文字層級；顏色只用於選取、焦點與真實狀態。主題、字級、尺寸與動效集中在 [`frontend/src/styles/tokens.scss`](../../frontend/src/styles/tokens.scss)，元件不寫 hex、不重複字級；實際值與元件清單以程式碼和 `/design` 頁為準。

## 三層 token

| 層 | 例 | 規則 |
| --- | --- | --- |
| Primitive | `--p-stone-50`、`--p-space-4` | 原始色階、rem 字級、4px 間距；元件不直接使用 |
| Semantic | `--canvas`、`--ink`、`--signal`、`--text-body` | 指定用途；light／dark 重新映射，元件不寫主題條件 |
| Component | `--reading-width`、`--dialog-width`、`--button-target` | 單一元件或版面的尺寸與密度 |

- 新顏色先建立用途（semantic）再選 primitive。換品牌色只改 `--accent`、`--accent-soft`、`--focus`、`--signal` 的兩種主題映射。
- 全站字級調 font primitives；單一區域密度調 component tokens；元件要讓父層調整時開 custom property，不讓父層覆寫內部。
- 不用彩色側邊線、漸層背景、發光陰影與透視網格。低層次表面共用 `--surface-subtle`。
- 建議 chip（如對話起點）：透明底、1px `--line`、膠囊圓角，hover 用 `--hover`；不用卡片外框，也不加 chevron（chevron 只給會開啟另一個畫面的列）。
- 選取 chip：「目前所在／已選取」與展開後取代原內容的切換鈕（如「全部功能」）用 `--surface`＋`--selected-ring` 凸起、圖示 `--accent`；hover 只用平的 `--hover`，兩者不能一樣。同組只有一個選取 chip（或底線），換選取以 `--ease-spring` 滑動（`SlidingIndicator`）。

## 主題

- `[data-theme='light'|'dark']` 重新映射同一組 semantic tokens；`/design` 的局部預覽靠它。
- 危險按鈕用 `--danger-surface`／`--danger-text`，與錯誤文字 `--error` 分開。
- 個人設定只改 semantic／component tokens，不能縮小最小字級與觸控目標。
- 一般文字對比 WCAG AA 4.5:1，focus 有 2px 可見輪廓；新的文字／背景組合要實測。

## 字級與密度

- 根字級沿用瀏覽器預設，讓使用者縮放生效；UI 與程式碼用系統字型，不發外部字型請求。
- 個人閱讀偏好只調 `--text-body`／`--line-reading`（對話、Markdown、文件正文、評測答案），不影響操作尺度。
- 閱讀預設值同時存在前端 tokens、[`UserPreferences`](../../backend/src/AiNexus.Features/Identity/Users/UserPreferences.cs) 常數與資料庫 default constraint；改預設要一起改，migration 不動已保存的偏好。
- 緊湊 scope（`.ui-density-compact`、`FeaturePage` 預設）要同時設定 tokens 與實際字級／行高；共用控制項用 `--density-text-size`／`--density-caption-size`。
- 操作面積：桌面 34px，觸控與窄螢幕 44px，見 [共用介面模式](ui-patterns.md#手機)。

## 外框與密度

- 參考 [Vercel Geist](https://vercel.com/geist/introduction) 與 Linear 的緊湊介面：1px 細線＋分層陰影（`--shadow` 貼合、`--shadow-floating` 浮層），控制項圓角 6px（`--p-radius-md`）、浮層與對話框 12px（`--dialog-radius`），遮罩不模糊。
- 主要／危險按鈕 hover 往 `--ink` 混色加深（深色主題變亮），不降透明度；次要按鈕有 `--surface` 底。

## 動效

規則、動態島、通知 toast 與對話框進出場見 [動效](motion.md)。

## 圖示

- `Icon` 以語意名稱對應精選的 Lucide SVG 資料，由單一 renderer 繪製；新增圖示只改 `shared/ui/icon.ts` 的對照。
- 不同操作不共用同一個符號。品牌、傅立葉畫布與資料圖表保留自己的圖形。

## 捲軸與彈窗外框

- 捲軸樣式集中在 `frontend/src/styles/scrollbars.scss`，不用 JavaScript 捲動或覆寫滾輪；forced-colors 保留原生捲軸。
- 主要內容與 dialog body 用 stable `scrollbar-gutter`，資料變多時寬度不跳動。
- Playwright 不用 headless 預設的 `--hide-scrollbars`，截圖與溢出檢查涵蓋實際捲軸。

## 樣式載入

- 元件樣式放在元件旁的 `.scss`，用預設 Emulated 封裝隨元件載入；不用 `ViewEncapsulation.None` 與 `::ng-deep`。
- `frontend/src/styles.scss` 只決定載入順序；`frontend/src/styles` 只放跨多個元件或封裝碰不到的規則：
  - Markdown 由 `innerHTML` 產生（`markdown.scss`）。
  - 投影內容（`ng-content`）帶宣告端的屬性：Notice、EmptyState 放 `projected.scss`，表格內容放 `data-workspace.scss`。
  - View Transition 名稱與 pseudo-element 樣式（`motion.scss`）。
- driver.js 的覆寫是獨立 bundle（`angular.json` 的 `driver-tour`，`inject: false`），導覽啟動時才載入。
- 跨元件調整：祖先狀態用 `:host(.x)`／`:host-context(.x)`；父層要改子元件內部，由子元件開 custom property 或 input（例：`--inline-title-max-width`、`Select` 的 `appearance`）；父層可以排版子元件的 host。
- 元件樣式檔在 `anyComponentStyle` 預算（4 kB）內；超過時拆子元件或分檔，不提高預算、不搬到全域。
- CSP 禁止 inline script，所以 build 關閉 `inlineCritical`。

## 元件工作臺 /design

- 管理員從平台管理右上「管理工具」→「介面元件」進入；正式元件配本機範例，淺色／深色局部預覽、匯出 semantic tokens，不改偏好、不呼叫模型、不建立資料。
- 側欄是元件索引（捲動時標示目前區段）；每個元件一個 `DesignSection`（名稱、用途、來源元件、範例舞台），各組分檔於 `frontend/src/app/features/design`。
- 新共用元件要放範例（含停用、錯誤、選填等狀態）。

## 改 token 或元件後的檢查

- 跑 `scripts/Verify.ps1 -Browser`，再人工檢查：light／dark 對比、375px 與 1280×768、長回答／長草稿、浮層與捲動、Tab／方向鍵／Esc、焦點返回、停用狀態、減少動態。
- 自動測試只涵蓋選定的對比組合、控制面積與橫向溢出，不等於視覺驗收。
