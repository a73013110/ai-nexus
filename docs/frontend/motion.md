# 動效

結論：動效交代「東西從哪來、變成什麼」與真的在進行的工作，用彈簧收尾的 morph（動態島語彙），但不擋操作與閱讀；減少動態時一律直接到位。Token 在 [`tokens.scss`](../../frontend/src/styles/tokens.scss)，keyframes 集中在 [`motion.scss`](../../frontend/src/styles/motion.scss)（元件內宣告的會被改名，e2e 也以名稱檢查）。

## 原則

- Tokens：時長 `--motion-fast`／`--motion`／`--motion-panel`／`--motion-morph`；easing `--ease`、`--ease-exit`、`--ease-spring`（`linear()` 彈簧，不支援時退回 cubic-bezier）。
- 可以用：形狀 morph、共享元素過渡、進場位移／縮放與彈簧回彈、按下時微縮（`scale: 0.97`）。不用：假百分比、整頁持續動畫、要等動畫結束才能操作。
- 漸進增強（使用者以新版 Edge／Chrome 為主）：View Transitions、`linear()`、`interpolate-size`、`@starting-style` 與 `transition-behavior: allow-discrete` 只是加分，不支援時直接切換。

## 共享元素

- 在 `core/layout/view-morph.ts`：換頁時頁面交叉淡化，登入標誌移到側欄品牌；側欄顯示即時狀態，導覽 pill 用自己的 CSS 滑動。
- 元件沿用的路由（`/chat`↔`/chat/:id`）不跑路由過渡，改用 `ViewMorph.run()` 包住狀態變更：歷史列標題→標題列、第一則提問讓 composer 從置中移到底部。
- 名稱只在 `:root[data-morphing]` 期間生效，平時不產生 stacking context；一次性配對用 `ViewMorph.arm()`＋目標的 `data-morph`。

## 動態島

- `shared/ui/island.ts`：composer 的狀態與警告註冊到 `IslandService`，依 priority 排序。一則是 pill、多則分出泡泡（split）、點開展開全部（expanded）。
- 會擋住送出、改變送出內容或涉及隱私的提示設 `urgent`，出現時自動展開，收合後維持 pill 直到它消失再出現。
- 跟隨頁面主題，狀態由 tone 色與邊框表示；寬高由 ResizeObserver 量測後用彈簧過渡，不依賴 `interpolate-size`。

## 通知（toast）

- `ToastService`＋`Toaster`（掛在 `WorkspaceShell`，右下；640px 以下貼齊兩側）。只用於剛完成的操作回饋；失敗、風險與要留著讀的內容用頁內 `Notice`，見 [共用介面模式](ui-patterns.md#提示載入與錯誤)。
- 堆疊參考 [Sonner](https://sonner.emilkowal.ski/)：最新在前、後方露出邊緣（最多 3 張可見、保留 5 則）；滑過或聚焦時以彈簧展開並暫停所有計時，分頁隱藏時也暫停。
- 進場是動態島語彙：表面（`::before`）從角落的膠囊拉成寬 pill，再展開成卡片，文字最後淡入；離場收回膠囊。表面另成一層，陰影跟著形變。
- 同一 `id` 再次顯示會原地更新並重新計時（例：上傳中→已上傳）；進行中的 toast 設 `duration: 0`，失敗或中止時 `dismiss`。
- 關閉：關閉鈕、Esc（焦點移到下一則或回到原處）、向右滑超過 64px。帶動作的 toast 預設 8 秒。
- 圖層是 manual popover；modal 開著時新 toast 會重新進入 top layer，所以顯示在對話框之上（但受 modal inert，無法點）。
- 螢幕閱讀器只聽兩個常駐 live region（polite／danger 用 assertive），不重複朗讀卡片；換帳號時清空。

## 對話框

- 置中的對話框一律掛 `.platform-dialog` 或 `.workspace-dialog`，共用外框與動效，元件只覆寫尺寸（快捷指令、檔案預覽也是）；側邊的 `DetailDrawer` 另有自己的滑入。
- 開啟時由下方 0.5rem、`scale 0.97` 彈入，關閉沿原路以 `--ease-exit` 退場，遮罩同步淡入淡出（`overlay`、`display` 用 `allow-discrete` 保留在 top layer）。
- 關閉中仍在畫面上，但 `pointer-events: none`＋`interactivity: inert`，不接點擊與焦點。
- `DialogMotion` 以 `offsetWidth／offsetHeight` 量測，不受開關時的縮放影響。

## 浮層（popover）

- 選單、下拉、日期、欄位、知識來源、資訊浮層與側欄功能面板都掛 [`popovers.scss`](../../frontend/src/styles/popovers.scss) 的 `.popover-panel`：外框與進出場只寫在這裡，元件樣式只留 padding、尺寸等差異。新增浮層同樣掛這個 class。
- 進場從觸發點長出：位移 0.25rem＋`scale 0.98`，`--motion`＋`--ease-spring`；退場沿原路 `--ease-exit`，`overlay`、`display` 用 `allow-discrete`。
- 方向與原點由定位函式（`shared/browser/popover-position.ts` 的 `setPopoverOrigin`）在量測後寫入 `data-side` 與 `transform-origin`。進場因此用 keyframes（`popover-arrive`）而不是 `@starting-style`：後者的起始值在 `showPopover()` 後第一次量測就固定，翻轉來不及生效；keyframes 裡的 `var()` 會重新解析。
- 退場中已不是 `:popover-open`，Esc 歸屬（`DetailDrawer`）與 light dismiss 不受影響；同時 inert、不接點擊。e2e 關閉後用 `toBeHidden()` 等退場結束。
- 對話大綱的預覽隨指標換位置、關閉即移除元素，不套這組動效。

## 減少動態

- 兩個來源：系統 `prefers-reduced-motion` 與個人設定（`:root[data-reduced-motion='true']`），共用 `motion.scss` 的 mixin：animation 直接到終點、移除 transition（版面屬性同一幀到位）、View Transitions 略過。
- 元件內的 loop 另以 `@media (prefers-reduced-motion)` 加 `:host-context([data-reduced-motion='true'])` 停止；顏色與文字狀態保留。

## 生成與登入

- 生成訊號、等待指示與串流游標只依伺服器 run 狀態與是否收到內容顯示（`core/api/generation-status.ts`）；思考強度不是推理階段，不宣稱「正在推理」，工作結束即停止。
- 登入：傅立葉標誌每個分頁只自動播一次（`sessionStorage`），可跳過／重播、背景分頁暫停，結束交給 `BrandWordmark`，文案與表單不等動畫；`LoginSubmit` 依 idle→loading→success morph，成功停約 0.4 秒再換頁（減少動態不停留）。
