# 資料庫優先導入計畫

把 [ADR 0010](../decisions/0010-database-first-schema.md)～[0014](../decisions/0014-procedure-audit.md) 落地的步驟。專案尚未上線，不保留相容層。**全部完成後刪除這份文件**，長期規則留在 ADR 與 [資料庫](../architecture/database.md)。

## 6. fork 範本

- 新增 `.template.config/template.json`（`sourceName: AiNexus`），並實際 fork 出一個新名稱的專案，從 build、建庫到啟動完整驗證一次。
- PowerShell 模組函式的 `-Nexus` 改為中性名稱。
- 設定鍵 `Databases:Nexus`（`AppDbContext.DatabaseName`）仍帶產品名，改為角色名 `App`，連同範本設定、腳本與文件。

## 7. 文件

後續階段依 ADR 改寫相關文件，並刪除過時段落。

## 順序

7 隨各階段一起更新。
