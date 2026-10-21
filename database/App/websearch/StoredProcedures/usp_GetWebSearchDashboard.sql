-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的網路搜尋區塊：期間內每日搜尋數與失敗數。
--              結果集依序為：計數、每日搜尋數；期間總數由程式加總每日數量，這個區塊沒有個人項目，@Actor 不使用。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE   PROCEDURE [websearch].[usp_GetWebSearchDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS Failed
    FROM [websearch].[WebSearches] AS w
    WHERE w.Status = N'failed' AND w.CreatedAt >= @From AND w.CreatedAt < @Until
      AND (@Owner IS NULL OR w.OwnerId = @Owner)
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [websearch].[WebSearches] AS w
    CROSS APPLY [dbo].[udf_DashboardDay](w.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE @Owner IS NULL OR w.OwnerId = @Owner
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);
END;

GO

