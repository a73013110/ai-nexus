# BGE-M3 與 Qwen 的地端檢索評估

**BGE-M3 應列入這個專案的主要候選，與 Qwen3-Embedding-0.6B 在同一份繁中驗收集比較。** 先前優先提 Qwen 是考量現有 768 維欄位、小型 Ollama 部署與可調維度，不代表 BGE-M3 的檢索品質較差。Qwen 回答模型與 Qwen embedding 也是不同角色；可以使用 BGE-M3 找資料、Qwen3:8b 生成答案，沒有品牌必須一致的限制。

## 規格與適用性

| 項目 | BGE-M3 | Qwen3-Embedding-0.6B |
| --- | --- | --- |
| 輸出 | 1024 維 dense；原生另支援 sparse／ColBERT 多向量 | 至多 1024 維 dense，官方支援 32–1024 維的 MRL 輸出 |
| 長度與語言 | 8192 tokens、100+ 語言 | 32K tokens、100+ 語言，含程式語言 |
| 查詢前處理 | 不需要額外檢索指令 | 查詢加入任務指令，文件不加；官方建議多語任務的指令使用英文 |
| 本版設定 | `bge-m3`、1024、`plain` | `qwen3-embedding:0.6b`、768 或 1024、`qwen-query` |
| 我會優先驗證 | 公文／規章的語意與字詞混合檢索、繁中句子 | 語意／程式文件檢索、可調維度與現有 Ollama 部署 |

規格來源：[BAAI BGE-M3](https://huggingface.co/BAAI/bge-m3)、[Qwen3-Embedding-0.6B](https://huggingface.co/Qwen/Qwen3-Embedding-0.6B)。最後一列是工程評估方向，並不是你的資料已驗證的品質排名；模型卡的通用 benchmark 也不能直接等同繁中公文／校務效果。

本版 Ollama `/api/embed` 只取得單一 dense 向量。即使選了 BGE-M3，也**不會自動得到 sparse／ColBERT 檢索**。要使用完整 M3，之後需要 FlagEmbedding 服務與相應的字詞／多向量儲存和排名；不能只把 JSON 的 Model 改成 BGE 就宣稱已有混合檢索。BAAI 的建議流程是 hybrid retrieval 再 rerank。[BGE-M3 原生輸出與檢索建議](https://huggingface.co/BAAI/bge-m3#usage)

## 本版已支援的設定

先套用 migration，再選擇其中一個 embedding profile。一般設定範例：

```json
{
  "Knowledge": {
    "Embedding": {
      "Provider": "ollama",
      "Model": "bge-m3",
      "Dimensions": 1024,
      "InputFormat": "plain",
      "QueryInstruction": "Given a web search query, retrieve relevant passages that answer the query",
      "Revision": "",
      "TimeoutSeconds": 60,
      "MaxDailyRequests": 2000
    }
  }
}
```

Qwen 對應 `Model=qwen3-embedding:0.6b`、`Dimensions=768`、`InputFormat=qwen-query`；也可比較 1024 維。QueryInstruction 只用於 Qwen 的查詢，不加到文件。若變更任務指令或模型版本，需重建索引；Revision 可填 Ollama 模型 digest 的 64 字元 SHA256 部分，變更 tag 指向的模型時也一起更新。

profile 包含供應商、模型、維度，並以 hash 追蹤前處理／任務指令／Revision，避免向量空間混用。plain 且 Revision 留空的原 profile 保持相容；建議正式部署固定版本後明確填 Revision。

SQL Server 2025 migration 新增 `knowledge.Chunks.EmbeddingVector1024 VECTOR(1024)`，保留原 `EmbeddingVector VECTOR(768)`。後端選擇對應欄位，SQL 的欄位與維度限於程式內兩種值。其他 SQL 版本仍保存正規化 JSON 向量並使用有候選上限的可攜式 cosine。更換模型／維度／前處理後，從文件操作「重新索引」完整重建；未完成切換前，舊 profile 不會冒充新模型結果。本版還沒有同時服務多個 embedding profile 的平滑雙索引切換。

## 可重複的比較指令

需要 Python 3，工具僅使用標準函式庫及自己的 Ollama，不呼叫 Google，不安裝／刪除／卸載模型。請先在 GPU 主機安裝你要比較的模型：

```powershell
ollama pull bge-m3
ollama pull qwen3-embedding:0.6b

# 驗證資料與 profile，完全不送請求
pwsh -NoProfile -File scripts/Compare-Embeddings.ps1 -ValidateOnly

# 在 GPU 主機執行，或改 Endpoint 為該主機的內網位址
pwsh -NoProfile -File scripts/Compare-Embeddings.ps1 `
  -Endpoint 'http://localhost:11434/' -TopK 3
```

`tooling/embeddings/profiles.json` 提供 BGE 1024 plain 與 Qwen 768 qwen-query。要做同維度比較，可複製 profile 到 `.local` 將 Qwen 改 1024，使用 `-ProfilesPath`。範例資料只是 5 題合成流程示範，不是公司的真實規章，也不能用它宣布哪個模型最好。

自己的驗收集放 `.local/embeddings/corpus.json`，格式：

```json
{
  "documents": [
    { "id": "doc-001-p3", "text": "一段經核准的文件內容" }
  ],
  "queries": [
    { "id": "q-001", "text": "使用者會問的問題", "relevantDocumentIds": ["doc-001-p3"] }
  ]
}
```

```powershell
pwsh -NoProfile -File scripts/Compare-Embeddings.ps1 `
  -Endpoint 'http://GPU主機:11434/' `
  -CorpusPath '.local/embeddings/corpus.json' -TopK 5
```

文件最多 200 段、每段 1600 字；問題最多 100 題、每題 2000 字，relevant IDs 必須指向既有文件。本工具兩個模型使用相同片段、cosine、查詢與標準答案，分別套用官方建議的前處理，檢查實際回傳維度，不自行截斷 BGE。

報告在 `artifacts/embeddings`，含 Recall@K、MRR、各題前 K 文件 ID、每題延遲、所有請求的中位／P95 延遲、載入時間、模型 digest 與 corpus 指紋，便於核對是否用了同一份驗收集。它不保存原文、查詢或向量；report 仍包含文件 ID，依資料政策保存。延遲含網路和可能的冷啟動，不是純 GPU benchmark。`truncate=false` 避免靜默裁切，`keep_alive=5m` 保持短期模型快取，Ollama 是否支援 requested dimensions 以實際回傳為準。[Ollama embed API](https://docs.ollama.com/api/embed)

建議挑 20–50 個真實問題，分成公文文號／日期／單位、規章語意、繁簡／同義詞、程式文件及「無答案」題型；此工具的有答案 Recall/MRR 與人工無答案檢查分開做。另量測 `ollama ps` 的 GPU／CPU 分配、GPU 尖峰用量、暖機後速度、文件批次索引時間及真實引用準確度。這些數值目前尚未在你的 RTX 5080 實測。

## 16 GB GPU 的部署取捨

Ollama 目前 BGE-M3 檔案約 1.2 GB、Qwen embedding 0.6B 約 639 MB；**檔案大小不是執行時 VRAM，也不能據此保證 Qwen 更快**。[BGE Ollama](https://ollama.com/library/bge-m3)、[Qwen Ollama](https://ollama.com/library/qwen3-embedding:0.6b)

先以 Qwen3:8b 生成、8192 Context、2048 輸出、單次並行，加上其中一個小 embedding 做實測。embedding 與生成仍會爭 GPU；批次建索引排離峰，避免聊天中頻繁換載模型。64 GB RAM 可容納索引與 CPU 模型，但 CPU offload 的速度須實測。14B、較長 Context、OCR 與 reranker 不要一次全加；每次增加一項後量測暖機延遲及尖峰 VRAM。[Ollama 並行與記憶體](https://docs.ollama.com/faq)

若 GPU 競爭顯著，可將 BGE 的批次索引移到獨立 CPU／另一張 GPU embedding 服務，但要量測查詢延遲；不是為了解決 VRAM 就直接把線上查詢全部移 CPU。完整稀疏／多向量服務、跨模型雙索引、RRF／metadata 混合排名及小 reranker 是後續階段。公文與校務先維持逐筆來源 ACL，再談 ANN；學生敏感資料不可因向量化變成全員可查。[向量架構](VECTOR_ARCHITECTURE.md)、[本機 AI](LOCAL-AI.md)
