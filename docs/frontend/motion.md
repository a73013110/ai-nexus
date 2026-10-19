# 動效

結論：動效交代「東西從哪來、變成什麼」與真的在進行的工作，用彈簧收尾的 morph（動態島語彙），但不擋操作與閱讀；減少動態時一律直接到位。Token 在 [`tokens.scss`](../../frontend/src/styles/tokens.scss)，keyframes 集中在 [`motion.scss`](../../frontend/src/styles/motion.scss)（元件內宣告的會被改名，e2e 也以名稱檢查）。

## 原則

- 時長用 `--motion*`、曲線用 `--ease*`；`--ease-spring` 是 `linear()` 彈簧，不支援時退回 cubic-bezier。
- 可以用：形狀 morph、共享元素過渡、進場位移／縮放與彈簧回彈、按下時微縮（`scale: 0.97`）。不用：假百分比、整頁持續動畫、要等動畫結束才能操作。
- 漸進增強（使用者以新版 Edge／Chrome 為主）：View Transitions、`linear()`、`interpolate-size`、`@starting-style` 與 `transition-behavior: allow-discrete` 只是加分，不支援時直接切換。

## 共享元素

- 在 `core/layout/view-morph.ts`：換頁時頁面交叉淡化，登入標誌與頭像移到側欄；側欄顯示即時狀態，導覽 pill 用自己的 CSS 滑動。
- 元件沿用的路由（`/work/chat`↔`/work/chat/:id`）不跑路由過渡，改用 `ViewMorph.run()` 包住狀態變更：歷史列標題→標題列、第一則提問讓 composer 從置中移到底部。
- 名稱只在 `:root[data-morphing]` 期間生效，平時不產生 stacking context；一次性配對用 `ViewMorph.arm()`＋目標的 `data-morph`。

## 動態島

- `shared/ui/island.ts`：composer 的狀態與警告註冊到 `IslandService`，依 priority 排序。一則是 pill、多則分出泡泡（split）、點開展開全部（expanded）。
- 會擋住送出、改變送出內容或涉及隱私的提示設 `urgent`，出現時自動展開，收合後維持 pill 直到它消失再出現。
- 跟隨頁面主題，狀態由 tone 色與邊框表示；寬高由 `morphSize`（`shared/ui/morph-size.ts`）量測內容後用彈簧過渡，不依賴 `interpolate-size`。
- `IslandService` 預設是根實例（composer）；元件在 `providers` 自備一份就有獨立的島。文件檢視器以此把處理進度、失敗（含重試）與錯誤浮在內容底部，放大時也不佔版面，也不與底下 composer 的提示混在一起。
- `FloatingToolbar` 用同一套變形：膠囊隨模式切換、展開搜尋或多出搜尋結果列彈簧變寬變高；閒置時縮小淡出，喚回時彈回。

## 通知（toast）

- `ToastService`＋`Toaster`（`WorkspaceShell` 右下）只回饋剛完成的操作；何時改用 `Notice` 見 [共用介面模式](ui-patterns.md#提示載入與錯誤)。
- 堆疊參考 [Sonner](https://sonner.emilkowal.ski/)：最新在前、後方露出邊緣（最多 3 張可見、保留 5 則）；滑過或聚焦時以彈簧展開並暫停所有計時，分頁隱藏時也暫停。
- 進場是動態島語彙：表面（`::before`，另成一層讓陰影跟著形變）從角落的膠囊拉成 pill 再展開成卡片，文字最後淡入；離場收回膠囊。
- 同一 `id` 再次顯示會原地更新並重新計時（例：上傳中→已上傳）；進行中的 toast 設 `duration: 0`，失敗或中止時 `dismiss`。
- 關閉：關閉鈕、Esc（焦點移到下一則或回到原處）、向右滑或甩動。帶動作的 toast 預設 8 秒。
- 圖層是 manual popover；modal 開著時新 toast 會重新進入 top layer，所以顯示在對話框之上（但受 modal inert，無法點）。
- 背景任務結束的 toast 是例外的失敗回饋：失敗仍留在任務頁與通知匣，規則見[本分頁的任務 toast](../features/notifications.md#本分頁的任務-toast)。
- 螢幕閱讀器只聽兩個常駐 live region（polite／danger 用 assertive），不重複朗讀卡片；換帳號時清空。

### 本次工作階段紀錄

- 已定案的 toast 另記在 `ToastService.history`（最近 20 則，只在記憶體，重新整理或換帳號清空）；進行中的定案才記，同一 `id` 只算一則。
- 併入通知中心而不另做清單：入口仍是鈴鐺，不多一個浮動按鈕；「本次工作階段」（`ToastHistory`）與通知匣分 tab，不計入未讀，重複也不必去重。
- 動作只執行一次（toast 或紀錄任一處），所以動作在 toast 離開後仍要有效（導覽或伺服器會再檢查的操作）。

## 對話框

- 置中對話框的動效都在 `.platform-dialog`（[結構](ui-patterns.md#對話框與抽屜)），元件只覆寫尺寸。
- 開啟時由下方小幅位移＋微縮彈入，關閉沿原路以 `--ease-exit` 退場，遮罩同步淡入淡出（`overlay`、`display` 用 `allow-discrete` 留在 top layer）；手機 sheet 見 [手機](mobile.md)。
- 關閉中仍在畫面上，但 `pointer-events: none`＋`interactivity: inert`，不接點擊與焦點。
- 尺寸跟著內容：`nxCompactDialog` 內建 `FrameMotion`，`nxViewMotion` 換檢視（例：頁籤）或 `size` 改變時外框過渡到新寬高，不固定高度；關閉即忘記尺寸，開啟不過渡。非 dialog 浮層掛 `nxFrameMotion`（通知中心）。
- 側邊的 `DetailDrawer` 同一套作法改成從右緣滑入、滑出；`closed` 等退場結束才送出，宿主清空內容時抽屜不會空著離開，退場中重新開啟則不送。
- sheet 與抽屜可滑動關閉，未達距離或速度以 `--ease-spring` 彈回，見 [手機](mobile.md#滑動關閉)。

## 浮層（popover）

- 選單、下拉、日期、資訊浮層、桌面的通知中心等浮層一律掛 [`popovers.scss`](../../frontend/src/styles/popovers.scss) 的 `.popover-panel`：外框與進出場只寫在這裡，元件樣式只留 padding、尺寸等差異。
- 進場從觸發點長出（小幅位移＋微縮，`--ease-spring`），退場沿原路 `--ease-exit`。
- 方向與原點由定位函式（`shared/browser/popover-position.ts` 的 `setPopoverOrigin`）在量測後寫入 `data-side` 與 `transform-origin`。進場因此用 keyframes（`popover-arrive`）而不是 `@starting-style`：後者的起始值在 `showPopover()` 後第一次量測就固定，翻轉來不及生效；keyframes 裡的 `var()` 會重新解析。
- 退場中已不是 `:popover-open`，Esc 歸屬（`DetailDrawer`）與 light dismiss 不受影響；同時 inert、不接點擊。e2e 關閉後用 `toBeHidden()` 等退場結束。
- 對話大綱的預覽隨指標換位置、關閉即移除元素，不套這組動效。

## 減少動態

- 兩個來源：系統 `prefers-reduced-motion` 與個人設定（`:root[data-reduced-motion='true']`），共用 `motion.scss` 的 mixin：animation 直接到終點、移除 transition（版面屬性同一幀到位）、View Transitions 略過。JS 動畫（`view-motion.ts`）看 `ThemeService.reducedMotion()`。
- 元件內的 loop 另以 `@media (prefers-reduced-motion)` 加 `:host-context([data-reduced-motion='true'])` 停止；顏色與文字狀態保留。

## 生成與登入

- 生成訊號、等待指示與串流游標只依伺服器 run 狀態與是否收到內容顯示（`core/api/generation-status.ts`）；思考強度不是推理階段，不宣稱「正在推理」，工作結束即停止。
- 登入：兩道訊號沿 N 的輪廓描繪、交會後填色再落到字標（`signal-mark.ts`），輪廓留下，品牌側不變空。每個分頁只播一次（`sessionStorage`），可跳過／重播；窄或矮的視窗不播。文案與表單不等動畫。`LoginSubmit` 依 idle→loading→success morph，打勾停約 0.4 秒。
- 登入後依序：（多個身分時）身分選擇滑入 → 品牌側展開成歡迎畫面（`sign-in-welcome`），同時預先載入 `/me` 約 0.9 秒 → 工作區從標誌位置擴散，標誌與頭像飛進側欄（`sign-in`），第一頁內容依序進場（`data-entrance`）。編排用 `ViewMorph.next(kind, origin)`；減少動態時不經歡迎畫面。
- 登出反向且較快（`sign-out`）：工作區收進頭像、側欄滑出、標誌飛回登入頁。帳號世代在離開工作區後才更新，收合時仍顯示登出的人，頁面也不會重新讀取。
