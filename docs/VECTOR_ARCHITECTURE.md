# SQL Server 2025 向量檢索設計

AI Nexus 將原始文件、逐頁文字、來源權限與向量留在同一個 SQL Server database。權限以知識庫的擁有者／具名成員／有效群組檢查，檢索前及遠端 embedding 呼叫後都再確認一次；不先搜尋全庫再於前端隱藏結果。

## 現行實作

`knowledge.Chunks.EmbeddingJson` 保存 768／1024 維正規化向量，profile 包含 provider、模型、維度與前處理／revision。SQL Server 2025 migrations 建立 `EmbeddingVector VECTOR(768)` 與 `EmbeddingVector1024 VECTOR(1024)`；向量寫入與片段 checkpoint 在同一 EF transaction 內完成。

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

## 公文與校務的落地順序

本版已分析 GDWEB_HOTAI 與 HL_Mvc_MEIHO 原始碼並準備固定 Dapper／EDoc adapters，來源契約見 [INTEGRATIONS](INTEGRATIONS.md)。**實際來源連線與逐筆授權 view 尚未啟用**；不把程式中的搜尋篩選或選單角色直接認定為完整的資料讀取權。

| 資料                           | 建議處理                         | 原因                             |
| ------------------------------ | -------------------------------- | -------------------------------- |
| 公文本文、核准附件、規範       | 有原文與頁碼的文字索引／向量檢索 | 適合語意找資料與引用核對         |
| 文號、流程狀態、簽核／版本歷程 | 固定參數化唯讀查詢               | 精確值與最新狀態應以來源為準     |
| 校務規章、公開表單、單位說明   | 先經明確授權的知識庫索引         | 有適合團隊閱讀的文字來源         |
| 學生個資、成績、健康／人員資料 | 另建逐筆授權的專用查詢 adapter   | 不能由選單可見性推定所有資料可讀 |

GDWEB 的 Doc_vwInOutDetail 可提供文號、主旨、承辦及部門 metadata，但既有報表的部門篩選不足以推定機密／承辦／代理的完整 ACL。MEIHO 的 T_Account_Group／WebFunctionRole／T_WebFunctionAcc 決定功能入口，不能取代學生資料的 row scope；目前 adapter 只接受 reference／organization 類型。

以下是下一階段**受控來源索引的建議設計**，尚未實作自動同步：

1. DBA 完成來源帳號映射與授權 view，用至少兩個帳號驗證允許／拒絕、代理／部門、停用與版本變動。使用專用 view-only SQL 登入，不授原始表全集讀取。
2. 來源目錄保留 SourceId、ExternalId、Revision、修改時間、來源 URI／頁碼、分類及可信 ACL 識別。唯一鍵包含來源與外部 ID，修訂變更觸發重建，刪除／撤權先停用檢索。
3. ACL 與索引分開同步，使用者查詢時仍以當下來源授權限制候選文件；來源驗權服務不可用時拒絕查詢，不能退回舊 grant。已在平台另存的明確個人副本使用另一套保存契約，不與同步鏡像混用。
4. 通過授權後才讀本文、逐頁 OCR／切段、建立 embedding；先在 staging 保存完整新版本，再切換 ready 版本，避免半份文件進入檢索。引用記錄來源／修訂／頁碼，開原文時再驗權。
5. 用品質評測集保存代表問題、正確引用與拒絕案例，量測引用命中、無答案行為、ACL 撤銷、索引延遲及查詢 p50／p95。達成部署單位的目標後，再擴大範圍。

**我的建議是先繼續使用 MSSQL 的精確向量檢索。**目前原文、ACL、片段、版本與向量已可在同一交易範圍管理，不必為第一階段再引入另一個資料庫。此為本專案的架構判斷；若未來實測顯示需要更大規模、獨立擴展或更成熟的 ANN filter，再以相同權限契約比較其他搜尋服務。

## ANN 升級的實際限制

2026-10-04 查閱 Microsoft 文件：SQL Server 2025 的 VECTOR_SEARCH／向量索引仍為 preview；不要將 Azure SQL 的 GA 或最新索引能力直接套用到本機 17.x 版本。新舊索引的篩選及寫入行為不同，升級需查實際 build／索引版本與 execution plan。[VECTOR_SEARCH 功能與版本](https://learn.microsoft.com/en-us/sql/t-sql/functions/vector-search-transact-sql?view=sql-server-ver17)。

文件所列向量索引限制包含 **int 的 clustered primary key**。目前 Chunks 使用 Guid 主鍵，不能直接假設加一條 CREATE VECTOR INDEX 就能完成；若採 ANN，需另規劃 int surrogate 的搜尋表、ChunkId 映射、同步及重建策略。精確 VECTOR_DISTANCE 本身不使用向量索引，新增 ANN index 不會自動加速現有查詢。[索引限制](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-vector-index-transact-sql?view=sql-server-ver17)、[精確距離](https://learn.microsoft.com/en-us/sql/t-sql/functions/vector-distance-transact-sql?view=sql-server-ver17)。

本版支援 768／1024 維 float32 與版本化 embedding profile。BGE-M3 與 Qwen 的實際設定與比較工具見 [EMBEDDING_MODELS](EMBEDDING_MODELS.md)。更換向量模型、維度或前處理需建立新 profile 並重建全部片段，不能只因維度相同就混合向量；聊天模型更換則不必同步更換 embedding。對話用 Google、未來本機模型也可共用核准的同一檢索服務。
