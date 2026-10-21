-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 用量紀錄：聊天執行（GenerationRuns，Kind 固定為 chat）與模型工作（ModelInvocations）合併成同一種列，供用量報表一起統計。
--              期間為 [@From, @Until)，@Until 為 NULL 表示不限；時間由程式傳入（TimeProvider），不用資料庫時鐘。
--              EF 以 AppDbContext.UsageEntries 對應（UsageReports），operations.usp_ListAdminUsers 也讀它。
-- =============================================
CREATE FUNCTION [inference].[udf_UsageEntries] (@From DATETIMEOFFSET (7), @Until DATETIMEOFFSET (7))
RETURNS TABLE
AS
RETURN
    -- @Until 為 NULL 時以最大值代替，而不是寫 OR：兩個邊界都成為 CreatedAt 覆蓋索引的 seek 範圍。
    SELECT r.OwnerId, r.CreatedAt, CAST(N'chat' AS NVARCHAR (32)) AS Kind, r.ModelId, r.Status, r.InputTokens, r.OutputTokens, r.DurationMilliseconds
    FROM [inference].[GenerationRuns] AS r
    WHERE r.CreatedAt >= @From
      AND r.CreatedAt < ISNULL(@Until, CAST('9999-12-31T23:59:59.9999999+00:00' AS DATETIMEOFFSET (7)))
    UNION ALL
    SELECT m.OwnerId, m.CreatedAt, m.Kind, m.ModelId, m.Status, m.InputTokens, m.OutputTokens, m.DurationMilliseconds
    FROM [inference].[ModelInvocations] AS m
    WHERE m.CreatedAt >= @From
      AND m.CreatedAt < ISNULL(@Until, CAST('9999-12-31T23:59:59.9999999+00:00' AS DATETIMEOFFSET (7)));

GO

