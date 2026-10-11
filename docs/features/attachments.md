# 文件與圖片分析

在輸入框旁選「加入文件或圖片」，也可以拖放檔案或貼上剪貼簿圖片，輸入要模型處理的任務後送出。附件卡片提供名稱、大小、分析方式、預覽與移除。原檔的保存、容量與刪除見 [附件保存](attachment-storage.md)；跨對話重用見 [檔案庫](files.md)。

## 支援的格式

| 類型 | 模型收到的內容 |
| --- | --- |
| PNG、JPEG、WebP | 原始圖片（Google `inlineData`／Ollama `images`）；模型需支援圖片 |
| PDF | PdfPig 逐頁抽取原生文字，掃描頁另經背景 OCR；可在閱讀器核對原頁 |
| Word `.docx` | 段落與表格文字；不讀巨集、外部連結或內嵌圖片 |
| Excel `.xlsx` | 工作表名稱、儲存格位置、文字、布林與已保存的公式結果；不計算公式 |
| PowerPoint `.pptx` | 依投影片編號擷取段落與表格文字；圖片、圖表與備註不做 OCR |
| UTF-8 文字 | Markdown、CSV／TSV、JSON、log、XML、YAML、TOML、INI、HTML／CSS、程式碼；全部當純文字，不執行 |

- 實際的副檔名清單在 `DocumentExtractor.Extensions`，前端所有入口共用 `/api/v1/attachments/policy` 回傳的 accept 清單。
- 不支援 XLSM／DOCM／PPTM、舊二進位 XLS／DOC／PPT、加密 Office、壓縮包與 SVG；請先轉成不含巨集的現代格式或 PDF。偽裝副檔名的巨集也會拒絕，解析失敗回 400。
- 加密 PDF 要先解密。掃描 PDF 自動進入辨識任務，辨識中不能送出提問；無法抽出頁面圖片時提示改傳頁面圖片。

## Office 解析

採被動 OOXML 解析（專案自有的 bounded XML／ZIP reader，沒有 Office 依賴）：不啟動 Office、不執行巨集、公式、嵌入物件或外部連線。XML 禁用 DTD／resolver；ZIP 最多 1,500 項、解壓總量 32 MiB、每項 8 MiB。XLSX 最多 128 個工作表、每表 100,000 格；PPTX 最多 200 頁。公式只用 cached value，日期保留原始數值。參考 [OOXML 公式與保存值](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/working-with-formulas)。

## 限制與設定

`Attachments` 區段的鍵與預設值在 `backend/src/AiNexus.Features/Attachments/AttachmentOptions.cs` 與 `appsettings.json`，主要預設：

| 項目 | 預設 |
| --- | --- |
| 單檔／單則總量／單則檔數 | 4 MiB／8 MiB／4 個 |
| PDF 頁數／抽取文字 | 40 頁／64,000 字元，超過要求拆分，不會悄悄截斷 |
| 每人原檔總容量 | 5 GB（十進位，介面同時顯示精確 bytes） |
| 圖片 Context 預估 | 每張 4,096 tokens（不是實際計費用量） |
| 未送出草稿的回收 | 14 天，每 60 分鐘清理一次 |

- 文件內容計入 Context 預算。模型是否接受圖片由 `Inference.Providers.<provider>.Models.<alias>.SupportsImages` 決定（Ollama 未設定時依 `/api/show` 判斷）。
- Host request body 上限 10 MB；一般 JSON 64 KB；提問與 Context 依 `MaxInputCharacters × 6 + 8192` 放寬（最低 64 KB），容納 JSON 跳脫後的中文。附件端點最多 9 MB（含 multipart），文字備份匯入最多 8 MB。調高附件限制時要一併檢查 host 與 IIS request filtering。

## 隱私

Google key 只在後端。使用 Google 模型時，本次需要的文字與圖片會送到 Google API。參考：[Gemma 圖片能力](https://ai.google.dev/gemma/docs/core/gemma_on_gemini_api#image-understanding)、[Google 圖片請求](https://ai.google.dev/gemini-api/docs/image-understanding)、[Ollama Chat API](https://docs.ollama.com/api/chat)、[PdfPig](https://github.com/UglyToad/PdfPig)。
