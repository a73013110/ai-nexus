-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的專案區塊：專案與封存數、提問範本數、期間內建立的專案、別人共用給 @Owner 的專案，以及檢視者最近的專案。
--              結果集依序為：計數、每日建立專案數、@Actor 的專案（最近 5 筆）。
--              只算未軟刪除的 Resources；Shared 只在指定 @Owner 時有意義（NULL 時為 0，程式不顯示）。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE PROCEDURE [projects].[usp_GetProjectsDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    WITH projects AS (
        SELECT p.Id, p.IsArchived
        FROM [projects].[Projects] AS p
        JOIN [collaboration].[Resources] AS r ON r.Id = p.Id
        WHERE r.Kind = N'project' AND r.IsDeleted = 0 AND (@Owner IS NULL OR r.OwnerId = @Owner)
    )
    SELECT c.Projects, c.Archived, t.Templates, s.Shared
    FROM (SELECT COUNT(*) AS Projects, COUNT(CASE WHEN x.IsArchived = 1 THEN 1 END) AS Archived
          FROM projects AS x) AS c
    CROSS JOIN (SELECT COUNT(*) AS Templates
                FROM [projects].[ProjectTemplates] AS pt
                JOIN projects AS x ON x.Id = pt.ProjectId) AS t
    CROSS JOIN (SELECT COUNT(*) AS Shared
                FROM [collaboration].[ResourceMembers] AS m
                JOIN [collaboration].[Resources] AS r ON r.Id = m.ResourceId
                WHERE m.UserId = @Owner AND r.Kind = N'project' AND r.IsDeleted = 0 AND r.OwnerId <> @Owner) AS s
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [collaboration].[Resources] AS r
    CROSS APPLY [dbo].[udf_DashboardDay](r.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE r.Kind = N'project' AND r.IsDeleted = 0 AND (@Owner IS NULL OR r.OwnerId = @Owner)
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);

    SELECT TOP (5) r.Id, r.Name, r.UpdatedAt
    FROM [collaboration].[Resources] AS r
    WHERE r.OwnerId = @Actor AND r.Kind = N'project' AND r.IsDeleted = 0
    ORDER BY r.UpdatedAt DESC, r.Id;
END;

GO

