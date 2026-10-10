# 知識檢索架構

正式環境固定 SQL Server 2025（17.x），預設 Ollama `bge-m3`、1024 維。本次移除舊片段與 JSON 向量查詢，保留原始檔及 `DocumentPages`；可重新上傳資料，也可由保存的頁面重建，無須重做 OCR。SQLite 僅供測試。

## 元件與資料流

```text
原檔 → document-ingest（逐頁擷取／OCR checkpoint）
     → ITextChunker → Chunks → document-embedding
     → IEmbeddingClient → hash 快取／正規化 → 維度分表

授權 collection IDs + query
  → IQueryRewriter → 查詢向量快取 → IRetrievalStore（ACL → vector／FTS → RRF）
  → IRerankClient → 文件多樣性／相鄰合併 → ContextTokens → 來源再授權
```

各元件透過 DI 替換；公文、校務 adapter 匯入授權知識庫後可共用此管線，不直接繞過來源 ACL。`KnowledgeRetrieval` 委派 `RetrievalPipeline`，對話、搜尋測試與品質評測使用同一套處理。

`StructuredChunker` 先找標題、中文條文、段落及句界，再做硬切；維護標題階層，允許跨頁，引用顯示起訖頁。預設目標 450、上限 700、最小 80 tokens、句界重疊 12%。CJK 約每字一 token，其他文字約四字元一 token；這是估算。表格按完整列處理，超過硬上限的單列明確失敗，不悄悄截斷。UTF-16 surrogate pair 保持完整。

實際 embedding 輸入為 `文件名 › HeadingPath\n本文`，`Text` 只存本文。SHA-256 ContentHash 計算完整輸入；同 profile 的相同 hash 可複製向量。批次預設 16，背景併發 1，在聊天生成或排隊時讓出 GPU；容量限制屬每個程序，已發出的請求不會被聊天搶占。每批寫入使用多列 Dapper INSERT，參與 lease-fenced checkpoint 的同一個 EF transaction。HTTP 共用具名 client、逾時與暫時錯誤重試；最多重試兩次，408／429／5xx／網路錯誤採指數退避與抖動。向量檢查數量、維度、有限值與非零值後 L2 正規化。

查詢快取以 profile key + 正規化查詢 hash 為鍵，獨立 `IMemoryCache` 最多 512 筆、預設 TTL 10 分鐘，條帶鎖抑制重複請求。embedding 與 rerank 共用每人每日 20,000 次配額、SQL 使用者列鎖與 `ModelQuotaLock`；批次算一次，快取命中不新增模型呼叫。

## Schema 與 profile

`EmbeddingProfiles` 保存 provider、model、dimensions、input format、query instruction、revision、chunker 版本及參數快照。Key 的 hash 防止向量空間混用；filtered unique index 強制最多一個 active。`Chunks` 使用一套切段布局，保留 Guid 引用鍵、唯一 int identity SearchId、文件序號、頁碼範圍、HeadingPath、ContentHash 與 TokenEstimate。

`ChunkEmbeddings768`／`ChunkEmbeddings1024` 以 int identity clustered PK 預留 ANN 映射，真正保存 `VECTOR(n)`；每個 `(ProfileId, ChunkId)` 唯一，片段刪除 cascade 向量。同一片段可持有 active 與 building 向量，但一次查詢只排名 active profile。SQLite 使用 blob converter 與測試用 cosine／詞頻，正式環境沒有 portable 或舊索引查詢。

啟動無 active 時建立目標為 active，將 migration 標記的文件排入索引；設定變更則建立 building，預設等待管理員「開始重建」。`embedding-reindex` 逐文件／逐批補齊，重試跳過已完成向量，使用文件擁有者授權與配額。新增／編輯資料填入 active 及所有 building；active 完成即可 ready，building 失敗可續跑。編輯前先取得 hash 快取，未變片段沿用 ID／向量；單一布局不保存舊切段版本。

啟用需所有文件 ready 且片段覆蓋率 100%，在 serializable transaction 內將舊 active 改 retired、building 改 active。`AutoActivate=true` 會排重建並完成後自動切換，需已有管理員；預設 false。退役向量可由管理員清除，或每小時清理超過 `RetiredRetentionDays=7` 的資料；保留 profile 與原文。操作有權限檢查及稽核。這支援往後模型版本管理，不提供舊版 JSON 索引兼容。

## 混合排名與授權

SQL 的 Authorized CTE 先限制授權 collections、ready 與非刪除文件，再計算 cosine／全文候選，向量 40、全文 40，合併取 30 供重排。RRF 為：

```text
score = VectorWeight / (RrfK + vectorRank) + FtsWeight / (RrfK + ftsRank)
```

排名從 1 開始，缺少的通道貢獻零；預設 k=60、權重各 1。使用 FREETEXTTABLE 及參數化自然語言，避免全文查詢語法注入。**不傳全域 top_n_by_rank**：該參數在授權 join 前截斷整庫，會使未授權資料擠掉授權結果。因此先 join Authorized 再 ROW_NUMBER／TOP FtsCandidates，這是為遵守「ACL 先於 TOP」而調整規格的字面 SQL。[Microsoft 全文排名限制](https://learn.microsoft.com/en-us/sql/relational-databases/search/limit-search-results-with-rank?view=sql-server-ver17)

預設 hybrid；全文元件、1028 斷詞器或 index 不可用時明確回報 vector 並記 log，keyword 則回應 503。Embedding provider none 強制 keyword。MinVectorScore=0 不做分數過濾。重排 provider 非 none 時依 MinScore 過濾；失敗依 skip／fail，skip 回報 `hybrid(rerank-skipped)` 等實際模式。不同重排模型分數不能直接互比。

最後取 TopK=6，每文件最多 3 片段，相鄰 Ordinal 合併、去除句子重疊，再按 ContextTokens=3500 裁切，預算含標題及來源框架。最近 4 輪追問才做本機 query rewrite，走 ModelTaskService、生成 token 配額與 5 秒逾時；僅選實際可用且核准的 Ollama 模型。失敗使用原句並標 rewrite-skipped。改寫只用於檢索，僅 debug log 記錄，未對使用者顯示或存檔；正式環境應限制 debug log 留存。

每個遠端呼叫後、來源送 reranker／回答模型／瀏覽器前重新檢查 collections 與命中授權，索引切換則請求重試。來源與對話歷史以資料傳入，防注入指令不授予它們指令權限。

## 全文與維度擴充

SQL Setup 對既有 instance 加入「Full-Text and Semantic Extractions for Search」。migration 在交易外建立 catalog 及 Chunks(Text, HeadingPath) 的 LANGUAGE 1028 index，key 為 SearchId 唯一索引、CHANGE_TRACKING AUTO；缺少全文不阻擋 migration，但有 warning。全文填入非同步，新文件可能暫無 keyword 命中。[Microsoft 新增功能](https://learn.microsoft.com/en-us/sql/database-engine/install-windows/add-features-to-an-instance-of-sql-server-setup?view=sql-server-ver17)

```sql
SELECT SERVERPROPERTY('ProductMajorVersion'), SERVERPROPERTY('IsFullTextInstalled');
SELECT * FROM sys.fulltext_languages WHERE lcid = 1028;
SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks');
```

執行 `scripts/Test-Environment.ps1 -SqlOnly` 或管理 → 知識檢索查看狀態；安裝後若 catalog 尚未建立，由 DBA 依 migration 的全文 DDL 建立 catalog/index，勿改 migration history。已登記的 migration 不會因重跑 idempotent script 而再執行。

新增維度：新增 EF vector entity／`ConfigureVector` 映射及對應表 migration；在單一 `VectorDimensions` allowlist／Table 映射加入維度，擴充 store、coverage、SQLite converter 與設定驗證／腳本。補齊 DatabaseDescriptions 與真實 SQL 測試，再生成 migration SQL、契約，建立新 profile 重建驗證。不能任意拼接使用者提供的表名或維度。

目前採精確 cosine，未啟用 VECTOR_SEARCH／CREATE VECTOR INDEX preview；int clustered PK 與 SearchId 已保留接縫。資料量、p95、Recall@K 確認成為瓶頸後，再評估正式支援的 ANN，必須保留授權候選邊界並驗證召回與撤權。不使用其他向量資料庫。四模式驗收與操作見 [品質評測](QUALITY.md)、[模型與重排](EMBEDDING_MODELS.md)。
