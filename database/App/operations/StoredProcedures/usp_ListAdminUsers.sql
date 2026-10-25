
-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-20
-- Description: 管理頁的使用者列表：未刪除的使用者每頁 100 筆、依使用者順序排列，附通知信箱、角色、近期用量、對話數與儲存空間。
--              結果集依序為：總筆數與已排序人數、本頁使用者（含在整份名單的位置）、本頁使用者的角色。
--              使用者順序與程式的 UserOrder 相同：有 SortOrder 的在前，同值依姓名、帳號（AD 帳號優先，否則本地帳號）的二進位比較（程式用 ordinal），最後依 Id。
--              @Since 與 @DefaultAttachmentLimitBytes 由程式傳入（TimeProvider 與設定），不用資料庫時鐘。
-- Modify:      2026-10-21 Yikai 改用標準檔頭，說明移入檔頭
-- Modify:      2026-10-21 Yikai 改讀共用函式 udf_UsageEntries、udf_AttachmentStorage
-- Modify:      2026-10-24 Yikai 多回傳是否已以 SID 綁定 AD（DirectoryBound）
-- Modify:      2026-10-25 Yikai 改依使用者順序排列，多回傳 SortOrder、Position 與已排序人數
-- Modify:      2026-10-25 Yikai 移除 Users.Account，帳號改回傳 AD 帳號（沒有時為本地帳號）
-- =============================================
CREATE   PROCEDURE [operations].[usp_ListAdminUsers]
    @Search                      NVARCHAR (120) = NULL,
    @Offset                      INT,
    @Since                       DATETIMEOFFSET (7),
    @DefaultAttachmentLimitBytes BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @matches TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, Position INT NOT NULL);
    DECLARE @page TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, Position INT NOT NULL);

    -- Position 是在整份名單（不受搜尋影響）的位置：搜尋時畫面上的編號仍是出勤等列表中的實際順序。
    -- CHARINDEX 不把 % 與 _ 當萬用字元，與程式的 Contains 相同；大小寫依資料庫定序。
    WITH ordered AS (
        SELECT u.Id,
               ROW_NUMBER() OVER (ORDER BY IIF(u.SortOrder IS NULL, 1, 0), u.SortOrder,
                                           u.DisplayName COLLATE Latin1_General_100_BIN2,
                                           UPPER(COALESCE(u.AdAccount, u.LocalAccount)) COLLATE Latin1_General_100_BIN2, u.Id) AS Position
        FROM [identity].[Users] AS u
        WHERE u.DeletedAt IS NULL
    )
    INSERT @matches (Id, Position)
    SELECT o.Id, o.Position
    FROM ordered AS o
    JOIN [identity].[Users] AS u ON u.Id = o.Id
    WHERE (@Search IS NULL
           OR CHARINDEX(@Search, u.DisplayName) > 0
           OR CHARINDEX(@Search, u.LocalAccount) > 0
           OR CHARINDEX(@Search, u.AdAccount) > 0
           OR CHARINDEX(@Search, u.Email) > 0);

    SELECT COUNT(*) AS Total,
           (SELECT COUNT(*) FROM [identity].[Users] WHERE DeletedAt IS NULL AND SortOrder IS NOT NULL) AS Sorted
    FROM @matches;

    INSERT @page (Id, Position)
    SELECT m.Id, m.Position
    FROM @matches AS m
    ORDER BY m.Position
    OFFSET @Offset ROWS FETCH NEXT 100 ROWS ONLY;

    -- 用量紀錄與儲存空間的規則在共用函式（UsageReports、AttachmentQuota 讀同一份），這裡只依本頁使用者過濾。
    WITH totals AS (
        SELECT e.OwnerId,
               COUNT(*) AS Requests,
               SUM(IIF(e.Status = N'completed', 1, 0)) AS Completed,
               SUM(IIF(e.Status = N'failed', 1, 0)) AS Failed,
               SUM(IIF(e.Status = N'cancelled', 1, 0)) AS Cancelled,
               SUM(ISNULL(e.InputTokens, 0)) AS InputTokens,
               SUM(ISNULL(e.OutputTokens, 0)) AS OutputTokens,
               SUM(IIF(e.InputTokens IS NOT NULL AND e.OutputTokens IS NOT NULL, 1, 0)) AS RequestsWithUsage,
               SUM(ISNULL(e.DurationMilliseconds, 0)) AS TotalDurationMilliseconds,
               SUM(IIF(e.DurationMilliseconds IS NOT NULL, 1, 0)) AS TimedRequests
        FROM [inference].[udf_UsageEntries](@Since, NULL) AS e
        WHERE e.OwnerId IN (SELECT Id FROM @page)
        GROUP BY e.OwnerId
    ),
    conversations AS (
        -- 包含已刪除的對話：管理者看的是使用者累積的對話量。
        SELECT c.OwnerId, COUNT(*) AS Conversations
        FROM [conversations].[Conversations] AS c
        WHERE c.OwnerId IN (SELECT Id FROM @page)
        GROUP BY c.OwnerId
    )
    SELECT u.Id, COALESCE(u.AdAccount, u.LocalAccount) AS Account, u.DisplayName, u.LastSeenAt, u.Enabled,
           u.AdEnabled, u.LocalEnabled, u.AdAccount, u.LocalAccount, CAST(IIF(u.PasswordHash IS NULL, 0, 1) AS BIT) AS HasLocalPassword,
           -- 尚未綁定的帳號（管理員預建或解除綁定）SID 是 managed: 開頭，與程式的 UserAccounts.ManagedSidPrefix 一致。
           CAST(IIF(u.Sid LIKE N'managed:%', 0, 1) AS BIT) AS DirectoryBound,
           u.Email, u.EmailSource, u.EmailVerifiedAt, u.SortOrder, p.Position,
           ISNULL(t.Requests, 0) AS Requests, ISNULL(t.Completed, 0) AS Completed, ISNULL(t.Failed, 0) AS Failed, ISNULL(t.Cancelled, 0) AS Cancelled,
           ISNULL(t.InputTokens, 0) AS InputTokens, ISNULL(t.OutputTokens, 0) AS OutputTokens, ISNULL(t.RequestsWithUsage, 0) AS RequestsWithUsage,
           ISNULL(t.TotalDurationMilliseconds, 0) AS TotalDurationMilliseconds, ISNULL(t.TimedRequests, 0) AS TimedRequests,
           ISNULL(c.Conversations, 0) AS Conversations,
           s.UsedBytes, s.LimitBytes, s.RemainingBytes,
           s.PersonalLimitBytes, s.GroupLimitBytes, s.DefaultLimitBytes, s.LimitSource
    FROM @page AS p
    JOIN [identity].[Users] AS u ON u.Id = p.Id
    JOIN [files].[udf_AttachmentStorage](@DefaultAttachmentLimitBytes) AS s ON s.OwnerId = p.Id
    LEFT JOIN totals AS t ON t.OwnerId = p.Id
    LEFT JOIN conversations AS c ON c.OwnerId = p.Id
    ORDER BY p.Position;

    SELECT ur.UserId, ur.RoleId
    FROM [accesscontrol].[UserRoles] AS ur
    JOIN @page AS p ON p.Id = ur.UserId
    ORDER BY ur.UserId, ur.RoleId;
END;

GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'管理頁使用者列表：總筆數、本頁使用者（含用量、對話數、儲存空間）與其角色。', @level0type = N'SCHEMA', @level0name = N'operations', @level1type = N'PROCEDURE', @level1name = N'usp_ListAdminUsers';


GO

