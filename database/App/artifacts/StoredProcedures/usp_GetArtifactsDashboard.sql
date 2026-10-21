-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的成果文件區塊：目前的成果數（屬於專案、由對話建立）、期間內保存的版本，以及檢視者最近的成果。
--              結果集依序為：計數、每日保存版本數、@Actor 的成果（最近 5 筆）。
--              成果數只算未軟刪除的 Resources；版本數依作者（協作者的編輯算在協作者名下），不看成果是否已刪除。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE   PROCEDURE [artifacts].[usp_GetArtifactsDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS Artifacts,
           COUNT(a.ProjectId) AS InProjects,
           COUNT(a.SourceMessageId) AS FromChat
    FROM [artifacts].[Artifacts] AS a
    JOIN [collaboration].[Resources] AS r ON r.Id = a.Id
    WHERE r.Kind = N'artifact' AND r.IsDeleted = 0 AND (@Owner IS NULL OR r.OwnerId = @Owner)
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [artifacts].[ArtifactRevisions] AS v
    CROSS APPLY [dbo].[udf_DashboardDay](v.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE @Owner IS NULL OR v.AuthorId = @Owner
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);

    SELECT TOP (5) r.Id, r.Name, r.UpdatedAt
    FROM [collaboration].[Resources] AS r
    WHERE r.OwnerId = @Actor AND r.Kind = N'artifact' AND r.IsDeleted = 0
    ORDER BY r.UpdatedAt DESC, r.Id;
END;

GO

