-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽的通知區塊：@Actor 收件匣（未 dismiss）中的未讀與未讀錯誤數、期間內每日收到的通知，以及最近的未讀通知。
--              結果集依序為：計數、每日收到通知數（含已讀）、@Actor 的未讀通知（最近 6 筆）；錯誤通知的遮罩由程式處理。
--              通知是個人資料，全平台範圍也只看 @Actor 自己的，所以不使用 @Owner。
-- =============================================
CREATE   PROCEDURE [notifications].[usp_GetNotificationsDashboard]
    @Owner         UNIQUEIDENTIFIER = NULL,
    @Actor         UNIQUEIDENTIFIER,
    @From          DATETIMEOFFSET (7),
    @Until         DATETIMEOFFSET (7),
    @OffsetMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS Unread, COUNT(CASE WHEN n.Severity = N'error' THEN 1 END) AS Errors
    FROM [notifications].[Notifications] AS n
    WHERE n.OwnerId = @Actor AND n.DismissedAt IS NULL AND n.ReadAt IS NULL;

    SELECT d.[Day], COUNT(*) AS [Value]
    FROM [notifications].[Notifications] AS n
    CROSS APPLY [dbo].[udf_DashboardDay](n.CreatedAt, @From, @Until, @OffsetMinutes) AS d
    WHERE n.OwnerId = @Actor AND n.DismissedAt IS NULL
    GROUP BY d.[Day]
    ORDER BY d.[Day];

    SELECT TOP (6) n.Id, n.Title, n.Body, n.Severity, n.IssueCode, n.TargetKind, n.TargetId, n.CreatedAt
    FROM [notifications].[Notifications] AS n
    WHERE n.OwnerId = @Actor AND n.DismissedAt IS NULL AND n.ReadAt IS NULL
    ORDER BY n.CreatedAt DESC, n.Id;
END;

GO

