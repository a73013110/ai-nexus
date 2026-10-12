# 資源集合頁

結論：檔案庫、知識庫、專案、成果文件用同一套「列表 → 詳細」結構與總覽的卡片外觀，新的資源型功能照做，不另寫側欄清單或下拉切換。

## 路由

- 列表與詳細用 `optionalId('<path>')` 合成一條路由（`/knowledge`、`/knowledge/{id}`），切換時沿用元件。
- 後端總覽的最近項目、檔案用途等連結直接指向詳細頁，不用 query 選取。

## 列表

- `ResourceGallery`：一列膠囊工具（範圍 `ViewSwitch` segment、功能自己的 `[gallery-tools]`、搜尋）＋卡片格線；載入時保留格線形狀，沒有結果顯示 `[gallery-empty]`。格線最小寬度由 `--gallery-min` 調整。
- 卡片是連結 `a[nxResourceCard]`：圖示、名稱、最多兩行說明，meta 由功能投影（權限、版本、更新時間）。
- 有操作的卡片（檔案）所有卡片共用一個 `MenuPanel`，不每張建 `ActionMenu`。

## 詳細

- `FeaturePage` 的 `back` 在標題前放返回列表；標題、說明換成該資源的名稱與說明。
- 頁首一個主要動作（開始對話、上傳文件），其餘（編輯、權限、封存、刪除、匯出）放進「⋯」`ActionMenu`。
- 內容是 `Panel`（小標題、圖示、數量、右側動作）組成的雙欄：左側是工作，右側是脈絡；`tablet` 以下疊成一欄。可拖放上傳的區域把 `nxFileDrop` 放在 Panel 上。
- 載入用 `.skeleton` 排出版面；讀取失敗的 `Notice` 附「重試」。

## 共用字樣

權限用 `accessLabel`（我擁有／共用 · 可編輯／共用 · 可閱讀），文件處理狀態用 `documentStatus`，時間用 `relativeTime`；不要各頁自寫對照。
