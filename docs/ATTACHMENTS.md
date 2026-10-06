# 文件與圖片分析

在輸入框旁選擇「加入文件或圖片」，也可拖放檔案或貼上剪貼簿圖片。輸入模型要處理的任務再送出。附件卡片提供名稱、大小、分析方式、預覽及移除。

| 類型                                                    | 模型收到的內容                                                              |
| ------------------------------------------------------- | --------------------------------------------------------------------------- |
| PNG、JPEG、WebP                                         | 原始圖片，Google `inlineData`／Ollama `images`；模型需啟用 `SupportsImages` |
| PDF                                                     | PdfPig 逐頁抽取原生文字，掃描頁另經背景 OCR；可在閱讀器核對原始頁面         |
| Word `.docx`                                            | 主文件段落及表格文字，不讀巨集、外部連結或內嵌圖片                          |
| Excel `.xlsx` | 工作表名稱、儲存格位置、文字、布林及已保存的公式結果；不計算公式 |
| PowerPoint `.pptx` | 依投影片編號擷取原生段落／表格文字；圖片、圖表及備註不做 OCR |
| UTF-8 文字、Markdown、CSV、JSON、log、XML、YAML、程式碼 | 文件內容以使用者訊息附文傳送                                                |

預設每檔 4 MiB（4,194,304 bytes），每則最多 4 個、總共 8 MiB；每位使用者的原檔總容量預設 **5 GB（5,000,000,000 bytes）**。介面容量採十進位 KB／MB／GB 並提供精確 bytes。PDF 最多 40 頁，抽取文字最多 64,000 字元；超限要求拆分，不會悄悄截斷。加密 PDF 需先解密；掃描 PDF 自動進入辨識任務，無法抽出頁面圖片時提示上傳頁面圖片。辨識中不能送出提問，完成後可在閱讀器核對原文。文件計入 Context 預算；圖片每張保守預估 4,096 tokens，此數字不是實際計費用量。Gemma 範本啟用圖片、32,768 Context，輸出預留 2,048；其他 profile 的能力需明確設定。

## 參數

文字格式另包含 TSV、TOML、INI、HTML／CSS、TSX／JSX、MJS／CJS，全部當 UTF-8 純文字，不執行內容。accept 清單由 `/attachments/policy` 提供，所有入口共用。

Office 採被動 OOXML，不啟動 Office、不執行巨集、公式、嵌入物件或外部連線。XML 禁 DTD／resolver；ZIP 最多 1,500 項、32 MiB 解壓總量、每項 8 MiB，另受擷取字數限制。XLSX 最多 128 個工作表、每表 100,000 個儲存格；PPTX 最多 200 頁。公式只用 cached value，無 cache 明確標示；日期保留原始數值，不套用 Excel 顯示樣式。圖片／圖表不屬於原生文字擷取。

不支援 XLSM／DOCM／PPTM、XLS／DOC／PPT 舊二進位、加密 Office、任意壓縮包及 SVG；需要額外安全解析器／沙箱，請先轉成不含巨集的現代格式或 PDF。偽裝副檔名的巨集也拒絕，解析失敗回 400，超限要求拆分。抽取採專案自有 bounded XML／ZIP reader，無新 Office 依賴。公式限制參考 [Microsoft OOXML 公式與保存值](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/working-with-formulas)。

一般設定放在 `.local/config/appsettings.Local.json`，不需新增密碼。環境變數可用 `Attachments__MaxFileBytes` 等名稱覆寫。

| 參數                                                           | 預設值                                         | 用途                             |
| -------------------------------------------------------------- | ---------------------------------------------- | -------------------------------- |
| `Attachments.MaxFileBytes`                                     | 4194304                                        | 單檔大小                         |
| `Attachments.MaxFilesPerMessage`                               | 4                                              | 單則附件數                       |
| `Attachments.MaxMessageBytes`                                  | 8388608                                        | 單則附件總大小                   |
| `Attachments.StoragePath`                                      | 正式 `D:\CoreProject\AiNexus\data\attachments` | 站外原檔目錄，必須為絕對路徑     |
| `Attachments.DefaultOwnerLimitBytes`                           | 5000000000                                     | 未設定個人或群組上限時的預設容量 |
| `Attachments.MaxExtractedCharacters`                           | 64000                                          | 文件文字上限                     |
| `Attachments.MaxPdfPages`                                      | 40                                             | PDF 頁數上限                     |
| `Attachments.ImageTokenEstimate`                               | 4096                                           | 圖片 Context 預估                |
| `Attachments.DraftRetentionDays`                               | 14                                             | 未送出附件的回收期限（1–365 天） |
| `Attachments.CleanupIntervalMinutes`                           | 60                                             | 定期草稿回收與刪檔重試週期       |
| `Inference.Providers.<provider>.Models.<alias>.SupportsImages` | false，Gemma 範本 true                         | 模型圖片能力                     |

Host request body 上限 10 MB；一般 JSON 操作為 64 KB。提問／Context 依 `MaxInputCharacters × 6 + 8192` 放寬（最低 64 KB），範本為 `12000 × 6 + 8192`，以容納 JSON 跳脫的中文字元。附件端點最多 9 MB（含 multipart overhead），文字備份匯入最多 8 MB。調高附件限制需同步檢查 host 與 IIS request filtering。

## 保存與權限

`attachments.Attachments` 保存 owner、metadata、StorageKey、StorageState、抽取文字與引用所需資料，不保存原始檔 bytes。`MessageAttachments`／`ResourceAttachments` 連接提問、知識庫、專案與分享；同一原檔只存一份、只計一次容量。重新上傳相同內容仍是新的原檔，不依內容 hash 跨使用者共用。

`IAttachmentStorage` 將 domain 與實體儲存分開。正式路徑由網站旁的 `config/appsettings.Production.json` 設定，IIS 可用 `Attachments__StoragePath` 覆寫；Development 未指定時使用工作區 `.local/data/attachments`。啟動拒絕相對路徑或網站目錄內的路徑。原檔使用不可猜測的 32 字元 StorageKey，分層保存為 `<前2碼>/<次2碼>/<StorageKey>.blob`；原始檔名只作 metadata。磁碟目錄不映射 IIS 虛擬目錄、不提供靜態 URL。下載須通過 owner、知識／專案 ACL 或有效具名分享，才以 stream 回傳。圖片仍檢查格式標記，不接受 SVG。

上傳先驗格式與實際長度，在 transaction 以使用者 SQL row lock 檢查容量並建立 `pending` 預約，commit 後才寫入 `.upload`，flush 並原子 rename 成 `.blob`，最後轉為 `ready`。這個鎖可協調共用 SQL 的不同程序，檔案 IO 不持有 SQL transaction。失敗以 `deleting` outbox 清理；若預約已被回收才完成寫檔，重新建立清理記錄與容量，避免早一批回收器刪掉晚到寫入的清理狀態。若程序在寫檔與 SQL 確認之間中斷，定期孤兒掃描接手。只讓 `ready` 檔案建立引用或下載；空間不足回應 413／`attachment_quota`。

容量以每筆原檔的 Size 加總，包含草稿、`pending` 預約及尚未刪除成功的 `deleting` 檔案，不加總引用、不加計衍生文字／索引。個人設定 `identity.Users.AttachmentLimitBytes` 優先於群組；未設定個人上限才取有效群組最低非空上限，再無設定則用網站預設。管理者調高個人上限可超過群組與預設。0 表示禁止新上傳；降低上限不刪既有檔案，剩餘容量最低為 0。個人設定、檔案庫與管理使用者介面顯示已用／上限／剩餘；API 為 `GET /attachments/storage` 與 `PUT /admin/users/{id}/storage`（皆加 `/api/v1`）。管理 API 以整數 bytes 接收 `limitBytes`，null 恢復繼承，變更保存稽核。

歷史只載入 metadata；Context 預覽只讀文字及圖片預估成本。生成完成分支裁切後，才載入仍需要的圖片資料。附件關聯、提問與 run 在同一筆 transaction 建立，檢查失敗不會留下孤立訊息。

未送出的新附件可移除；成功送出、加入知識庫／專案或從檔案庫上傳後，`InLibrary` 保存原檔。刪除對話只移除訊息引用；原檔可在檔案庫再次使用。從輸入框移除已保存的檔案不會刪除原檔。要釋放空間，先移除對話、知識庫、專案與分享引用，再明確從檔案庫刪除；伺服器在 transaction 與 owner lock 內重新檢查引用，衝突回應 409。訊息本身仍採 soft-delete。

`AttachmentCleanupWorker` 啟動時及每 60 分鐘回收草稿，無須等待使用者上傳；上傳也觸發一次回收。未保存、沒有 durable 引用的 `ready` 檔案 14 天後回收；私人閱讀器的衍生頁面／片段會一併移除，不會永久阻擋草稿回收。中斷的 `pending` 一小時後回收。每批最多 100 筆，以免清理長時間占用服務。定期 worker 另外串流掃描儲存區：只處理標準命名、最後寫入超過 24 小時且 SQL 無任何 metadata 的孤兒 `.blob`／`.upload`，每 100 個儲存識別批次核對。有效原檔與近期寫入保留，刪除失敗下次重試；此掃描不在每次上傳執行。清除瀏覽器草稿不等同即時伺服器刪檔。

實際刪檔採 durable outbox：先移除引用並將 metadata 標記 `deleting`，commit 後刪除 `.blob` 及 `.upload`，成功才刪 metadata、釋放容量。檔案不存在視為已刪；磁碟／權限失敗保留記錄及容量並記錄附件 ID，下一週期重試。維運應監控清理警告及磁碟剩餘量；每人上限不等於磁碟總容量。

完整備份必須包含 SQL 與同一時點的附件目錄，操作見 [備份與還原](BACKUP.md)。JSON **文字備份**保存分支、指令、標籤及附件名稱，**不含原始檔**，匯入後須重新上傳附件；「建立對話副本」完整保留附件關聯。資料庫透過增量 migration 升級，原檔不需搬移，操作見 [資料庫](DATABASE.md)。操作與權限見 [檔案庫](FILES.md)。

Google key 僅存在後端。使用 Google 時，本次需要的文字／圖片會傳送到 Google API。格式參考：[Gemma 圖片能力](https://ai.google.dev/gemma/docs/core/gemma_on_gemini_api#image-understanding)、[Google 圖片請求](https://ai.google.dev/gemini-api/docs/image-understanding)、[Ollama Chat API](https://docs.ollama.com/api/chat)、[PdfPig](https://github.com/UglyToad/PdfPig)。
