-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的回答品質區塊：期間內有幫助／沒幫助的評分數、發起的模型與檢索評測數，以及仍在背景任務中的評測。
--              結果集依序為：計數、每日評分數；只計評分（Rating 不為 0），理由與備註不讀取。這個區塊沒有個人項目，@Actor 不使用。
--              評分依 UpdatedAt 落在期間內計算（改評分會移到新的日期）；進行中的評測不受期間限制。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE PROCEDURE [quality].[usp_GetQualityDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT f.Up, f.Down, e.Evaluations, r.Retrievals, a.Running
    FROM (SELECT COUNT(CASE WHEN x.Rating > 0 THEN 1 END) AS Up, COUNT(CASE WHEN x.Rating < 0 THEN 1 END) AS Down
          FROM [quality].[MessageFeedback] AS x
          WHERE x.Rating <> 0 AND x.UpdatedAt >= @From AND x.UpdatedAt < @Until
            AND (@Owner IS NULL OR x.OwnerId = @Owner)) AS f
    CROSS JOIN (SELECT COUNT(*) AS Evaluations
                FROM [quality].[EvaluationRuns] AS x
                WHERE x.CreatedAt >= @From AND x.CreatedAt < @Until AND (@Owner IS NULL OR x.OwnerId = @Owner)) AS e
    CROSS JOIN (SELECT COUNT(*) AS Retrievals
                FROM [quality].[RetrievalEvaluations] AS x
                WHERE x.CreatedAt >= @From AND x.CreatedAt < @Until AND (@Owner IS NULL OR x.OwnerId = @Owner)) AS r
    CROSS JOIN (SELECT COUNT(*) AS Running
                FROM (SELECT x.JobId FROM [quality].[EvaluationRuns] AS x WHERE @Owner IS NULL OR x.OwnerId = @Owner
                      UNION ALL
                      SELECT x.JobId FROM [quality].[RetrievalEvaluations] AS x WHERE @Owner IS NULL OR x.OwnerId = @Owner) AS q
                JOIN [jobs].[BackgroundJobs] AS j ON j.Id = q.JobId
                WHERE j.Status IN (N'queued', N'running')) AS a
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [quality].[MessageFeedback] AS x
    CROSS APPLY [dbo].[udf_DashboardDay](x.UpdatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE x.Rating <> 0 AND (@Owner IS NULL OR x.OwnerId = @Owner)
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);
END;

GO

