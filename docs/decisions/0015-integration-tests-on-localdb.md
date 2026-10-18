# 0015 整合測試在 LocalDB 上以 dacpac 建庫

- **狀態**：採用（2026-10-18）
- **取代**：[ADR 0005](0005-sqlite-tests-sqlserver-opt-in.md)
- **決定**：整合測試一律連 LocalDB（SQL Server 2025 Express）。每個測試程序以 `database/App` 的 dacpac 與 `Seed/` 建一個範本，每個 host 複製範本的 mdf／ldf 後 attach 成自己的資料庫，結束時刪除。不用 SQLite，也沒有選用的 SQL Server 測試分類；只有依賴全文索引的測試在沒有全文元件時略過。不用 Testcontainers。

## 原因

- 資料庫優先（[ADR 0010](0010-database-first-schema.md)）之後，結構、索引、約束與 procedure 只存在資料庫；SQLite 依 EF 模型建庫，驗證不到唯一性、filtered index，也跑不了 procedure。
- 正式程式不必再為 SQLite 留分支（記憶體內檢索、記憶體內彙總、自編號）；測試走的就是正式的 SQL。
- 開發與 AI 助手的環境不能跑 Docker；LocalDB 隨 Visual Studio／SQL Server Express 安裝，不需要服務帳號或權限設定。
- EF 模型與資料庫的一致性（[ADR 0011](0011-data-access-ef-dapper-procedures.md#ef-的角色)）與 `MS_Description`（[ADR 0012](0012-database-naming-and-foreign-keys.md)）都對 dacpac 建出的資料庫檢查。

## 為什麼每個 host 一個資料庫

| 做法 | 每次成本（實測） | 取捨 |
|---|---|---|
| 每次以 SqlPackage 部署 dacpac | 約 20 秒 | 太慢，只用來建範本 |
| 從範本 RESTORE | 約 0.3 秒 | 可行，但比複製檔案慢 |
| 複製範本 mdf／ldf 再 attach | 約 0.15 秒，平行時約 0.25 秒 | **採用**；刪庫在背景執行 |
| 每個測試類別共用資料庫，測試結束回復交易 | 幾乎為零 | host 的請求、生成 worker 與 Dapper 各自開連線，包不進同一個交易；隔離不完整會造成難查的互相干擾 |

- 預設測試集（後端三個專案）從 SQLite 的約 39 秒變成約 65 秒（16 執行緒開發機；不含第一次建範本的約 25 秒）。慢的部分主要是真實的 SQL 往返，可以接受。
- 範本依 dacpac 的 `model.xml` 與要執行的腳本內容快取在暫存資料夾，結構不變時下次測試直接沿用。

## 代價

- 執行測試的機器必須有 SQL Server 2025 的 LocalDB；LocalDB 偶爾無法啟動，排除步驟見 [測試資料庫](../development/test-database.md#localdb-無法啟動)。
- LocalDB 沒有全文元件：關鍵字檢索、hybrid 的全文排名與全文失敗回退不會在每次 `Verify.ps1` 驗證，改由 `Test-Environment.ps1 -SqlOnly` 與部署驗收補上。hybrid 在沒有全文時降為 vector 是正式行為，相關測試照這個行為斷言。
- 測試期間 LocalDB 會占用 CPU；開發機同時跑其他重負載時，時間會拉長。

## 何時重新評估

LocalDB 無法再安裝或跟不上目標版本，或預設測試集超過 2 分鐘時。
