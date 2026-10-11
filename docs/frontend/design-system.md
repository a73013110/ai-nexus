# 設計系統

結論：介面以閱讀長對話為主，中性色表面、細線分隔與文字層級；顏色只用於選取、焦點與真實狀態。動效交代「東西從哪來、變成什麼」與真的在進行的工作，可以有彈性，但不擋操作與閱讀。主題、字級、尺寸與動效集中在 [`frontend/src/styles/tokens.scss`](../../frontend/src/styles/tokens.scss)，元件不寫 hex、不重複字級；實際值與元件清單以程式碼和 `/design` 頁為準。

## 三層 token

| 層 | 例 | 規則 |
| --- | --- | --- |
| Primitive | `--p-stone-50`、`--p-space-4` | 原始色階、rem 字級、4px 間距；元件不直接使用 |
| Semantic | `--canvas`、`--ink`、`--signal`、`--text-body` | 指定用途；light／dark 重新映射，元件不寫主題條件 |
| Component | `--reading-width`、`--dialog-width`、`--button-target` | 單一元件或版面的尺寸與密度 |

- 新顏色先建立用途（semantic）再選 primitive。換品牌色只改 `--accent`、`--accent-soft`、`--focus`、`--signal` 的兩種主題映射。
- 全站字級調 font primitives；單一區域密度調 component tokens；元件要讓父層調整時開 custom property，不讓父層覆寫內部。
- 不用彩色側邊線、漸層背景、發光陰影與透視網格。低層次表面共用 `--surface-subtle`。
- 「目前所在／已選取」是 `--surface` 加 `--selected-ring` 的凸起 chip，圖示用 `--accent`；hover 只用平的 `--hover`，兩者不能長得一樣。同組換選取時用一個 chip 以 `--ease-spring` 滑動。

## 主題

- `[data-theme='light'|'dark']` 重新映射同一組 semantic tokens；`/design` 的局部預覽與固定深色的元件（靈動島）都靠它。
- 危險按鈕用 `--danger-surface`／`--danger-text`，與錯誤文字 `--error` 分開。
- 個人設定只改 semantic／component tokens，不能縮小最小字級與觸控目標。
- 一般文字對比 WCAG AA 4.5:1，focus 有 2px 可見輪廓；新的文字／背景組合要實測。

## 字級與密度

- 根字級沿用瀏覽器預設，讓使用者縮放生效；UI 與程式碼用系統字型，不發外部字型請求。
- 個人閱讀偏好只調 `--text-body`／`--line-reading`（對話、Markdown、文件正文、評測答案），不影響操作尺度。
- 閱讀預設值同時存在前端 tokens、[`UserPreferences`](../../backend/src/AiNexus.Features/Identity/Users/UserPreferences.cs) 常數與資料庫 default constraint；改預設要一起改，migration 不動已保存的偏好。
- 緊湊 scope（`.ui-density-compact`、`FeaturePage` 預設）要同時設定 tokens 與實際字級／行高；共用控制項用 `--density-text-size`／`--density-caption-size`。
- 操作面積：桌面 34px，觸控與窄螢幕 44px，見 [共用介面模式](ui-patterns.md#手機)。

## 動效

- Tokens：時長 `--motion-fast`／`--motion`／`--motion-panel`／`--motion-morph`；easing `--ease`、`--ease-exit`、`--ease-spring`（`linear()` 彈簧，不支援時退回 cubic-bezier）。keyframes 集中在 `frontend/src/styles/motion.scss`（元件內宣告的會被改名，e2e 也以名稱檢查）。
- 可以用：形狀 morph、共享元素過渡、進場位移／縮放與彈簧回彈。不用：假百分比、整頁持續動畫、要等動畫結束才能操作的內容。
- 漸進增強：使用者以新版 Edge／Chrome 為主，少數 Safari／Firefox。View Transitions、`linear()`、`interpolate-size`（`:root` 在支援時開啟）都只是加分，不支援時直接切換。
- 共享元素在 `core/layout/view-morph.ts`：
  - 換頁時頁面交叉淡化，登入標誌移到側欄品牌；側欄顯示即時狀態，導覽 pill 用自己的 CSS 滑動。
  - 元件沿用的路由（`/chat`↔`/chat/:id`）不跑路由過渡，改用 `ViewMorph.run()` 包住狀態變更：歷史列標題→標題列、第一則提問讓 composer 從置中移到底部。
  - 名稱只在 `:root[data-morphing]` 期間生效，平時不產生 stacking context；一次性配對用 `ViewMorph.arm()`＋目標的 `data-morph`。
- 靈動島（`shared/ui/island.ts`）：composer 的狀態與警告註冊到 `IslandService`，依 priority 排序。一則是 pill、多則分出泡泡（split）、點開展開全部（expanded）。會擋住送出、改變送出內容或涉及隱私的提示設 `urgent`，出現時自動展開，收合後維持 pill 直到它消失再出現。寬高由 ResizeObserver 量測後用彈簧過渡，因此不依賴 `interpolate-size`。
- 生成訊號、等待指示與串流游標只依伺服器 run 狀態與是否收到內容顯示（`core/api/generation-status.ts`）；思考強度不是推理階段，不宣稱「正在推理」，工作結束即停止。
- 減少動態有兩個來源：系統 `prefers-reduced-motion` 與個人設定（`:root[data-reduced-motion='true']`），共用 `motion.scss` 的 mixin：animation 直接到終點、移除 transition（版面屬性同一幀到位）、View Transitions 略過。元件內的 loop 另以 `@media (prefers-reduced-motion)` 加 `:host-context([data-reduced-motion='true'])` 停止；顏色與文字狀態保留。
- 登入頁的傅立葉標誌動畫每個分頁只自動播一次（`sessionStorage`，登出回來直接是靜態標誌），可跳過／重播、背景分頁暫停，結束後交給靜態 SVG（`BrandWordmark`）；介紹文案與表單不等動畫。
- 登入按鈕（`LoginSubmit`）依 idle→loading→success morph：成功時縮成打勾圓鈕，停約 0.4 秒再換頁；減少動態時不停留。

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

- 管理員從平台管理右上「管理工具」→「介面元件」進入。
- 組合正式元件與本機範例，提供淺色／深色局部預覽與 semantic tokens 的 JSON 匯出；不改偏好、不呼叫模型、不建立資料。
- 新共用元件要放範例（含停用、錯誤、選填等狀態）。

## 改 token 或元件後的檢查

- 跑 `scripts/Verify.ps1 -Browser`（例：`design.spec.ts`、`chat-layout.spec.ts`），並在瀏覽器人工檢視。
- 人工至少檢查：light／dark 對比、375px 手機與 1280×768 桌面、長回答／長草稿、浮層與捲動、Tab／方向鍵／Esc、確認視窗焦點返回、停用狀態、減少動態。
- build 成功不等於視覺驗收；自動測試只涵蓋選定的文字／背景組合、控制面積與橫向溢出。
