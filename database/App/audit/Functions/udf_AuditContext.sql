-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-20
-- Description: 目前批次的稽核情境（有效身分、實際操作者、追蹤識別、時間），一列。
--              由程式在呼叫 procedure 的同一個批次以 sp_set_session_context 帶入（ADR 0014）；沒有情境時全部為 NULL。
--              inline 函式會展開進查詢計畫，CROSS APPLY 到多列也沒有逐列呼叫的成本。
-- Modify:      2026-10-21 Yikai 改用標準檔頭，說明移入檔頭
-- =============================================
CREATE   FUNCTION [audit].[udf_AuditContext] ()
RETURNS TABLE
AS
RETURN
    SELECT CAST(SESSION_CONTEXT(N'audit.owner_id') AS UNIQUEIDENTIFIER) AS OwnerId,
           CAST(SESSION_CONTEXT(N'audit.actor_id') AS UNIQUEIDENTIFIER) AS ActorId,
           CAST(SESSION_CONTEXT(N'audit.trace_id') AS NVARCHAR (32)) AS TraceId,
           CAST(SESSION_CONTEXT(N'audit.operation_id') AS UNIQUEIDENTIFIER) AS OperationId,
           CAST(SESSION_CONTEXT(N'audit.at') AS DATETIMEOFFSET (7)) AS At;

GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'目前批次的稽核情境（有效身分、實際操作者、追蹤識別、時間），由程式以 session context 帶入。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'FUNCTION', @level1name = N'udf_AuditContext';


GO

