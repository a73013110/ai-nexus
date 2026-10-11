# 設計系統

結論：介面以閱讀長對話為主，中性色表面、細線分隔與文字層級；顏色只用於選取、焦點與真實狀態，動效只表達系統真的在工作。所有主題、字級、尺寸與動效集中在 [`frontend/src/styles/tokens.scss`](../../frontend/src/styles/tokens.scss)，元件不寫 hex、不重複字級。實際值與元件清單以程式碼和 `/design` 頁為準。

## 三層 token

| 層 | 例 | 規則 |
| --- | --- | --- |
| Primitive | `--p-stone-50`、`--p-space-4` | 原始色階、rem 字級、4px 間距；元件不直接使用 |
| Semantic | `--canvas`、`--ink`、`--signal`、`--text-body` | 指定用途；light／dark 重新映射，元件不寫主題條件 |
| Component | `--reading-width`、`--dialog-width`、`--button-target` | 單一元件或版面的尺寸與密度 |

- 新增顏色先建立用途（semantic），才選 primitive。換品牌色只改 `--accent`、`--accent-soft`、`--focus`、`--signal` 的兩種主題映射。
- 全站文字變大調 font primitives；單一區域密度調 component tokens；元件要讓父層調整時開 custom property，不讓父層覆寫內部。
- 禁止彩色側邊線、漸層背景、發光陰影、透視網格與懸浮卡片動效。低層次表面共用 `--surface-subtle`。

## 主題

- 主題 zone 用 `[data-theme='light'|'dark']` 重新映射同一組 semantic tokens；`/design` 的局部預覽也靠它。
- 危險按鈕用 `--danger-surface`／`--danger-text`，與錯誤文字 `--error` 分開，避免深色主題的提示色變成低對比按鈕。
- 個人設定只改 semantic／component tokens，不能縮小 UI 最小字級與觸控目標。
- 對比目標是一般文字 WCAG AA 4.5:1，focus 有 2px 可見輪廓。token 有值不代表組合都合格，新的文字／背景組合要實測。

## 字級與密度

- 根字級沿用瀏覽器預設，不固定 html px，讓使用者縮放生效。UI 與程式碼使用系統字型，不發外部字型請求。
- 介面 tokens 與閱讀 tokens 分開：個人閱讀偏好只調 `--text-body`／`--line-reading`（對話、Markdown、文件正文、評測答案），不影響操作尺度。
- 閱讀預設值同時存在前端 tokens、[`UserPreferences`](../../backend/src/AiNexus.Features/Identity/Users/UserPreferences.cs) 常數與資料庫 default constraint；改預設要一起改，migration 只改預設、不動已保存的偏好。
- 緊湊 scope（`.ui-density-compact`、`FeaturePage` 預設）必須同時設定 tokens 與實際字級／行高，否則未指定字級的標籤會繼承 body 的 16px。共用緊湊控制項用 `--density-text-size`／`--density-caption-size` 調字級，不複製樣式。
- 操作面積：桌面 34px，觸控與窄螢幕 44px，見 [共用介面模式](ui-patterns.md#手機)。

## 動效

- 時長與 easing 用 `--motion-*`／`--ease`，keyframes 集中在 `frontend/src/styles/motion.scss`（元件內宣告的 keyframes 會被改名，e2e 也以名稱檢查）。
- 生成訊號、等待指示與串流游標只依伺服器 run 狀態與是否收到內容顯示（`core/api/generation-status.ts`）；思考強度不是推理階段，不宣稱「正在推理」。工作結束即停止。
- 禁止假百分比與干擾閱讀的整頁持續動畫；只有真實的進行中工作（例：總覽的 activeJobs／activeGenerations）觸發訊號動畫。
- 減少動態有兩個來源：系統 `prefers-reduced-motion` 與個人設定（`:root[data-reduced-motion='true']`），共用 `motion.scss` 的 mixin。元件內的 loop 動畫另以 `@media (prefers-reduced-motion)` 加 `:host-context([data-reduced-motion='true'])` 停止。停用動效時顏色與文字狀態仍保留。
- 登入頁的傅立葉標誌動畫可跳過／重播、背景分頁暫停、數秒後完全停止，結束後交給靜態 SVG（`BrandWordmark`）；表單全程可用。

## 圖示

- `Icon` 以語意名稱對應精選的 Lucide SVG 資料，由單一 renderer 繪製，不匯入整套圖示或每個圖示一個 component；新增圖示只改 `shared/ui/icon.ts` 的對照。
- 不同操作不共用同一個符號（例：工作區與快捷指令各有圖示）。
- 品牌、傅立葉畫布與資料圖表保留自己的圖形，不走 `Icon`。

## 捲軸與彈窗外框

- 捲軸樣式集中在 `frontend/src/styles/scrollbars.scss`，不用 JavaScript 捲動或覆寫滾輪；forced-colors 保留原生捲軸。
- 主要內容與 dialog body 使用 stable `scrollbar-gutter`，資料變多時寬度不跳動。
- Playwright 不使用 headless 預設的 `--hide-scrollbars`，失敗截圖與溢出檢查涵蓋實際捲軸。

## 樣式載入

- 元件樣式放在元件旁的 `.scss`，用預設 Emulated 封裝隨元件（多半隨 lazy 路由）載入；刪掉元件就刪掉樣式。不用 `ViewEncapsulation.None`，也不用 `::ng-deep`。
- `frontend/src/styles.scss` 只決定載入順序；`frontend/src/styles` 只放套在多個元件或封裝碰不到的內容上的規則：
  - Markdown 由 `innerHTML` 產生，元件屬性加不上去（`markdown.scss`）。
  - 投影進共用元件的內容（`ng-content`）帶宣告端的屬性：Notice、EmptyState 的規則放 `projected.scss`，表格內容放 `data-workspace.scss`。
- driver.js 的覆寫是獨立 bundle（`angular.json` 的 `driver-tour`，`inject: false`），導覽啟動時才載入。
- 跨元件調整：祖先或 host 狀態用 `:host(.x)`／`:host-context(.x)`；父層要改子元件內部，由子元件開 custom property 或 input（例：`--inline-title-max-width`、`Select` 的 `appearance`）；父層可以直接排版子元件的 host 元素。
- 每個元件樣式檔要在 `anyComponentStyle` 預算（4 kB）內；超過時拆子元件或分檔，不提高預算，也不搬到全域樣式掩蓋。
- CSP 禁止 inline script，所以 build 關閉 `inlineCritical`（它會產生 inline onload）。

## 元件工作臺 /design

- 管理員從平台管理右上「管理工具」→「介面元件」進入；只有具 admin 功能者可見。
- 直接組合正式元件與本機範例，提供淺色／深色局部預覽與目前 semantic tokens 的 JSON 匯出；不改個人偏好、不呼叫模型、不建立資料。
- 新共用元件要在這裡放範例（含停用、錯誤、選填等狀態）。

## 改 token 或元件後的檢查

- 跑 `scripts/Verify.ps1 -Browser`（瀏覽器測試在 `frontend/e2e`，例：`design.spec.ts`、`chat-layout.spec.ts`），並在瀏覽器人工檢視畫面。
- 人工至少檢查：light／dark 文字與按鈕對比、375px 手機與 1280×768 桌面、長回答／長草稿、浮層與捲動、Tab／方向鍵／Esc、確認視窗焦點返回、停用狀態、減少動態。
- build 成功不等於視覺驗收；自動測試只涵蓋已選定的文字／背景組合、控制面積與橫向溢出。
