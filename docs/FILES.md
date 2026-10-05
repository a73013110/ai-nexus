# 檔案庫與共用原檔

檔案庫是每個人的原檔清單；知識庫是有權限控制的檢索來源。知識庫、專案及成功送出的對話附件都會保存到上傳者的檔案庫。也可直接上傳到 `/files`，先保存素材再決定用途。共用知識庫的成員只在授權來源中閱讀文件，不會取得上傳者的整個檔案庫。

## 操作

- 搜尋名稱，依圖片／文件及對話／知識庫／專案／尚未引用篩選，切換卡片或清單。每頁 40 筆；附件配額維持同一個原檔計算。
- 點擊名稱開大型預覽，保留目前頁面、對話草稿、捲動與焦點。圖片預覽不自動啟動 OCR；PDF 有頁碼、縮放、搜尋及原文／文字切換。下載與獨立閱讀頁保留在精簡工具列。
- 聊天輸入框「從檔案庫加入」重用既有 attachment ID，套用原本的附件數／總大小／OCR／模型限制。
- 檔案卡片「加入知識庫」選擇有編輯權限的知識庫。確認文字說明成員可閱讀原檔；後端重新檢查權限，並建立獨立索引與引用。
- 刪除對話或知識來源保留原檔。仍被對話、知識庫、專案或分享引用時，不能刪除原檔；移除引用後明確刪檔才釋放配額。輸入框移除已保存原檔只移除這次草稿引用。

## 實作邊界

`attachments.Attachments.InLibrary` 區分已保存與未送出草稿；MessageAttachments／ResourceAttachments 分別連接歷史與知識／專案。原始檔只存一份，索引、頁面及 ACL 屬於各來源。新增來源可重用同一擁有者、同一不可變原檔的完成頁面；向量索引與授權不會跨來源共用。未完成的擷取各自以 durable job 處理。

`GET /api/v1/files` 是 metadata-only、伺服器篩選與分頁；不讀 binary／擷取全文。關聯名稱採固定批次查詢並限制筆數；只顯示登入者仍可讀的來源。`POST /files/{id}/retain` 保存獨立上傳；`DELETE /files/{id}` 在寫入鎖與 transaction 內檢查所有引用並清理私有閱讀器。所有端點檢查 owner 及 chat／knowledge／projects 其中一項功能 grant。沒有新增可略過既有授權的「檔案庫」grant。

FileLibraryStore 共用於頁面與選取器，搜尋去抖、取消舊請求並檢查 view／登入身分世代。FileBrowser 共用卡片、選取與列表。DocumentViewer 延遲載入，PDF.js 僅在需要 PDF 時下載；關閉預覽取消載入、輪詢及畫布渲染。

## 升級

`FileLibraryRetention` migration 新增保留欄位與 owner／保留狀態／時間／ID 索引，把已有 MessageAttachments 或 ResourceAttachments 的檔案標記為已保存，不複製 binary。停用舊 host 後執行 `./scripts/Initialize-Database.ps1` 再啟動新版本。SQL 審閱產物在 `db/migrations.sql`；完整備份包含 attachments、knowledge 及 collaboration schemas。
