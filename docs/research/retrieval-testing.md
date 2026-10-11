# 向量檢索測試與異常復原

先確認附件處理與索引工作能完成，再測召回、引用與權限。模型能力探測只驗證服務輸出形狀，完整驗收還需要文件完成索引及實際搜尋。

## 1. 確認服務與資料庫

以管理員登入，開啟「平台管理 → 知識檢索」：

- 「SQL 原生向量」須為可用；正式儲存為 SQL Server 2025。
- 全文元件、1028 中文斷詞器及全文索引均須可用，才能比較 keyword／hybrid。全文不可用時 hybrid 會降為 vector，須看實際模式。
- 按「驗證檢索模型」。bge-m3 須回傳成功、每筆 1024 維；這會呼叫真實模型並計入配額。
- 模型端點來自 `Knowledge.Embedding.Endpoint`，未指定時使用 `Inference.Providers.Ollama.Endpoint`。確認此網址是部署的 Ollama 設備。

若升級後 host 無法啟動，依啟動訊息套用待完成 migration，詳見 [資料庫](../architecture/database.md)。已經套用的 migration 不需重跑或清空資料。

## 2. 準備可核對的測試文件

建立私有知識庫「向量驗收」，用「貼上純文字」建立下列兩份來源，或另存 UTF-8 文字檔後上傳。先用文字測試，避免 OCR 干擾向量驗收。

**差旅規範**

```text
差旅費用與預支
員工出差前可申請交通及住宿費預支，須先取得主管核准。
返程後七個工作日內，應檢附單據辦理核銷並退回未使用款項。
```

**設備報修**

```text
資訊設備維修
筆記型電腦故障時，請向資訊服務台提出報修，附上設備編號及故障描述。
維修期間如需借用備用設備，須先由資訊服務台確認庫存與借用期限。
```

預期結果：背景任務完成，文件狀態為「就緒」，片段數大於零；管理頁 active profile 的向量覆蓋率達 100%，待處理文件為 0。只有 building profile 達成條件時才需按「啟用索引」；首次建立的 profile 自動為 active。

PDF 可在上述測試通過後另測：確認擷取文字、頁碼與原檔一致，掃描文字應有核對提示，引用可開啟對應頁面。

## 3. 比較實際召回

在「平台管理 → 知識檢索 → 檢索測試台」只選「向量驗收」，先取消「使用重排模型」。依序使用 vector、keyword、hybrid 執行：

| 查詢 | 應優先出現的來源 | 驗證目的 |
| --- | --- | --- |
| 出差之前可以先領交通和住宿的錢嗎？ | 差旅規範 | 語意相近、措辭不同的召回 |
| 七個工作日 核銷 | 差旅規範 | 原文關鍵字與全文召回 |
| 電腦壞掉，要找誰處理？ | 設備報修 | 區分不同主題 |

核對實際 mode、文件名稱、片段原文、頁碼，以及向量／全文排名、RRF 分數與分段耗時。keyword 對不同措辭可能沒有結果；hybrid 應結合兩路候選。同一句再次查詢可觀察查詢向量快取，但不能僅靠耗時判斷模型品質。

目前預設 `Knowledge.Rerank.Provider=none`，未部署重排服務時無法驗收真正的 hybrid+rerank；勾選後顯示 rerank-skipped 是明確降級。部署並驗證 TEI 或 OpenAI compatible 重排端點後，再測第四模式。

## 4. 測聊天引用與 ACL

在聊天「來源」選取測試知識庫，詢問「出差費用何時核銷？」，確認答案引用「差旅規範」且來源卡片能開啟原文。接著問「需要誰先核准？」測追問改寫；改寫需要核准且可用的本地生成模型，失敗時實際 mode 會標記 rewrite-skipped。管理測試台的單句搜尋不會觸發追問改寫。

用另一個沒有授權的帳號驗證：看不到此知識庫，無法讀取原檔、頁面或搜尋結果。授予閱讀權限後可檢索，撤銷後再次請求應被拒絕；管理員的測試台也只搜尋本人有權限的來源。

## 5. 使用固定題庫評測

開啟「品質評測 → 檢索評測」，選取測試知識庫，填入評測名稱及 1 至 20 題 JSON。將 `documentId` 換成閱讀頁網址中的實際文件 UUID：

```json
[
  {
    "id": "語意召回",
    "query": "出差之前可以先領交通和住宿的錢嗎？",
    "relevant": [{ "documentId": "差旅規範的 UUID", "pages": [1], "grade": 3 }]
  },
  {
    "id": "設備維修",
    "query": "電腦壞掉，要找誰處理？",
    "relevant": [{ "documentId": "設備報修的 UUID", "pages": [1], "grade": 3 }]
  },
  {
    "id": "無答案",
    "query": "公司今年發放多少年終獎金？",
    "relevant": [],
    "noAnswer": true
  }
]
```

按「開始四模式評測」，完成後查看 Recall@K、MRR、nDCG@K、無來源拒答率及延遲 p50／p95。含降級或服務未啟用的模式不應當成有效比較。預設 `MinVectorScore=0` 不過濾低相似度候選，無答案題仍可能取得來源；應用固定題庫校準門檻，不能預設拒答率必須為 100%。這裡的拒答率只衡量沒有檢索來源，沒有測生成答案是否正確。

## 自動化回歸

```powershell
dotnet test --solution backend/AiNexus.slnx --filter 'FullyQualifiedName~.Knowledge.|FullyQualifiedName~.Quality.|FullyQualifiedName~JobLifecycleTests'
```

`RetrievalDependencyTests` 驗證正式 SQL 檢索服務所需依賴，並啟用 DI scope validation，確認實際改寫器與全部背景處理器可正確建立。其餘測試使用 SQLite 與替代模型，驗證工作續跑、profile、ACL、快取與評測流程；不代表真實 Ollama 或 SQL 原生向量已通過。

真實 SQL Server 的 768／1024 向量、交易及中文全文測試需設定測試 instance 的 `AINEXUS_SQLSERVER_TEST`，詳見 [資料庫整合測試](../architecture/database.md)。每項建立及清理自己的暫時資料庫；未設定連線時會明確略過。

若全文元件顯示已安裝，但 `FREETEXTTABLE` 實際回傳 SQL 30053，仍須由 SQL 管理員檢查斷詞器、Filter Daemon Launcher／FDHost 與服務帳號。[Microsoft 全文錯誤說明](https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/mssqlserver-30053-database-engine-error?view=sql-server-ver17)。應用程式對已知全文執行錯誤會讓 hybrid 明確降為 vector，30 秒後重新檢查；keyword 則回報 `fulltext_unavailable`。SQL 2025 部分全文錯誤也與索引版本及未註冊的 stemmer 有關，應依實際版本確認，不能僅由錯誤代碼判定根因。[SQL Server 2025 已知問題](https://learn.microsoft.com/en-us/sql/sql-server/sql-server-2025-known-issues?view=sql-server-ver17#full-text-search)。

## 本次附件錯誤與復原

若日誌只有 `InvalidOperationException, job_processing_failed`，不能單靠例外類型判斷是模型或 SQL 問題。本次可重現兩個服務註冊問題：正式 `SqlServerRetrievalStore` 所需的 `IMemoryCache` 未註冊；`IQueryRewriter` 原先註冊為 Singleton，卻使用 Scoped 的 `ModelTaskService`，Development 的 scope validation 會拒絕從 root provider 解析它。

背景 worker 在執行任務前會解析所有 `IBackgroundJobHandler`；檢索評測處理器使用 SQL 檢索服務與改寫器，因此也會使 document-ingest／document-embedding 在第一個 checkpoint 前失敗。原先 SQLite host 沒有走 SQL 檢索服務，Testing host 也沒有強制 scope validation，才漏掉這些錯誤。修正後註冊 memory cache，並讓改寫器與模型工作服務使用相同 scope。

完成修正版建置並重新啟動本機服務後，到「背景任務 → 需要處理」重試失敗的文件任務。重試保留原檔與已保存的 `DocumentPages`，索引工作會繼續建立片段及向量。確認任務完成後再測搜尋；沒有超過六次嘗試上限時，不需重新上傳原檔。
