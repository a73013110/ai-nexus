# 詳情抽屜

結論：逐筆詳情用 `DetailDrawer`，放大、記憶大小與版面都由它負責；頁面只組合下面的區塊，不寫抽屜的外框、內距或寬度。範例在 `/admin/design` 的資料區段。

## 組成

依序放在 `nx-detail-drawer` 裡：

| 區塊 | 用途 |
| --- | --- |
| `drawer-actions` | 上下筆（`DrawerPager`）、操作選單 |
| `nx-drawer-summary` | 狀態列（投影徽章與時間）、`heading`、`subtitle`；識別字加 `mono` |
| `drawer-tabs` | 頁籤，和摘要在同一塊不捲動的標題區 |
| 內文 | 唯一的捲動區；數個區塊時包 `.ui-drawer-sections` |
| `nx-drawer-foot` | 不隨內文捲動的動作，`note` 寫在按鈕左側 |

- `title` 是無障礙名稱；有摘要時退成小標，紀錄名稱才是視覺標題，所以 `title` 寫功能名（「郵件詳情」），不放紀錄內容。
- 摘要與底部要依條件出現時用單層 `@if`：巢狀的控制流程投影不到它們的位置，會掉進內文。
- 內容順序：摘要 → 主要內容（差異、訊息、指標）→ 基本資訊 → 關聯（可複製的 ID、跨功能反查）→ 原始資料（`details.ui-raw-record`，預設收合、純文字）。
- 上下筆只在本頁移動；有未儲存變更時先確認。識別碼用 `CopyValue`。

## 大小

- `preferenceKey` 必填，每種抽屜一把，使用者放大或縮小後記住；沒有調整過時用 `defaultSize`，差異比對這類要寬度的內容設 `wide`。
- 寬度只定義在 `--drawer-width`、`--drawer-width-wide`，頁面不覆寫。
- 內文是名為 `drawer` 的 container：內容用 `@container drawer` 換欄數，放大才不只是變寬（`.ui-description-list` 寬時四欄）。
- 桌面 modeless，`tablet` 以下改為 modal，`phone` 以下貼滿並隱藏放大鈕。
