# 資料庫優先導入計畫

把 [ADR 0010](../decisions/0010-database-first-schema.md)～[0014](../decisions/0014-procedure-audit.md) 落地的步驟。專案尚未上線，不保留相容層。**全部完成後刪除這份文件**，長期規則留在 ADR 與 [資料庫](../architecture/database.md)。

## 5. procedure 範本與稽核

- `ISqlDatabase<T>` 加入呼叫 procedure 的方法（`CommandType.StoredProcedure`）；寫入用的方法依 [ADR 0014](../decisions/0014-procedure-audit.md) 在同一個批次設定稽核情境。
- 在開發庫建立 `audit.udf_AuditContext` 與 `audit.usp_WriteAuditEvent`，匯出後提交；整合測試的稽核 helper 一併完成。
- 兩支範例，讓後續開發者照抄：
  - 讀取：`ListAdminUsers` 改為 `administration.usp_ListAdminUsers`，handler 只負責驗證與呼叫。
  - 寫入：挑一個不需要前後快照的單純管理寫入改成 procedure，示範交易寫法、成功稽核由 procedure 寫、失敗稽核由程式寫、成功後發 domain event。
- 流程：在 SSMS 建立 → 匯出 → 程式呼叫 → 整合測試。

## 6. fork 範本

- 新增 `.template.config/template.json`（`sourceName: AiNexus`），並實際 fork 出一個新名稱的專案，從 build、建庫到啟動完整驗證一次。
- PowerShell 模組函式的 `-Nexus` 改為中性名稱。
- 設定鍵 `Databases:Nexus`（`AppDbContext.DatabaseName`）仍帶產品名，改為角色名 `App`，連同範本設定、腳本與文件。

## 7. 文件

後續階段依 ADR 改寫相關文件，並刪除過時段落。

## 順序

5、6 可以平行；7 隨各階段一起更新。
