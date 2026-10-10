# 地端 AI 與向量化

已安裝並測試成功的 Ollama bge-m3 可直接作為本版預設：Knowledge.Embedding.Provider=ollama、Model=bge-m3、Dimensions=1024、InputFormat=plain。聊天仍使用自己的核准模型；完全地端須同時停用 Inference.Providers.Google.Enabled，選取可用的 Ollama 路由。

## 啟用與探測

先套用 [資料庫升級](DATABASE.md)，重啟 API，再重新上傳資料。管理 → 知識檢索顯示 SQL 原生向量、全文元件、1028 斷詞器及端點，按「探測模型」使用合成資料檢查實際 batch 數量／維度／rerank 格式；會計入操作人的模型呼叫配額。

```powershell
./tooling/embeddings/Test-LocalAI.ps1 -Endpoint 'http://localhost:11434/' `
  -EmbeddingModel bge-m3 -Dimensions 1024
./scripts/Test-Environment.ps1 -SqlOnly
ollama ps
```

Test-LocalAI 同時檢查預設聊天模型 qwen3:8b、批次 embedding、短生成、載入與輸出速度；聊天不同時加 -ChatModel。資料僅為合成測試，不送私人原文，不代表多人吞吐或檢索品質。主機指令 `verify connections`／`verify deployment` 也檢查真實合成 embedding 及已啟用 rerank，會有模型運算／載入成本。

Ollama 主機與 IIS 分開時，Inference.Providers.Ollama.Endpoint 改成可連的主機位址；只移 embedding 可填 Knowledge.Embedding.Endpoint，空值沿用 Ollama 端點。模型服務位址由伺服器設定，限制可連的應用主機。none 會強制 SQL keyword，需要全文元件，沒有舊 substring／JSON fallback。

## GPU 與並行

16 GB VRAM／64 GB RAM 可作為測量環境，模型檔案大小不能推算實際峰值。先用單一聊天生成、8192 Context、適當輸出上限，量冷啟動、暖機 p95、GPU／CPU 分配、VRAM 與引用品質；再一次增加一項（較大聊天模型、較長 Context、OCR 或 rerank）。[Ollama Context 說明](https://docs.ollama.com/context-length)

預設 embedding BatchSize=16、MaxConcurrentBatches=1，背景批次在聊天生成或排隊時等待，已發出的批次不能搶占；每日配額及 durable queue 跨程序保存，GPU semaphore／查詢快取屬每個 API 程序。多 IIS instance 的整體 GPU 容量由部署端協調。可設定 Ollama OLLAMA_NUM_PARALLEL=1，再依實測調整；調整服務環境變數後須重啟 Ollama。[Ollama 並行說明](https://docs.ollama.com/faq)

聊天、embedding、rerank 不保證同時留在 VRAM。可將 embedding 移到另一主機，rerank 也可使用獨立 TEI／llama.cpp 端點；CPU offload 的線上延遲仍需測量。[重排部署指令](EMBEDDING_MODELS.md)提供兩種範例，預設不啟用，先完成四模式評測再決定配置。

## 更新與驗收

模型／維度／Revision／切段改變後建立 building，管理員重建、100% 覆蓋後切換；AutoActivate 預設 false，退役向量預設保留 7 日。保留原始頁面重建不重做 OCR；資料編輯以完整輸入 hash 重用向量。管理動作有稽核，查詢只讀 active profile。

品質 → 檢索評測用固定的真實驗收集比較四種模式與頁碼相關性，報告不含來源或問題原文。資料品質、授權、Recall／nDCG 與 p95 優先驗證；目前 SQL Server 2025 使用精確 cosine，ANN preview 尚未啟用。公文／校務來源仍需先通過來源 ACL 後匯入 collection，共用同一管線。[架構](VECTOR_ARCHITECTURE.md)、[品質評測](QUALITY.md)
