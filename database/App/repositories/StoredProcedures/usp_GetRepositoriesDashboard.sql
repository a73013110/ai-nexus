-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的程式庫區塊：已連線帳號、進行中的 AI review、期間內 review 的結果分佈，以及檢視者最近的 review。
--              結果集依序為：計數、每日 review 數、期間內 review 依背景任務狀態的數量、@Actor 的 review（最近 5 筆）。
--              review 的狀態取自它的背景任務；任務已不存在時視為 completed（與程式原本的預設相同）。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE PROCEDURE [repositories].[usp_GetRepositoriesDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.Connections, a.Running
    FROM (SELECT COUNT(*) AS Connections
          FROM [repositories].[RepositoryConnections] AS x
          WHERE @Owner IS NULL OR x.OwnerId = @Owner) AS c
    CROSS JOIN (SELECT COUNT(*) AS Running
                FROM [repositories].[RepositoryReviews] AS r
                JOIN [jobs].[BackgroundJobs] AS j ON j.Id = r.JobId
                WHERE j.Status IN (N'queued', N'running') AND (@Owner IS NULL OR r.OwnerId = @Owner)) AS a
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [repositories].[RepositoryReviews] AS r
    CROSS APPLY [dbo].[udf_DashboardDay](r.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE @Owner IS NULL OR r.OwnerId = @Owner
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);

    SELECT ISNULL(j.Status, N'completed') AS [Key], COUNT(*) AS [Value]
    FROM [repositories].[RepositoryReviews] AS r
    LEFT JOIN [jobs].[BackgroundJobs] AS j ON j.Id = r.JobId
    WHERE r.CreatedAt >= @From AND r.CreatedAt < @Until
      AND (@Owner IS NULL OR r.OwnerId = @Owner)
    GROUP BY ISNULL(j.Status, N'completed')
    ORDER BY [Key]
    OPTION (RECOMPILE);

    SELECT TOP (5) r.Id, r.Repository, r.[Commit], r.CreatedAt
    FROM [repositories].[RepositoryReviews] AS r
    WHERE r.OwnerId = @Actor
    ORDER BY r.CreatedAt DESC, r.Id;
END;

GO

