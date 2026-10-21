-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的檔案庫區塊：已保存（InLibrary 且 ready）的原檔數與大小、期間內每日保存數、各 ContentType 的數量。
--              結果集依序為：計數、每日保存數、各 ContentType 的原檔數；類型歸類（圖片、PDF…）與儲存配額由程式處理。
--              這個區塊沒有個人項目，@Actor 不使用。
--              @Owner 為 NULL＝全平台；依 @Owner 篩選的查詢加 OPTION (RECOMPILE)，個人範圍才會用 OwnerId 索引而不沿用全平台的計畫。
-- =============================================
CREATE   PROCEDURE [files].[usp_GetFilesDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS Files, ISNULL(SUM(a.Size), 0) AS Bytes
    FROM [files].[Attachments] AS a
    WHERE a.InLibrary = 1 AND a.StorageState = N'ready' AND (@Owner IS NULL OR a.OwnerId = @Owner)
    OPTION (RECOMPILE);

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [files].[Attachments] AS a
    CROSS APPLY [dbo].[udf_DashboardDay](a.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE a.InLibrary = 1 AND a.StorageState = N'ready' AND (@Owner IS NULL OR a.OwnerId = @Owner)
    GROUP BY d.[Day]
    ORDER BY d.[Day]
    OPTION (RECOMPILE);

    SELECT a.ContentType AS [Key], COUNT(*) AS [Value]
    FROM [files].[Attachments] AS a
    WHERE a.InLibrary = 1 AND a.StorageState = N'ready' AND (@Owner IS NULL OR a.OwnerId = @Owner)
    GROUP BY a.ContentType
    ORDER BY [Key]
    OPTION (RECOMPILE);
END;

GO

