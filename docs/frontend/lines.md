# 線條

結論：外框色只看**元件是什麼**，不看它放在哪一頁。要使用者辨認邊界才能操作的控制項一律 `--control-line`，表面與分隔用較淡的髮絲線。角色定義在 [`tokens.scss`](../../frontend/src/styles/tokens.scss)，元件只用角色；`scripts/tests/Styles.Tests.ps1` 擋下外框寫死的顏色、在元件裡宣告 `--*-line` 旋鈕，以及共用控制項不用 `--control-line`。

## 角色

| 角色 | 淺色／深色 | 用於 |
| --- | --- | --- |
| `--control-line` | `--ink` 混入 `--line` 46%／32% | 次要按鈕、`SearchField`、`Select`（`field` 與 `pill`）、`nxField` 的 input／textarea／select、`DateTimePicker`、`TimeRangePicker`、勾選框、分頁按鈕組、有外框的 chip 與篩選鈕、Markdown 編輯框 |
| `--line` | `--p-stone-300`／`--p-night-600` | 列與區塊之間的分隔線、側欄與抽屜邊、浮層、對話框、toast、內嵌區塊（程式碼、預覽、`Notice`） |
| `--tile-line` | `--line` 70% | 資源表面（總覽、`Panel`、`ResourceCard`、檔案與儲存庫卡片）與其內的分隔 |
| `--material-line` | `--ink` 8% | 半透明材質（`FloatingToolbar`、黏頂列），見[質感](materials.md) |

- 判斷順序：使用者要點它、在裡面輸入或勾選，且外框是看出它在哪裡的線索 → control；圍住一塊內容 → surface（`--tile-line` 或 `--line`）；只是隔開兩段 → `--line`。
- 選項卡片、清單列與資源卡片靠內容辨識，仍屬表面；只有長得像表單控制項的才用 `--control-line`。
- 狀態不是角色：焦點 `--focus`、錯誤 `--danger`／`--error`、選取 `--accent` 與 `--selected-ring`、表面 hover 從 `--tile-line` 加深到 `--line`，都照原樣疊在角色上。

## 為什麼控制項要更深

- WCAG 1.4.11 要辨識元件所需的邊界與相鄰顏色有 3:1。`--line` 對白底只有 1.3:1，輸入框放在白色卡片上幾乎看不見邊界。
- `--control-line` 往 `--ink` 混到兩種主題都過 3:1：淺色對 `--surface` 3.4、`--canvas` 3.3，深色對 `--surface` 3.2、`--canvas` 3.6。`frontend/e2e/design.spec.ts` 在 `/admin/design` 的兩種主題預覽實測這個比值。
- 表面與分隔不承擔辨識功能，維持淡線，畫面才不會滿是框線。

## 改動方式

- 想讓整站控制項更淡或更深，只改 `tokens.scss` 兩個主題的混色比例，並重跑對比測試；不要在頁面或元件裡換色。
- 工具列上的膠囊（`Select` 的 `pill`、總覽 `.toolbar-pill`、資源列表的搜尋）與表單裡的欄位同色，只靠 `appearance` 換形狀。
