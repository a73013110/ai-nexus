# 形狀

結論：圓角看**元件做什麼**，不看它在哪一頁。立即改變畫面的工具（按鈕、搜尋、篩選、切換）是膠囊，填好才送出的值、清單列與內嵌區塊是 6px，浮層 12px，對話框與浮動面板 18px，資源卡片 20px。角色定義在 [`tokens.scss`](../../frontend/src/styles/tokens.scss) 的 `--radius-*`，元件只用角色；`scripts/tests/Styles.Tests.ps1` 擋下 `--p-radius-*`、寫死的長度與在元件裡改寫角色。

## 角色

| 角色 | 值 | 用於 |
| --- | --- | --- |
| `--radius-action` | 膠囊 | 主要／次要／危險／quiet 按鈕、icon 按鈕（圓形）、segment `ViewSwitch`、`SearchField` 的 `pill`、`Select` 的 `pill` 與 `inline`、chip 上的按鈕 |
| `--radius-field` | 6px | `nxField` 的 input／textarea／select、`Select` 的 `field`、`TreeSelect`、`DateTimePicker`、`TimeRangePicker`、`PersonPicker`（搜尋後選的是值） |
| `--radius-item` | 6px | 清單列、選單與 select 選項、側欄導覽、分頁標籤與底線 `ViewSwitch` 的按鈕、`SearchField` 的 `filled`（清單頂端，與下方的列同形） |
| `--radius-block` | 6px | 內嵌區塊：程式碼、預覽、`Notice`、表單 fieldset、`ChoiceGroups`、小型 tooltip |
| `--radius-surface` | 12px | 浮層、選單面板、toast、composer、`QueryBar`、選項卡片 |
| `--radius-floating` | 18px | 浮在頁面上的視窗：對話框、詳情抽屜（`--dialog-radius`）、可拖動的 `FloatingPanel`（助理） |
| `--tile-radius`／`--tile-inner-radius` | 20px／14px | 資源表面（總覽、`Panel`、`ResourceCard`）與其內層 |
| `--radius-full` | 膠囊 | badge、chip、計數、switch、meter、捲軸 |
| `--radius-mark` | 4px | kbd、行內 code、勾選框、骨架 |
| `--radius-data` | 2px | 圖例色塊、熱圖格、分段長條 |
| `--radius-icon` | 25% | 圖示底板、字母頭像（隨尺寸縮放） |

- 判斷順序：按下或輸入後畫面立即改變（動作、搜尋、篩選、切換檢視）→ action；值要等送出才生效 → field；是一列可點的清單 → item。
- 同一個元件放在不同容器會換角色，由 `appearance` 決定、不由頁面 CSS 決定；同名的值同義：`pill` 是工具列上的膠囊（`SearchField`、`Select`），`field` 是表單欄位（`Select`），`inline`／`bare` 是所在的列已是外框，`filled` 是浮層與清單頂端。
- 同一列不混膠囊與 6px：工具列、篩選列上與搜尋並排的 `Select` 用 `pill`。`SearchField` 沒有 `field`；表單裡搜尋後選一個值用 `PersonPicker`、可搜尋的 `Select` 或 `TreeSelect`。
- 圓形用 `50%`，貼邊的容器用 `0`，跟著父層用 `inherit`；其他值都取角色。

## 同心

- 容器裡的控制項圓角＝容器圓角減去內距：segment 晶片是 `calc(var(--radius-action) - 2px)`，浮動工具列的外框是 `--button-target / 2` 加內距。
- 容器自己就是軌道時不再疊一層表面：`FloatingToolbar` 設 `--view-switch-track: transparent`，裡面的 select 用 `inline`。
- 想換整站風格（例如按鈕改 6px）只改 `tokens.scss` 的角色值；不要在頁面或元件裡覆寫。
