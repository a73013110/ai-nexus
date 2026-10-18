-- 寫一筆稽核，身分與時間取自 udf_AuditContext。沒有情境時 OwnerId／At 為 NULL，INSERT 失敗，
-- 呼叫端整支 procedure 回復，不會留下沒有身分的異動（失敗即拒絕）。
-- 一次異動多列時不要逐列 EXEC：用 OUTPUT 收集異動列，再 CROSS APPLY udf_AuditContext 一次寫入（ADR 0014）。
CREATE PROCEDURE [audit].[usp_WriteAuditEvent]
    @Action      NVARCHAR (64),
    @ResourceId  UNIQUEIDENTIFIER = NULL,
    @Result      NVARCHAR (80),
    @DetailsJson NVARCHAR (MAX) = NULL
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- LEN 不計結尾空白，補一個字元再扣回才是實際長度。
IF @DetailsJson IS NOT NULL AND (ISJSON(@DetailsJson) = 0 OR LEN(@DetailsJson + N'x') - 1 > 40000)
    THROW 51000, N'audit.usp_WriteAuditEvent: DetailsJson must be JSON of at most 40000 characters.', 1;

INSERT [audit].[AuditEvents] (OwnerId, ActorId, TraceId, OperationId, Action, ResourceId, Result, DetailsJson, At)
SELECT c.OwnerId, c.ActorId, c.TraceId, c.OperationId, @Action, @ResourceId, @Result, @DetailsJson, c.At
FROM [audit].[udf_AuditContext]() AS c;

GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'以目前的稽核情境寫入一筆稽核事件；procedure 寫入業務資料時與異動同一個交易呼叫。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'PROCEDURE', @level1name = N'usp_WriteAuditEvent';


GO

