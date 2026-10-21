-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的對話區塊：對話數、進行中的生成、期間內的回覆結果與平均耗時、模型分佈，以及檢視者最近的對話。
--              結果集依序為：計數、每日提問數、期間內各模型的生成數（多到少）、@Actor 未封存的對話（最近 6 筆）。
--              Chat 模組沒有自己的 schema，放在主要資料表所在的 conversations。軟刪除的對話（IsDeleted）不算，提問也只算未刪除對話裡的。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE PROCEDURE [conversations].[usp_GetChatDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    -- 生成紀錄不隨對話軟刪除，所以 GenerationRuns 不排除已刪除的對話。
    -- 平均耗時只算完成且有耗時的回覆：失敗或取消的沒有可比較的完整耗時；沒有這種回覆時是 NULL。
    SELECT c.Conversations, c.Favorites, c.Archived, a.Generating, p.Completed, p.Failed, p.Cancelled, p.LatencyMilliseconds
    FROM (SELECT COUNT(*) AS Conversations,
                 COUNT(CASE WHEN x.IsFavorite = 1 THEN 1 END) AS Favorites,
                 COUNT(CASE WHEN x.IsArchived = 1 THEN 1 END) AS Archived
          FROM [conversations].[Conversations] AS x
          WHERE x.IsDeleted = 0 AND (@Owner IS NULL OR x.OwnerId = @Owner)) AS c
    CROSS JOIN (SELECT COUNT(*) AS Generating
                FROM [inference].[GenerationRuns] AS r
                WHERE r.ActiveOwnerId IS NOT NULL AND (@Owner IS NULL OR r.OwnerId = @Owner)) AS a
    CROSS JOIN (SELECT COUNT(CASE WHEN r.Status = N'completed' THEN 1 END) AS Completed,
                       COUNT(CASE WHEN r.Status = N'failed' THEN 1 END) AS Failed,
                       COUNT(CASE WHEN r.Status = N'cancelled' THEN 1 END) AS Cancelled,
                       AVG(CASE WHEN r.Status = N'completed' THEN CAST (r.DurationMilliseconds AS FLOAT) END) AS LatencyMilliseconds
                FROM [inference].[GenerationRuns] AS r
                WHERE r.CreatedAt >= @From AND r.CreatedAt < @Until
                  AND (@Owner IS NULL OR r.OwnerId = @Owner)) AS p
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [conversations].[Messages] AS m
    JOIN [conversations].[Conversations] AS c ON c.Id = m.ConversationId
    CROSS APPLY [dbo].[udf_DashboardDay](m.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE m.Role = N'user' AND c.IsDeleted = 0 AND (@Owner IS NULL OR c.OwnerId = @Owner)
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);

    SELECT r.ModelId AS [Key], COUNT(*) AS [Value]
    FROM [inference].[GenerationRuns] AS r
    WHERE r.CreatedAt >= @From AND r.CreatedAt < @Until
      AND (@Owner IS NULL OR r.OwnerId = @Owner)
    GROUP BY r.ModelId
    ORDER BY [Value] DESC, [Key]
    OPTION (RECOMPILE);

    SELECT TOP (6) c.Id, c.Title, c.UpdatedAt, c.IsFavorite
    FROM [conversations].[Conversations] AS c
    WHERE c.OwnerId = @Actor AND c.IsDeleted = 0 AND c.IsArchived = 0
    ORDER BY c.UpdatedAt DESC, c.Id;
END;

GO

