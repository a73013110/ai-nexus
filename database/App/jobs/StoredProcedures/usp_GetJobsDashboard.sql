-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的背景任務區塊：處理中與失敗的數量、期間內建立與完成的數量、各種類分佈，以及檢視者自己的未結束任務。
--              結果集依序為：計數、每日建立數、期間內各種類數量、@Actor 的 queued／running／failed 任務（最近 6 筆）。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE PROCEDURE [jobs].[usp_GetJobsDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    -- 「處理中」是 queued 與 running；失敗只計 failed，不含其他未完成的狀態。
    SELECT c.Queued, c.Running, c.Failed, p.Completed
    FROM (SELECT COUNT(CASE WHEN j.Status = N'queued' THEN 1 END) AS Queued,
                 COUNT(CASE WHEN j.Status = N'running' THEN 1 END) AS Running,
                 COUNT(CASE WHEN j.Status = N'failed' THEN 1 END) AS Failed
          FROM [jobs].[BackgroundJobs] AS j
          WHERE j.Status IN (N'queued', N'running', N'failed')
            AND (@Owner IS NULL OR j.OwnerId = @Owner)) AS c
    CROSS JOIN (SELECT COUNT(*) AS Completed
                FROM [jobs].[BackgroundJobs] AS j
                WHERE j.Status = N'completed' AND j.CreatedAt >= @From AND j.CreatedAt < @Until
                  AND (@Owner IS NULL OR j.OwnerId = @Owner)) AS p
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [jobs].[BackgroundJobs] AS j
    CROSS APPLY [dbo].[udf_DashboardDay](j.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE @Owner IS NULL OR j.OwnerId = @Owner
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);

    SELECT j.Kind AS [Key], COUNT(*) AS [Value]
    FROM [jobs].[BackgroundJobs] AS j
    WHERE j.CreatedAt >= @From AND j.CreatedAt < @Until
      AND (@Owner IS NULL OR j.OwnerId = @Owner)
    GROUP BY j.Kind
    ORDER BY [Value] DESC, [Key]
    OPTION (RECOMPILE);

    SELECT TOP (6) j.Id, j.Label, j.Kind, j.Stage, j.Status, j.UpdatedAt
    FROM [jobs].[BackgroundJobs] AS j
    WHERE j.OwnerId = @Actor AND j.Status IN (N'queued', N'running', N'failed')
    ORDER BY j.UpdatedAt DESC, j.Id;
END;

GO

