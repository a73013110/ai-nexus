# 並排對齊

結論：方框、圓點、圖示與一行文字並排時，對齊的是**字的中線**，不是行框的中線。共用寫法在 [`optical.scss`](../../frontend/src/styles/optical.scss)，元件以 `@use 'optical'` 引用；即時範例是 `/admin/design`「核取方塊」。

## 為什麼

- `--font-ui` 的 Segoe UI 上緣比下緣高，行框置中時字會比旁邊的方框低約 0.7px（13px 字），中文字也一樣。單看不明顯，但勾選框、單選與選單勾號一整排時就看得出「字往下掉」。
- `text-box: trim-both cap alphabetic` 把行框修到大寫字高與基線，flex／grid 置中後兩者的中線重合；中文字筆畫上下各超出大寫字高約 0.07em，中線仍在同一處。不支援的瀏覽器維持原本的行框置中。

## 規則

- 與指示物並排的**單行標題**（勾選框、單選、選單與 select 選項的名稱）用 `@include optical.label-line`。
- 標題要以刪節號截斷時用 `optical.label-line-truncate`：修剪後的盒子只有大寫字高，`overflow: hidden` 會切掉中文字與下伸部（g、y），它改用 `overflow: clip` 加裁切邊界。不要自己把 `text-box` 與 `overflow: hidden` 寫在一起。
- 有說明行時，指示物只對齊**標題那一行**（grid：指示物與標題同一列、說明在下一列），說明再長也不會把方框拉到兩行中間。修剪後的標題沒有行距，標題與說明之間用 `gap`（`--p-space-1`）。
- 多行內容的區塊（卡片標題＋說明、段落）不修剪；置中對齊的是整塊。

## 勾選與單選

- 勾選一律用 `nx-checkbox`（含表格欄位設定、列內選取的 `hideLabel`），不用原生 `input[type=checkbox]` 的外觀：大小、外框（`--control-line`）、圓角（`--radius-mark`）與對齊才會全站一致。
- 列高預設 `--button-target`；面板內的緊湊清單設 `--selection-control-height: var(--control-height-compact)` 與 `--selection-control-padding`，不改元件內部。
- 單選目前沒有圓點元件：說明較長的選項用選項卡片（例：行事曆的放假時段），短選項用 `ViewSwitch`。之後要做圓點單選，沿用 `nx-checkbox` 的結構（grid、方框對齊標題列、`label-line`）。
