# 本地向量與 RTX 5080 的建議路徑

以 **RTX 5080 16GB VRAM + 64GB RAM**，優先做「本地 embedding → MSSQL 向量／關鍵字檢索 → 少量授權來源 → Qwen 回答」。大量資料放在索引中，每次只送檢索出的片段；不要把整個公文／校務庫塞進聊天 Context。

## 起步配置與測量

建議從 `qwen3:8b`、8,192 Context、2,048 輸出 token、預設不思考、單次生成開始。Ollama 目前列出的 8B 檔案約 5.2GB、14B 約 9.3GB、30B 約 19GB、32B 約 20GB；**下載大小不是執行時 VRAM**。因此先測 8B，再測 14B；30B／32B 不適合作為這張卡的速度基準。這是依容量的工程判斷，實際仍需量化版本、KV cache 與你的文件測試。[Ollama Qwen3 模型清單](https://ollama.com/library/qwen3)

Context 與並行數會增加 KV 記憶體。先設定 Ollama `OLLAMA_NUM_PARALLEL=1`、`OLLAMA_CONTEXT_LENGTH=8192`，在實際啟動 Ollama 的服務／帳號設定後重啟；AI Nexus profile 的 Context 也設 8192。64GB RAM 能容納更大模型的 CPU 部分，但不能取代 VRAM 的速度。用 GPU 主機上的 `ollama ps` 確認 GPU／CPU 分配，並記錄冷啟動與暖機後的速度。[Ollama FAQ](https://docs.ollama.com/faq)、[Context 與記憶體](https://docs.ollama.com/context-length)

若實測 KV cache 佔用偏高，再測 Flash Attention 與 `OLLAMA_KV_CACHE_TYPE=q8_0`；先確認後端／GPU 支援並比較檢索回答品質，不預設開 q4 cache。Ollama 的 cache 量化是全域設定，也會影響其他模型，不能把權重量化與 KV cache 量化當成同一件事。[Ollama KV cache 說明](https://docs.ollama.com/faq#how-can-i-set-the-quantization-type-for-the-k-v-cache)

Embedding 現在建議把 **BGE-M3（1024 維）與 Qwen3-Embedding-0.6B（768／1024 維）並列比較**。BGE 可與 Qwen 回答模型搭配，不需要相同品牌。先前 Qwen 建議著重小型 Ollama 部署與既有 768 維相容性，不是繁中檢索品質的定論；本版已新增 BGE 1024 欄位與 Qwen query-only 前處理／版本指紋。規格、取捨與可重複驗收工具見 [EMBEDDING_MODELS](EMBEDDING_MODELS.md)。

在 GPU 主機下載模型後，執行只使用合成資料的探測：

```powershell
ollama pull qwen3:8b
ollama pull qwen3-embedding:0.6b
pwsh -NoProfile -File scripts/Test-LocalAI.ps1 `
  -Endpoint 'http://localhost:11434/'
ollama ps
```

探測會檢查已安裝模型、768 維回應、短生成是否完成、載入時間與 tokens/sec；不會呼叫 Google，不會送私人公文，亦不代表多人實際吞吐量。先用 20–50 題繁體中文公文／校務問題，測 recall、頁碼引用、首字延遲、輸出速度及尖峰 VRAM。再決定 14B、16K Context 或 reranker 是否值得。8B 與小 embedding 可能同時放入卡中，但不是保證；索引排離峰、限制並行，可避免模型來回卸載。

## AI Nexus 設定

將一般設定的 `Inference.Provider` 改 `ollama`，保留或調整 `Inference.Providers.Ollama` 的 endpoint、default profile。另將 `Knowledge.Embedding.Provider` 改 `ollama`、`Model` 改 `qwen3-embedding:0.6b`、`Dimensions` 保持 768、`InputFormat=qwen-query`；或選 BGE-M3、1024、plain。兩個設定都改才是完全本地。也可先 `Embedding.Provider=none` 用關鍵字，等本地模型可連後切換；不會自動改你目前已成功使用的 Google 設定。

模型主機不同於 IIS 時，Ollama endpoint 改為 GPU 主機 LAN 位址。Ollama API 只開給需要的應用主機，避免以無驗證 API 對整個網路開放。文字 `qwen3:8b` profile 的 `SupportsImages=false`；要分析圖片另選 vision 模型並測 OCR／圖片推論的額外 VRAM，不能只把這個旗標改 true。

更換 embedding 後，**全部相關文件重新索引**。同維度不同模型的向量仍不是同一個空間；現行 profile 會阻擋混用，新的索引須完成才可檢索。本版 migration 已新增 1024 維欄位；仍須先套用 migration、設定 profile 並重建，不能只改 JSON。

## 接著值得製作的功能（建議優先順序）

| 優先 | 功能               | 具體成果                                                                                     |
| ---- | ------------------ | -------------------------------------------------------------------------------------------- |
| 1    | 本地檢索驗收集     | 繁中問題／預期來源／版本／頁碼；比較 keyword、vector 與混合結果，資料不離開公司              |
| 2    | 公文增量索引       | 來源 revision／checksum／刪除 tombstone、斷點續作、只索引變更內容；沿用來源 ACL view         |
| 3    | 混合檢索與去重     | 精確文號、日期、姓名等走 keyword／metadata；語意找相關段落，再合併排名                       |
| 4    | ACL 與撤權同步     | 檢索前過濾、送模型前再驗來源授權；停用／機密／代理期限同步，避免向量索引成為旁路             |
| 5    | 索引版本與切換     | embedding profile／chunker／來源 revision 可追蹤；新索引完成後切換、可回復，失敗不破壞舊索引 |
| 6    | 小 reranker        | 在有限授權候選內重排，先測 0.6B／CPU 或離峰；依實測品質決定是否佔 GPU                        |
| 7    | 本地 OCR 工作池    | PDF 版面、表格、頁碼、批次 OCR；與聊天排程分離，限制 GPU 競爭                                |
| 8    | 來源診斷與容量面板 | 文件／chunk 數、失敗原因、索引進度、權限過期、GPU 工作排隊與可追溯引用                       |

MSSQL 2025 先沿用已驗證的精確 cosine 與 metadata／ACL 條件。不要只因有 VECTOR 就立即導入 ANN；來源資料量與召回率確認後，再依實際 SQL 版本能力選索引方案。現有實作、原生向量探測與權限邊界見 [VECTOR_ARCHITECTURE](VECTOR_ARCHITECTURE.md)。校務先做規章／公開單位資料；學生敏感資料必須有逐筆權限與經核准的檢索問題集。
