-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的知識庫區塊：知識庫數、知識庫文件依處理狀態的數量與頁數、索引分段數、期間內新增的文件，以及檢視者最近的知識庫。
--              結果集依序為：計數、每日新增知識庫文件數、@Actor 的知識庫（最近 5 筆）。
--              知識庫文件＝未刪除（Documents.IsDeleted）、屬於某個知識庫，且 Resources 未軟刪除；私人讀取與專案文件沒有 CollectionId，不算。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE   PROCEDURE [knowledge].[usp_GetKnowledgeDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    WITH documents AS (
        SELECT d.Id, d.Status, d.PageCount
        FROM [knowledge].[Documents] AS d
        JOIN [collaboration].[Resources] AS r ON r.Id = d.Id
        WHERE d.IsDeleted = 0 AND d.CollectionId IS NOT NULL
          AND r.IsDeleted = 0 AND (@Owner IS NULL OR r.OwnerId = @Owner)
    )
    SELECT c.Collections, s.Documents, s.Ready, s.Failed, s.Pages, k.Chunks
    FROM (SELECT COUNT(*) AS Collections
          FROM [collaboration].[Resources] AS r
          WHERE r.Kind = N'knowledge' AND r.IsDeleted = 0 AND (@Owner IS NULL OR r.OwnerId = @Owner)) AS c
    CROSS JOIN (SELECT COUNT(*) AS Documents,
                       COUNT(CASE WHEN x.Status = N'ready' THEN 1 END) AS Ready,
                       COUNT(CASE WHEN x.Status = N'failed' THEN 1 END) AS Failed,
                       ISNULL(SUM(x.PageCount), 0) AS Pages
                FROM documents AS x) AS s
    CROSS JOIN (SELECT COUNT(*) AS Chunks
                FROM [knowledge].[Chunks] AS ch
                JOIN documents AS x ON x.Id = ch.DocumentId) AS k
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [collaboration].[Resources] AS r
    JOIN [knowledge].[Documents] AS doc ON doc.Id = r.Id
    CROSS APPLY [dbo].[udf_DashboardDay](r.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE r.Kind = N'document' AND r.IsDeleted = 0 AND (@Owner IS NULL OR r.OwnerId = @Owner)
      AND doc.IsDeleted = 0 AND doc.CollectionId IS NOT NULL
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);

    SELECT TOP (5) r.Id, r.Name, r.UpdatedAt
    FROM [collaboration].[Resources] AS r
    WHERE r.OwnerId = @Actor AND r.Kind = N'knowledge' AND r.IsDeleted = 0
    ORDER BY r.UpdatedAt DESC, r.Id;
END;

GO

