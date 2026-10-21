# 形狀

結論：圓角只看**元件是什麼**，不看它放在哪一頁。按得下去的動作都是膠囊，收集值的欄位、清單列與內嵌區塊是 6px，浮層 12px，資源卡片 20px。角色定義在 [`tokens.scss`](../../frontend/src/styles/tokens.scss) 的 `--radius-*`，元件只用角色；`scripts/tests/Styles.Tests.ps1` 擋下 `--p-radius-*`、寫死的長度與在元件裡改寫角色。

## 角色

| 角色 | 值 | 用於 |
| --- | --- | --- |
| `--radius-action` | 膠囊 | 主要／次要／危險／quiet 按鈕、icon 按鈕（圓形）、segment `ViewSwitch`、`SearchField`、`Select` 的 `pill` 與 `inline`、chip 上的按鈕 |
| `--radius-field` | 6px | `nxField` 的 input／textarea／select、`Select` 的 `field`、`DateTimePicker`、`TimeRangePicker` |
| `--radius-item` | 6px | 清單列、選單與 select 選項、側欄導覽、分頁標籤與底線 `ViewSwitch` 的按鈕 |
| `--radius-block` | 6px | 內嵌區塊：程式碼、預覽、`Notice`、表單 fieldset、`ChoiceGroups`、小型 tooltip |
| `--radius-surface` | 12px | 浮層、選單面板、對話框、toast、composer、`QueryBar`、選項卡片 |
| `--tile-radius`／`--tile-inner-radius` | 20px／14px | 資源表面（總覽、`Panel`、`ResourceCard`）與其內層 |
| `--radius-full` | 膠囊 | badge、chip、計數、switch、meter、捲軸 |
| `--radius-mark` | 4px | kbd、行內 code、勾選框、骨架 |
| `--radius-data` | 2px | 圖例色塊、熱圖格、分段長條 |
| `--radius-icon` | 25% | 圖示底板、字母頭像（隨尺寸縮放） |

- 判斷順序：會觸發動作或切換檢視 → action；要使用者輸入值 → field；是一列可點的清單 → item。`Select` 與 `ViewSwitch` 由 `appearance` 決定角色，不由頁面 CSS 決定。
- 圓形用 `50%`，貼邊的容器用 `0`，跟著父層用 `inherit`；其他值都取角色。

## 同心

- 容器裡的控制項圓角＝容器圓角減去內距：segment 晶片是 `calc(var(--radius-action) - 2px)`，浮動工具列的外框是 `--button-target / 2` 加內距。
- 容器自己就是軌道時不再疊一層表面：`FloatingToolbar` 設 `--view-switch-track: transparent`，裡面的 select 用 `inline`。
- 想換整站風格（例如按鈕改 6px）只改 `tokens.scss` 的角色值；不要在頁面或元件裡覆寫。
