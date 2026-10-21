-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-20
-- Description: 以目前的稽核情境寫入一筆稽核事件；procedure 寫入業務資料時與異動同一個交易呼叫。
--              身分與時間取自 udf_AuditContext。沒有情境時 OwnerId／At 為 NULL，INSERT 失敗，
--              呼叫端整支 procedure 回復，不會留下沒有身分的異動（失敗即拒絕）。
--              一次異動多列時不要逐列 EXEC：用 OUTPUT 收集異動列，再 CROSS APPLY udf_AuditContext 一次寫入（ADR 0014）。
--              只有一句 INSERT，不另開交易：由呼叫端的交易決定成敗。
-- Modify:      2026-10-21 Yikai 改用標準檔頭，說明移入檔頭
-- =============================================
CREATE PROCEDURE [audit].[usp_WriteAuditEvent]
    @Action      NVARCHAR (64),
    @ResourceId  UNIQUEIDENTIFIER = NULL,
    @Result      NVARCHAR (80),
    @DetailsJson NVARCHAR (MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- LEN 不計結尾空白，補一個字元再扣回才是實際長度。
    IF @DetailsJson IS NOT NULL AND (ISJSON(@DetailsJson) = 0 OR LEN(@DetailsJson + N'x') - 1 > 40000)
        THROW 51000, N'audit.usp_WriteAuditEvent: DetailsJson must be JSON of at most 40000 characters.', 1;

    INSERT [audit].[AuditEvents] (OwnerId, ActorId, TraceId, OperationId, Action, ResourceId, Result, DetailsJson, At)
    SELECT c.OwnerId, c.ActorId, c.TraceId, c.OperationId, @Action, @ResourceId, @Result, @DetailsJson, c.At
    FROM [audit].[udf_AuditContext]() AS c;
END;

GO

