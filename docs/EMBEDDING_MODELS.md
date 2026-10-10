# Embedding、重排與驗收

預設 Ollama `bge-m3`、1024 維、plain，聊天模型獨立選擇。Ollama `/api/embed` 批次陣列與 truncate=false；目前只使用 dense 向量。混合檢索的另一通道是 SQL 全文，不是 BGE-M3 的 sparse／ColBERT。Qwen 可用 `qwen3-embedding:0.6b`、768 或 1024、qwen-query，查詢加入英文任務指令、文件保留 context header；回應維度須實際相符，不能自行截斷。[Ollama API](https://docs.ollama.com/api/embed)

Google provider 保留 batch embedding API，使用 Inference 的 Google key；none 不建立向量、強制全文。embedding 不隨聊天 provider 自動切換。完整預設及範圍見 [設定](CONFIGURATION.md)。Revision 可填固定模型 digest；同 tag 權重更新時同步變更，以觸發新 profile。InputFormat、QueryInstruction、Revision、維度及 chunker 版本／參數皆納入空間指紋。

## 重建與測量

先套用 [資料庫 migrations](DATABASE.md)，重新上傳資料建立新索引。往後模型設定變更產生 building profile；管理 → 知識檢索開始重建，覆蓋率 100% 後啟用，舊 profile 退役。共用一套切段布局，不提供舊 JSON 或舊 SQL 版本查詢。重建利用保存頁面及 hash 快取，無須重做 OCR。

從品質 → 檢索評測選最多三個授權知識庫、匯入 1–20 題驗收集，四模式執行 vector／keyword／hybrid／hybrid+rerank，計算 Recall@K、MRR、分級 nDCG@K、無來源拒答率與各階段 p50／p95。範例格式與報告定義見 [QUALITY](QUALITY.md)。應涵蓋文號、日期、單位、中文條文、同義詞、跨頁與無答案；以固定文件與設定比較，分開記錄冷啟動／快取效應。未啟用重排或降級的結果有明確模式，不能當成完整四方案比較。

`tooling/embeddings/Compare-Embeddings.ps1` 可做模型的獨立 dense cosine 比較：

```powershell
./tooling/embeddings/Compare-Embeddings.ps1 -ValidateOnly
./tooling/embeddings/Compare-Embeddings.ps1 -Endpoint 'http://localhost:11434/' -TopK 3
```

使用 `tooling/embeddings/profiles.json` 與合成 corpus，真實資料放 .local，以 -ProfilesPath／-CorpusPath 指定。舊工具 corpus 是 documents[{id,text}] + queries[{id,text,relevantDocumentIds}]，只比較固定片段的 embedding；品質頁 corpus 使用已入庫文件 UUID／頁碼，能測實際切段、FTS、ACL 與重排。兩者不宣稱是生成回答正確率。

## TEI 部署範例

先確認 GPU 的驅動、Docker GPU runtime、架構及映像相容性。下例選 TEI 官方列出的 bge-reranker-large；範例未在本機啟動。[TEI 官方部署與重排 API](https://github.com/huggingface/text-embeddings-inference/blob/main/README.md#using-re-rankers-models)

```powershell
docker run --gpus all -p 8081:80 -v tei-rerank-data:/data `
  ghcr.io/huggingface/text-embeddings-inference:cuda-1.9 `
  --model-id BAAI/bge-reranker-large
```

設定 Knowledge.Rerank 的 Provider=tei、Endpoint=http://localhost:8081、Model=bge-reranker-large。TEI model 由服務啟動參數固定，Model 用於用量識別；實作送 POST /rerank 的 query／texts／truncate=false／raw_scores=false，驗證全部候選的 index 與有限分數。

## llama.cpp 部署範例

準備實際支援的 cross-encoder reranker GGUF；不要使用普通聊天或 embedding GGUF 代替。下面模型路徑請換成自己的檔案。[llama.cpp server 重排介面](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md#post-reranking-rerank-documents-according-to-a-given-query)

```powershell
llama-server -m 'D:\Models\bge-reranker-v2-m3.gguf' `
  --host 127.0.0.1 --port 8082 --embedding --reranking --pooling rank
```

設定 Provider=openai-compatible、Endpoint=http://localhost:8082、Model=bge-reranker-v2-m3。POST /v1/rerank 送 query／documents／top_n，讀 results[index,relevance_score]。它是此重排協定的 adapter，不代表任意 OpenAI 聊天端點支援 rerank。連線探測以合成候選檢查真實格式。

兩種服務的**尖峰 VRAM、冷啟動、聊天競爭及延遲均需實測**，不能以模型檔案大小保證可同時載入。預設 Provider=none、TimeoutSeconds=10、MinScore=0、FailurePolicy=skip；啟用後先保持其他參數固定，測 rerank 是否改善 nDCG，再校準各模型門檻，避免提高拒答率卻降低有答案召回。

調參順序：確認擷取／頁碼 → 標註驗收集 → 切段 token 與表格 → 候選40／40 → RRF k60／權重1 → rerank30／門檻 → TopK6／每文件3／Context3500。每次改一項並保存指標，這些預設是起點，實際品質尚須使用你的資料驗收。GPU 分工與探測見 [本機 AI](LOCAL_AI.md)。
