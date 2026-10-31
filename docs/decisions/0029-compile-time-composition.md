# 0029 以 source generator 在編譯期組合模組

- **狀態**：採用（2026-11-01）
- **決定**：
  - 功能模組、端點切片、請求驗證器的註冊由 `FeatureCompositionGenerator` 在編譯期產生。`FeatureModules` 不再手寫模組清單，執行期也不反射掃描組件。
  - 模組之間不可依賴註冊順序；產生的順序只依完整型別名稱。
  - 之後的 source generator 都放在 `AiNexus.Generators`，一個產生器一個資料夾，共用 `Common/` 與 `AiNexus.GeneratorTests` 的測試工具。寫法見 [Source generator](../architecture/source-generators.md)。

## 原因

- **手寫清單**：每加一個模組都要改同一個檔案的兩處（Add、Map）。多人同時新增模組必然衝突；漏寫一處要到執行期才發現，端點 404 或服務解析失敗。
- **清單順序帶有隱含行為**：總覽區塊與 OpenAPI 的順序都取決於清單順序，所以清單不能排序，也合不成一份。改成產生後，順序只依型別名稱；需要順序的消費端自己宣告，例如監控來源的 `Order`、總覽前端的 `ORDER`。
- **反射掃描**：能免除清單，但有三個代價。static abstract 介面成員要透過 `MakeGenericMethod` 呼叫，失去編譯期檢查；每次 host 啟動（包括每個整合測試的 host）都要掃描數千個型別；也妨礙之後做 trimming。
- **產生器**：在編譯期就知道全部型別。漏實作的 partial 方法、引用不到的型別（private 巢狀類別）都會變成編譯錯誤。產出是普通的直接呼叫，執行期沒有額外成本。
- **一併取代 FluentValidation 的組件掃描**：不再需要 `FluentValidation.DependencyInjectionExtensions`，Platform 只依賴 FluentValidation 本身。
- **與 [ADR 0008](0008-no-json-source-generation.md) 一致**：0008 不用 JSON source generation，是因為它要人工維護 DTO 清單；這裡的產生器正好相反，是用來移除人工清單。

## 取捨

- 多一個 netstandard2.0 專案與它的測試專案。產生器出錯比一般程式難除錯，所以產出只放直接呼叫，判斷邏輯留在手寫程式碼。
- OpenAPI 文件與 `schema.ts` 的端點順序改為依模組完整名稱排列，這次會一次性重排。
- 產生器看不到其他組件，Data 專案的 `AppDatabaseModule` 仍要明確註冊。

## 何時重新評估

- 要把模組拆到多個組件時：產生器改讀引用組件的中繼資料，或每個組件各自產生，再由 Host 串接。
