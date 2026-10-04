# 知識庫、文件閱讀與背景處理

從工作台「知識庫」建立資料來源，直接上傳 PDF、Word、圖片或 UTF-8 文字。新知識庫預設私有；擁有者可從名稱右側選單授權具名使用者閱讀／編輯，或授權功能群組閱讀。成員必須先登入過 AI Nexus。群組成員不會因閱讀授權取得編輯能力。

上傳後立即可以開啟原始文件。PDF 支援頁碼、縮放、原始頁面／擷取文字切換；文字搜尋會列出符合頁碼，引用可直接開啟指定頁面。每個原檔、頁面、搜尋與任務摘要 API 都重新驗證權限；只有發起任務且仍有文件編輯權限的使用者能停止／重試。

## 從文件到回答

1. 原檔保存於 `attachments.Attachments`，文件以 `ResourceAttachments` 保留引用，避免草稿清理回收來源。
2. PDF 原生文字以 PdfPig 逐頁擷取。沒有足夠可讀文字的掃描頁，以及上傳圖片，由核准的圖片模型辨識；輸出截斷會報錯，不將不完整頁面當成成功。
3. 每頁完成即保存 checkpoint。OCR 文字標記為需要核對，原檔始終是確認依據。無法取得頁面圖片時，請改上傳頁面圖片；本版不會臆造缺失文字。
4. 知識文件切成 600 字元、80 字元重疊片段；逐段建立向量。只有全部完成的文件才進入搜尋。
5. 聊天輸入框「來源」選取最多三個知識庫，選取即保存。提交前先檢索有權限的來源，再將引用資料與當次生成參數一起保存。
6. 回答下方的來源卡片可一鍵開啟原始頁碼。原文是參考資料，系統指令明確禁止執行來源內容的指令。使用者仍應核對回答與原始文件。

一般聊天掃描 PDF 也自動建立私有閱讀任務；辨識完成前暫停送出。圖片聊天使用模型原生圖片能力。移除未送出的草稿附件時，其私有閱讀器與背景任務一併清理；已連到對話、知識庫或專案的附件依引用保留。

刪除知識來源會清除頁面、片段與無其他引用的原檔；對話回答及當時引用摘要保留，失效來源連結回應 404。刪除整個知識庫也移除對話的選取關聯。重新索引會沿用已完成的頁面與相同向量 profile 的片段；模型或維度變更則重建索引。

## 設定

一般設定放 `.local/config/appsettings.Local.json`；Google key 共用 `.local/secrets/appsettings.Secrets.json` 的 `Inference.GoogleApiKey`，不要新增前端 key。

| 參數                                         | 預設                 | 用途                                          |
| -------------------------------------------- | -------------------- | --------------------------------------------- |
| `Knowledge.EmbeddingProvider`                | `google`             | `google`、`ollama`；`none` 明確改用關鍵字搜尋 |
| `Knowledge.EmbeddingModel`                   | `gemini-embedding-2` | 向量模型，與聊天 Gemma 分開                   |
| `Knowledge.Dimensions`                       | 768                  | 本版固定 768，與原生 SQL 欄位一致             |
| `Knowledge.UseNativeVector`                  | true                 | 可用時使用 SQL 原生精確 cosine 距離           |
| `Knowledge.MaxDailyEmbeddingRequests`        | 2000                 | 每人每日索引與查詢向量呼叫上限，按 UTC 日重設 |
| `Knowledge.PortableCandidateLimit`           | 2000                 | 無原生向量時的授權候選上限；超限需縮小範圍    |
| `Knowledge.MaxCollections`                   | 30                   | 每人知識庫上限                                |
| `Knowledge.MaxDocumentsPerCollection`        | 100                  | 每個知識庫文件上限                            |
| `Knowledge.ChunkCharacters` / `ChunkOverlap` | 600 / 80             | 切段與重疊字元，保護 UTF-16 字元邊界          |
| `Knowledge.TopK` / `ContextCharacters`       | 6 / 5000             | 最多片段數與帶入對話的來源文字上限            |

OCR 與一般文字生成共用核准模型、群組政策、日生成配額與實際 token 記錄。向量呼叫另計次數上限，不會占用日生成次數。失敗的生成保留狀態供用量及問題排查；記錄不保存帳密。

變更向量模型後需重新索引；不同 profile 不混用。Google 官方目前提供獨立 embeddings API，Gemma 可搭配後端檢索結果；不要假設 Gemma 已支援 Google File Search。[Embeddings](https://ai.google.dev/gemini-api/docs/embeddings)、[File Search](https://ai.google.dev/gemini-api/docs/file-search)。

## 背景任務的可靠性

「背景任務」列出本人最近 100 筆任務，可篩選處理中、需要處理與已完成。頁面顯示真實階段與完成單位；未取得總量時使用不定進度，不顯示估計百分比。離開頁面不會停止工作。

`operations.BackgroundJobs` 是 durable queue。Worker 原子取得租約、每兩秒續約／檢查取消，checkpoint 驗證租約 token 與未取消狀態後才提交資料。程序中止後，租約到期可由下一個 worker 接手；失去租約的 worker 無法提交舊結果。同一來源的 active key 唯一，避免重複排程。失敗／取消後最多六次處理；重試先重新檢查來源權限。

此可靠佇列用於文件辨識與索引。聊天 SSE 排程仍採單一 host 限制，請遵守 IIS 部署文件；不要因為加入背景租約就增加聊天 worker 數量。
