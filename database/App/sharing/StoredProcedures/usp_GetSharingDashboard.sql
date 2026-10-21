-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的分享區塊：有效（未撤銷且未到期）的分享與即將到期數、期間內建立的分享、有效分享的種類分佈、@Owner 收到的分享，以及檢視者的收件匣。
--              結果集依序為：計數、每日建立分享數、有效分享各種類數量、@Actor 收到的有效分享（最近 5 筆）。
--              @Now 與 @ExpiringBefore（即將到期的界線）由程式的 TimeProvider 算出，不用資料庫時鐘；Received 只在指定 @Owner 時有意義（NULL 時為 0）。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE   PROCEDURE [sharing].[usp_GetSharingDashboard]
    @Owner          UNIQUEIDENTIFIER = NULL,
    @Actor          UNIQUEIDENTIFIER,
    @From           DATETIMEOFFSET (7),
    @Until          DATETIMEOFFSET (7),
    @OffsetMinutes  INT,
    @Now            DATETIMEOFFSET (7),
    @ExpiringBefore DATETIMEOFFSET (7)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.[Open], o.Expiring, r.Received
    FROM (SELECT COUNT(*) AS [Open], COUNT(CASE WHEN s.ExpiresAt <= @ExpiringBefore THEN 1 END) AS Expiring
          FROM [sharing].[ShareLinks] AS s
          WHERE s.IsRevoked = 0 AND s.ExpiresAt > @Now AND (@Owner IS NULL OR s.OwnerId = @Owner)) AS o
    CROSS JOIN (SELECT COUNT(*) AS Received
                FROM [sharing].[ShareRecipients] AS x
                JOIN [sharing].[ShareLinks] AS s ON s.Id = x.ShareId
                WHERE x.UserId = @Owner AND s.IsRevoked = 0 AND s.ExpiresAt > @Now) AS r
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [sharing].[ShareLinks] AS s
    CROSS APPLY [dbo].[udf_DashboardDay](s.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE @Owner IS NULL OR s.OwnerId = @Owner
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);

    SELECT s.Kind AS [Key], COUNT(*) AS [Value]
    FROM [sharing].[ShareLinks] AS s
    WHERE s.IsRevoked = 0 AND s.ExpiresAt > @Now AND (@Owner IS NULL OR s.OwnerId = @Owner)
    GROUP BY s.Kind
    ORDER BY [Key]
    OPTION (RECOMPILE);

    SELECT TOP (5) s.Id, s.Title, s.Kind, s.CreatedAt, s.ExpiresAt
    FROM [sharing].[ShareRecipients] AS x
    JOIN [sharing].[ShareLinks] AS s ON s.Id = x.ShareId
    WHERE x.UserId = @Actor AND s.IsRevoked = 0 AND s.ExpiresAt > @Now
    ORDER BY s.CreatedAt DESC, s.Id;
END;

GO

