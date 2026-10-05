# 文件與圖片分析

在輸入框旁選擇「加入文件或圖片」，也可拖放檔案或貼上剪貼簿圖片。輸入模型要處理的任務再送出。附件卡片提供名稱、大小、分析方式、預覽及移除。

| 類型                                                    | 模型收到的內容                                                              |
| ------------------------------------------------------- | --------------------------------------------------------------------------- |
| PNG、JPEG、WebP                                         | 原始圖片，Google `inlineData`／Ollama `images`；模型需啟用 `SupportsImages` |
| PDF                                                     | PdfPig 逐頁抽取原生文字，掃描頁另經背景 OCR；可在閱讀器核對原始頁面         |
| Word `.docx`                                            | 主文件段落及表格文字，不讀巨集、外部連結或內嵌圖片                          |
| UTF-8 文字、Markdown、CSV、JSON、log、XML、YAML、程式碼 | 文件內容以使用者訊息附文傳送                                                |

預設每檔 4 MB，每則最多 4 個、總共 8 MB，每位使用者 64 MB。PDF 最多 40 頁，抽取文字最多 64,000 字元；超限要求拆分，不會悄悄截斷。加密 PDF 需先解密；掃描 PDF 自動進入辨識任務，無法抽出頁面圖片時提示上傳頁面圖片。辨識中不能送出提問，完成後可在閱讀器核對原文。文件計入 Context 預算；圖片每張保守預估 4,096 tokens，此數字不是實際計費用量。Gemma 範本啟用圖片、32,768 Context，輸出預留 2,048；其他 profile 的能力需明確設定。

## 參數

一般設定放在 `.local/config/appsettings.Local.json`，不需新增密碼。環境變數可用 `Attachments__MaxFileBytes` 等名稱覆寫。

| 參數                                                           | 預設值                 | 用途                             |
| -------------------------------------------------------------- | ---------------------- | -------------------------------- |
| `Attachments.MaxFileBytes`                                     | 4194304                | 單檔大小                         |
| `Attachments.MaxFilesPerMessage`                               | 4                      | 單則附件數                       |
| `Attachments.MaxMessageBytes`                                  | 8388608                | 單則附件總大小                   |
| `Attachments.MaxOwnerBytes`                                    | 67108864               | 個人儲存配額                     |
| `Attachments.MaxExtractedCharacters`                           | 64000                  | 文件文字上限                     |
| `Attachments.MaxPdfPages`                                      | 40                     | PDF 頁數上限                     |
| `Attachments.ImageTokenEstimate`                               | 4096                   | 圖片 Context 預估                |
| `Attachments.DraftRetentionDays`                               | 14                     | 未送出附件的回收期限（1–365 天） |
| `Inference.Providers.<provider>.Models.<alias>.SupportsImages` | false，Gemma 範本 true | 模型圖片能力                     |

Host request body 上限 10 MB；一般 JSON 操作為 64 KB。提問／Context 依 `MaxInputCharacters × 6 + 8192` 放寬（最低 64 KB），範本為 `12000 × 6 + 8192`，以容納 JSON 跳脫的中文字元。附件端點最多 9 MB（含 multipart overhead），文字備份匯入最多 8 MB。調高附件限制需同步檢查 host 與 IIS request filtering。

## 保存與權限

`attachments.Attachments` 保存 owner、原始檔、抽取文字與 metadata；`MessageAttachments` 將檔案連到提問，供分支、重新生成及副本共用。原始檔不在 `wwwroot`；個人附件由登入、可用工作功能及 owner 檢查，共用來源則透過文件 API 的知識庫授權檢查。檔名不成為磁碟路徑；圖片會檢查格式標記，不接受 SVG。

歷史只載入 metadata；Context 預覽只讀文字及圖片預估成本。生成完成分支裁切後，才載入仍需要的圖片資料。附件關聯、提問與 run 在同一筆 transaction 建立，檢查失敗不會留下孤立訊息。

未送出的新附件可移除；成功送出、加入知識庫／專案或從檔案庫上傳後，`InLibrary` 保存原檔。刪除對話只移除訊息引用；原檔可在檔案庫再次使用。從輸入框移除已保存的檔案不會刪除原檔。要釋放空間，先移除對話、知識庫、專案與分享引用，再明確從檔案庫刪除；伺服器重新檢查引用，衝突回應 409。訊息本身仍採 soft-delete。未保存且未送出的附件超過 `DraftRetentionDays`（預設 14 天）後，在該使用者下次上傳時清理；檔案庫與任何資源仍引用的附件不在此規則內。清除瀏覽器草稿不等同即時伺服器刪檔。資料庫備份需包含 attachments schema。JSON **文字備份**保存分支、指令、標籤及附件名稱，**不含原始檔**，匯入後須重新上傳附件；「建立對話副本」完整保留附件關聯。操作與權限見 [檔案庫](FILES.md)。

Google key 僅存在後端。使用 Google 時，本次需要的文字／圖片會傳送到 Google API。格式參考：[Gemma 圖片能力](https://ai.google.dev/gemma/docs/core/gemma_on_gemini_api#image-understanding)、[Google 圖片請求](https://ai.google.dev/gemini-api/docs/image-understanding)、[Ollama Chat API](https://docs.ollama.com/api/chat)、[PdfPig](https://github.com/UglyToad/PdfPig)。
