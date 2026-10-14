# 0009 模組同名對應與資料化選單

- **狀態**：採用（2026-10-14）
- **決定**：
  - 一個模組在各層用同一個名字：後端 `AiNexus.Features/<Module>/`、前端 `features/<module-kebab>/`、資料庫 schema `<module>`。前端唯一例外是沒有後端的 `design/`。
  - 「再往下一層」只發生在模組內：根目錄超過 15 個檔案就依能力分子資料夾，前後端子資料夾同名（`Knowledge/Documents` ↔ `knowledge/documents`）。規則見 [後端慣例](../architecture/backend-conventions.md) 與 [前端邊界](../frontend/frontend-boundaries.md)。
  - 選單大類（AI 工作台、協作、系統管理…）是資料，存在 `accesscontrol.NavigationGroups`，可多層、由管理者調整；不反映在程式目錄。

## 原因

- 新人從畫面、API 路徑或錯誤訊息找到模組名後，前後端都能直接找到對應資料夾，不必記第二套分類。
- 選單分組會隨營運調整；目錄若跟著選單分組，管理者每移一次功能，程式就得搬一次。
- 後端不加「大類」層：`ModuleBoundaryTests` 以 namespace 第一段判斷模組（[ADR 0004](0004-module-dependencies-without-cycles.md)），多一層只會讓每個模組的歸屬多一次判斷，卻沒有邊界上的效益。
- 路由網址與功能 ID（`/files`、`tasks`）是使用者與資料看得到的名稱，維持不變；只有程式資料夾對齊後端模組。

## 擴充方式

新系統（例：打卡、請假）開一個後端模組（例：`Workforce`，底下分 `Attendance/`、`Leave/`），前端對應 `features/workforce/`；模組的 `FeatureSeed` 宣告自己的選單群組（`workforce`「員工服務」）、圖示與功能。

## 何時重新評估

模組數多到單層難以瀏覽（約 40 個以上），或某些模組需要獨立部署時（見 [ADR 0002](0002-vertical-slice-single-features-project.md)）。
