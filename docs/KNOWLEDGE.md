# 知識庫、文件閱讀與背景處理

從工作區「知識庫」建立資料來源，直接上傳 PDF、Word、圖片或 UTF-8 文字，也可從檔案庫選取已有原檔。新知識庫預設私有；擁有者可從名稱右側選單授權具名使用者閱讀／編輯，或授權功能群組閱讀。成員必須先登入過 AI Nexus。群組成員不會因閱讀授權取得編輯能力。

上傳後立即可以開啟原始文件。PDF 支援頁碼、縮放、原始頁面／擷取文字切換；文字搜尋會列出符合頁碼，引用可直接開啟指定頁面。每個原檔、頁面、搜尋與任務摘要 API 都重新驗證權限；只有發起任務且仍有文件編輯權限的使用者能停止／重試。

檔案庫保存個人的原始檔；知識庫保存可檢索的來源、頁面、索引與 ACL。加入共用知識庫會授權該知識庫的成員閱讀原檔，並在選取時明確提示。原始 binary 不重複上傳／保存；同一擁有者已有完成的原文擷取時會重用頁面，檢索索引及授權仍各自獨立，見 [檔案庫](FILES.md)。

一般點擊會開大型預覽彈窗，保留原頁的對話、輸入草稿與捲動位置；Ctrl／Cmd 點擊及直接 `/reader/...` 連結保留獨立閱讀頁。圖片預覽直接顯示原圖，使用者選擇「辨識文字」才建立 OCR 任務；PDF／文字閱讀使用同一個 `DocumentViewer`。

## 從文件到回答

1. 原檔保存於 `attachments.Attachments`，文件以 `ResourceAttachments` 保留引用，避免草稿清理回收來源。
2. PDF 原生文字以 PdfPig 逐頁擷取。沒有足夠可讀文字的掃描頁，以及上傳圖片，由核准的圖片模型辨識；輸出截斷會報錯，不將不完整頁面當成成功。
3. 每頁完成即保存 checkpoint。OCR 文字標記為需要核對，原檔始終是確認依據。無法取得頁面圖片時，請改上傳頁面圖片；本版不會臆造缺失文字。
4. 知識庫文件切成 600 字元、80 字元重疊片段；逐段建立向量。只有全部完成的文件才進入搜尋。
5. 聊天輸入框「來源」選取最多三個知識庫，選取即保存。提交前先檢索有權限的來源，再將引用資料與當次生成參數一起保存。
6. 回答下方的來源卡片可一鍵開啟原始頁碼。原文是參考資料，系統指令明確禁止執行來源內容的指令。使用者仍應核對回答與原始文件。

一般聊天掃描 PDF 也自動建立私有閱讀任務；辨識完成前暫停送出。圖片聊天使用模型原生圖片能力。移除未送出的草稿附件時，其私有閱讀器與背景任務一併清理；已連到對話、知識庫或專案的附件依引用保留。

刪除知識來源會清除頁面與片段，原檔仍保留在上傳者的檔案庫；對話回答及當時引用摘要保留，失效來源連結回應 404。刪除整個知識庫也移除對話的選取關聯。原檔要在沒有其他引用後從檔案庫明確刪除，才會釋放空間。重新索引會沿用已完成的頁面與相同向量 profile 的片段；模型或維度變更則重建索引。

## 設定

一般設定放 `.local/config/appsettings.Local.json`；Google key 共用 `.local/secrets/appsettings.Secrets.json` 的 `Inference.Providers.Google.ApiKey`，只由後端使用。聊天與向量模型各自選擇 provider；完全本地運行請參閱 [本地 AI 與向量化](LOCAL-AI.md)。

| 參數                                                  | 預設                 | 用途                                          |
| ----------------------------------------------------- | -------------------- | --------------------------------------------- |
| `Knowledge.Embedding.Provider`                        | `google`             | `google`、`ollama`；`none` 明確改用關鍵字搜尋 |
| `Knowledge.Embedding.Model`                           | `gemini-embedding-2` | 向量模型，與聊天模型分開                      |
| `Knowledge.Embedding.Dimensions`                      | 768                  | 支援 768／1024，查詢只使用相同維度與 profile 的片段 |
| `Knowledge.Embedding.InputFormat`                     | `plain`              | BGE-M3 使用 plain；Qwen 查詢使用 qwen-query，文件保持原文 |
| `Knowledge.Embedding.QueryInstruction`                | 英文檢索指令         | qwen-query 查詢前綴；更改後需重新索引 |
| `Knowledge.Embedding.Revision`                        | 空字串               | 同模型名稱更新權重時填寫新版本，避免混用舊索引 |
| `Knowledge.Embedding.TimeoutSeconds`                  | 60                   | 每次向量請求的時間上限                        |
| `Knowledge.Embedding.MaxDailyRequests`                | 2000                 | 每人每日索引與查詢向量呼叫上限，按 UTC 日重設 |
| `Knowledge.Indexing.MaxCollections`                   | 30                   | 每人知識庫上限                                |
| `Knowledge.Indexing.MaxDocumentsPerCollection`        | 100                  | 每個知識庫文件上限                            |
| `Knowledge.Indexing.ChunkCharacters` / `ChunkOverlap` | 600 / 80             | 切段與重疊字元，保護 UTF-16 字元邊界          |
| `Knowledge.Retrieval.UseNativeVector`                 | true                 | 可用時使用 SQL 原生精確 cosine 距離           |
| `Knowledge.Retrieval.PortableCandidateLimit`          | 2000                 | 無原生向量時的授權候選上限；超限需縮小範圍    |
| `Knowledge.Retrieval.TopK` / `ContextCharacters`      | 6 / 5000             | 最多片段數與帶入對話的來源文字上限            |

OCR 與一般文字生成共用核准模型、群組政策、日生成配額與實際 token 記錄。向量呼叫另計次數上限，不會占用日生成次數。失敗的生成保留狀態供用量及問題排查；記錄不保存帳密。

本地 BGE-M3／Qwen 的設定、查詢格式、硬體取捨與可重現比較指令見 [模型比較](EMBEDDING_MODELS.md)。切換 provider、模型、維度、查詢指令或 Revision 後，重新索引目標知識庫；總覽的「需重新索引」統計可協助檢查。

變更向量模型後需重新索引；不同 profile 不混用。Google 官方目前提供獨立 embeddings API，Gemma 可搭配後端檢索結果；不要假設 Gemma 已支援 Google File Search。[Embeddings](https://ai.google.dev/gemini-api/docs/embeddings)、[File Search](https://ai.google.dev/gemini-api/docs/file-search)。

## 背景任務的可靠性

「背景任務」頁列出本人最近 100 筆任務，可篩選處理中、需要處理與已完成。頁面顯示真實階段與完成單位；未取得總量時使用不定進度，不顯示估計百分比。離開頁面不會停止工作。

`operations.BackgroundJobs` 是 durable queue。Worker 原子取得租約、每兩秒續約／檢查取消，checkpoint 驗證租約 token 與未取消狀態後才提交資料。程序中止後，租約到期可由下一個 worker 接手；失去租約的 worker 無法提交舊結果。同一來源的 active key 唯一，避免重複排程。失敗／取消後最多六次處理；重試先重新檢查來源權限。

此可靠佇列用於文件辨識與索引。聊天有獨立的 executor 租約，避免另一個實例誤判正在生成的回答；其排程仍在記憶體，每個 IIS app 使用一個 worker，完整限制與升級流程見 [IIS 部署](../deploy/iis/README.md)。
