# 質感：材質、深度與光線

結論：介面追求 Apple 式的精緻——克制、有層次、有物理感，而不是「AI 炫光」。判準只有兩條：拿掉效果後資訊不變；效果不搶內容。token 在 [`frontend/src/styles/tokens.scss`](../../frontend/src/styles/tokens.scss) 的 `resource-surfaces`，兩種主題各自重算。

## 可以用

| 手法 | token | 用在 |
| --- | --- | --- |
| 半透明材質 | `--material`、`--material-thick`＋`backdrop-filter: var(--material-filter)`，邊緣 `--material-line` | 黏頂列（`FeaturePage` 的 `stickyHeader`，內容捲到下方才顯現）、浮動工具列、頁首主視覺（hero）、浮層 |
| 表面受光 | `--surface-sheen`（同色、約 3% 明度落差，由上往下） | 主視覺、重點卡片 |
| 分層陰影 | `--tile-shadow`；抬起與主視覺用 `--tile-shadow-raised` | 卡片 hover、主視覺 |
| 狀態暈染 | 狀態色 4–8% 混入表面（`color-mix`） | 只在真的有 warning／danger 時的區塊底色 |
| 圖表面積淡出 | 走勢色往透明的單色漸層 | 走勢、面積圖 |
| 物理回饋 | hover 上移 1px、按壓縮放 0.98，`--ease-spring` 回彈 | 可點的卡片與按鈕 |
| 狀態光暈 | 同色、低透明度、緩慢呼吸（`live-breathe`；`LiveDot` 的 `halo-breathe` 光環） | 只給「即時」與健康狀態的指示點，暫停或斷線時靜止 |

## 不要用

- 多色相漸層、霓虹色、漸層文字、彩色發光陰影；卡片、按鈕與文字都不發光。
- 傾斜、透視、3D 旋轉、透視網格、點陣背景、彩色側邊線。
- 模型活動仍維持單色（見 [設計系統](design-system.md)）：生成狀態出現頻率最高，最容易顯得廉價。

## 效能與無障礙

- `backdrop-filter` 只放在少數固定層；不放在會重複、捲動或每秒更新的卡片與列表上，也不疊兩層模糊。
- `prefers-reduced-transparency` 時材質 token 自動退回不透明表面；forced-colors 下不靠材質或暈染傳達狀態。
- 半透明表面上的文字仍要 WCAG AA 4.5:1，以最差的背景內容實測。
- 動效規則與減少動態見 [動效](motion.md)。
