# 成果文件與段落工具

AI 回答旁的「儲存成果」可直接將回答建立為文件。在對話內選取同一則訊息的文字，即可改寫、摘要、翻譯或另存成果；操作上限為 8,000 字元，使用系統核准的模型並記錄實際用量。

成果頁提供閱讀、編輯、並排預覽與版本選單。Ctrl／⌘ S 儲存新版本；歷史版本保持不變，「將此版帶入編輯」會在儲存後建立另一個新版本。兩人同時編輯時，較舊版本的儲存會回傳 409，保留畫面上的文字供使用者合併。未儲存編輯離開前有提示。

擁有者可設定具名的讀者／編輯者或唯讀群組。檢視、編輯、版本清單與匯出均重新檢查共用 ACL。移除文件會清除全部版本內容。

## 匯出

- Markdown：保存原始 Markdown。
- Word：使用 Open XML SDK 建立 DOCX，保留中文、標題、清單、表格與程式區塊。
- PDF：使用隔離的無 JavaScript Chromium 頁面以 A4 排版，保留中文字；每次匯出只輸出目前選取的**已儲存版本**。

匯出不載入外部圖片或遠端資源，原始 HTML 視為文字。部署主機必須安裝 `Artifacts.Export.BrowserChannel` 指定的 Edge（`msedge`，預設）、Chrome（`chrome`），或部署 Playwright 對應的 `chromium`；也可用 `Exports.BrowserExecutablePath` 指定 Chromium 系瀏覽器執行檔的絕對路徑，設定後取代 `BrowserChannel`。另需安裝可涵蓋繁體中文的字型；Windows 使用 Microsoft JhengHei。無可用瀏覽器時 PDF 會顯示可處理的錯誤，Word／Markdown 仍可使用。

`Artifacts.Export.TimeoutSeconds` 預設 30；同時最多兩次 PDF 匯出。Render 完成後會再次檢查使用者權限，才傳回私人、不快取的檔案。

## 資料物件

`collaboration.Resources` 保存名稱、擁有者、ACL 所屬項目及刪除狀態。`artifacts.Artifacts` 保存最新版本號、可選的來源訊息／專案連結；`artifacts.ArtifactRevisions` 以 `(ArtifactId, Version)` 為主鍵，保存每次標題、內容、作者及時間。原始對話不因成果編輯而改變。
