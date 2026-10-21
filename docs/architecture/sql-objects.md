# SQL 物件（procedure、function、view）

所有 procedure、function、view 用同一個檔頭與骨架：打開任何一支，都在同一個位置找到用途、結果集與異動紀錄。何時用 procedure 見 [ADR 0011](../decisions/0011-data-access-ef-dapper-procedures.md)，稽核見 [ADR 0014](../decisions/0014-procedure-audit.md)。

## 命名與排版

- procedure `<schema>.usp_<UseCase>`：schema＝擁有該 use case 的模組小寫，名稱＝use case 名稱（`ListAdminUsers` → `operations.usp_ListAdminUsers`），不放 `dbo`。function `udf_`、view `vw_`（[ADR 0012](../decisions/0012-database-naming-and-foreign-keys.md)）。
- 識別碼一律 `[schema].[name]`，關鍵字大寫，4 空白縮排；檔案 UTF-8 無 BOM、LF（匯出時統一）。

## 檔頭

```sql
-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 一句用途。
--              續行：結果集順序、參數來源、陷阱與為什麼。
-- Modify:      2026-10-22 Yikai 這次改了什麼
-- =============================================
```

- `Description` 第一行一句用途；續行只寫看程式看不出來的事。新物件沒有 `Modify` 行，之後每次異動加一行「日期 作者 說明」，舊行保留。
- 檔頭取代 `MS_Description`：procedure、function、view 不加 extended property；資料表與欄位的說明照舊（[ADR 0012](../decisions/0012-database-naming-and-foreign-keys.md)）。
- **檔頭與 `CREATE` 必須在同一個批次**（中間沒有 `GO`）：SQL Server 只把同一批次的文字存進物件定義，隔了 `GO` 的註解匯出時就消失。
- 只和某一句有關的陷阱（鎖定提示、萬用字元、時區）寫成行內註解，放在那一句上方。

## 讀取

```sql
-- （檔頭）
CREATE PROCEDURE [operations].[usp_ListThings]
    @Search NVARCHAR (120) = NULL,
    @Offset INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT t.Id, t.Name
    FROM [operations].[Things] AS t
    WHERE @Search IS NULL OR CHARINDEX(@Search, t.Name) > 0
    ORDER BY t.Name, t.Id
    OFFSET @Offset ROWS FETCH NEXT 100 ROWS ONLY;
END;

GO
```

- 多個結果集時在 `Description` 寫順序；時間與上限由程式傳入（`TimeProvider`、設定），不用資料庫時鐘。

## 寫入

```sql
-- （檔頭）
CREATE PROCEDURE [billing].[usp_RenameThing]
    @Id   UNIQUEIDENTIFIER,
    @Name NVARCHAR (100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ownsTran BIT = IIF(@@TRANCOUNT = 0, 1, 0);
    BEGIN TRY
        IF @ownsTran = 1 BEGIN TRAN;

        UPDATE [billing].[Things] SET Name = @Name WHERE Id = @Id;
        IF @@ROWCOUNT = 0 THROW 50000, N'thing_not_found', 1;

        EXEC [audit].[usp_WriteAuditEvent] @Action = N'billing.thing.renamed', @ResourceId = @Id, @Result = N'saved';

        IF @ownsTran = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @ownsTran = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;

GO
```

- `@ownsTran`：只在沒有外層交易時自己開，出錯直接 `THROW` 交給呼叫端回復，原因見 [ADR 0011](../decisions/0011-data-access-ef-dapper-procedures.md#交易)。只有一句異動、沒有先查後寫時可以省略交易區塊（例如 `audit.usp_WriteAuditEvent`）。
- 先查再寫的檢查加 `WITH (UPDLOCK, HOLDLOCK)`：RCSI 下一般讀取不等寫入（[讀取隔離](database.md#讀取隔離rcsi)）。
- 預期內的失敗 `THROW 50000, N'<error_code>', 1;`，代碼與 `<Module>Errors` 相同（`ProcedureError`）。
- 成功稽核與異動同一個交易；多列異動用 `OUTPUT` 一次寫入，動作名稱與明細規則見 [ADR 0014](../decisions/0014-procedure-audit.md)。

## function 與 view

只套檔頭，沒有 `BEGIN`／`END`：

```sql
-- （檔頭）
CREATE FUNCTION [audit].[udf_Something] ()
RETURNS TABLE
AS
RETURN
    SELECT 1 AS Value;

GO
```

- function 用 inline 資料表值函式；scalar 函式在 `SELECT` 裡會逐列呼叫（[ADR 0014](../decisions/0014-procedure-audit.md#效能)）。
- view：`CREATE VIEW [<schema>].[vw_<名稱>] AS SELECT …;`。

## 流程

1. 在開發庫的 SSMS 依範本執行：新物件 `CREATE`；修改時從 `database/App/<schema>/` 開檔，`CREATE` 改成 `ALTER`、加一行 `Modify` 後執行。不用 `CREATE OR ALTER`：匯出檔會變成 `CREATE   PROCEDURE`，多出空白差異。
2. `./scripts/Show-Scripts.ps1` 選 `Export-Database.ps1` 匯出，只 stage 與本次有關的檔案。
3. `dotnet build database/App`（引用錯誤在這裡失敗），再跑 `./scripts/Verify.ps1`。

`database/App/` 的物件檔是 `CREATE`＋`GO`，可直接在 SSMS 執行，用來在其他庫建同一個物件。

## 程式端

- 讀取用 `QueryProcedureAsync`，寫入用 `ExecuteProcedureAsync`（同一個批次帶稽核情境）；record 欄位名稱、順序、型別與 `SELECT` 一致。
- 每支被程式呼叫的 procedure 都要有整合測試（`ProcedureCoverageTests`）；寫稽核的用 `ProcedureAudits` 檢查。
- 程式與 procedure 都要用的規則寫成一支 inline 函式，兩邊都讀它，不在 C# 重寫一次：EF 以 `[DbFunction]` 對應成可再組合的 `IQueryable`（`AppDbContext.UsageEntries`，keyless 實體 `ToTable(null)`），Dapper 直接從函式 `SELECT`（`AttachmentQuota`）。函式不在 `ProcedureCoverageTests` 範圍內，結果另以整合測試檢查。
- 範例：[`ListAdminUsers`](../../backend/src/AiNexus.Features/Admin/Operations/ListAdminUsers.cs)、[`CreateModelPrice`](../../backend/src/AiNexus.Features/Admin/Billing/Prices/CreateModelPrice.cs)。
