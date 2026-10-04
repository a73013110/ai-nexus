# SQL Server 2025 向量檢索設計

AI Nexus 將原始文件、逐頁文字、來源權限與向量留在同一個 SQL Server database。權限以知識庫的擁有者／具名成員／有效群組檢查，檢索前及遠端 embedding 呼叫後都再確認一次；不先搜尋全庫再於前端隱藏結果。

## 現行實作

`knowledge.Chunks.EmbeddingJson` 保存 768 維正規化向量，profile 包含 provider、模型、維度。SQL Server 2025 migration 額外建立 `EmbeddingVector VECTOR(768)`；向量寫入與片段 checkpoint 在同一 EF transaction 內完成。

SQL 路徑以授權知識庫、未刪除、已完成、相同 profile 作為 WHERE 條件，再以 `VECTOR_DISTANCE('cosine', …)` 排序及 TOP K。無原生欄位時使用同一授權範圍的可攜式 cosine；候選量超過設定上限就回報縮小範圍，不任意丟棄來源。明確設定 provider=`none` 才用關鍵字，不因遠端失敗悄悄降低為另一種檢索方式。

`VECTOR` 與精確距離適合第一階段驗證和中小型來源。SQL Server 2025 的 approximate `VECTOR_SEARCH`／`CREATE VECTOR INDEX` 目前仍有預覽限制；本版不自動啟用 preview 功能。[VECTOR 型別](https://learn.microsoft.com/en-us/sql/t-sql/data-types/vector-data-type?view=sql-server-ver17)、[VECTOR_DISTANCE](https://learn.microsoft.com/en-us/sql/t-sql/functions/vector-distance-transact-sql?view=sql-server-ver17)、[向量搜尋](https://learn.microsoft.com/en-us/sql/t-sql/functions/vector-search-transact-sql?view=sql-server-ver17)。

```powershell
./scripts/Test-SqlCapabilities.ps1
```

此指令以本機設定檢查實際版本、edition、原生向量與 cosine 距離；不輸出連線帳密。結果放在忽略版控的 `artifacts/sql-capabilities.json`。較舊 SQL 版本可使用 JSON 可攜式路徑；升級至 2025 後需要另行建立／回填原生欄位，不只切換設定。

## 公司系統整合的原則

公文及校務資料來源以受控唯讀 adapter 擷取。原系統資料庫與帳號保持獨立，不將整個來源庫複製到聊天，也不讓 LLM 產生 SQL。來源 adapter 必須回傳可信識別碼、版本／修改時間及權限範圍；匯入至明確授權的來源後才切段索引。

公文可先從文件標題、文號、部門、流程狀態、核准的本文及附件開始；版本和核章記錄要保存來源識別碼，避免回答引用過期文件。機密等級及承辦／部門存取必須以原系統授權為準。

校務應先選作業規範、公開表單與政策。學生個資、成績與人員資料需要逐類唯讀查詢和原系統權限映射；使用者對某張表有查詢權不代表能取得所有學生資料。不以平台管理員身份推定所有外部資料權限。

資料量增加後先量測來源筆數、候選量、回應時間、引用命中率與權限拒絕案例，再評估混合關鍵字／向量排序、預先摘要與 ANN。升級 ANN 前以固定評測集比較召回率、延遲、更新行為，並驗證其實際 filter 行為能維持相同授權邊界。
