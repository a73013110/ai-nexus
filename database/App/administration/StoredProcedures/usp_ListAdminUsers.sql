-- 管理頁的使用者列表（ListAdminUsers）：未刪除的使用者每頁 100 筆、依帳號排序，附角色、近期用量、對話數與儲存空間。
-- 結果集依序為：總筆數、本頁使用者、本頁使用者的角色。
-- @Since 與 @DefaultAttachmentLimitBytes 由程式傳入（TimeProvider 與設定），不用資料庫時鐘。
CREATE PROCEDURE [administration].[usp_ListAdminUsers]
    @Search                      NVARCHAR (120) = NULL,
    @Offset                      INT,
    @Since                       DATETIMEOFFSET (7),
    @DefaultAttachmentLimitBytes BIGINT
AS
SET NOCOUNT ON;

DECLARE @matches TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, Account NVARCHAR (256) NOT NULL);
DECLARE @page TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY, Ordinal INT NOT NULL);

-- CHARINDEX 不把 % 與 _ 當萬用字元，與程式的 Contains 相同；大小寫依資料庫定序。
INSERT @matches (Id, Account)
SELECT u.Id, u.Account
FROM [identity].[Users] AS u
WHERE u.DeletedAt IS NULL
  AND (@Search IS NULL
       OR CHARINDEX(@Search, u.Account) > 0
       OR CHARINDEX(@Search, u.DisplayName) > 0
       OR CHARINDEX(@Search, u.LocalAccount) > 0
       OR CHARINDEX(@Search, u.AdAccount) > 0);

SELECT COUNT(*) AS Total FROM @matches;

INSERT @page (Id, Ordinal)
SELECT m.Id, ROW_NUMBER() OVER (ORDER BY m.Account, m.Id)
FROM @matches AS m
ORDER BY m.Account, m.Id
OFFSET @Offset ROWS FETCH NEXT 100 ROWS ONLY;

WITH usage AS (
    SELECT r.OwnerId, r.Status, r.InputTokens, r.OutputTokens, r.DurationMilliseconds
    FROM [inference].[GenerationRuns] AS r
    WHERE r.CreatedAt >= @Since AND r.OwnerId IN (SELECT Id FROM @page)
    UNION ALL
    SELECT m.OwnerId, m.Status, m.InputTokens, m.OutputTokens, m.DurationMilliseconds
    FROM [inference].[ModelInvocations] AS m
    WHERE m.CreatedAt >= @Since AND m.OwnerId IN (SELECT Id FROM @page)
),
totals AS (
    SELECT OwnerId,
           COUNT(*) AS Requests,
           SUM(IIF(Status = N'completed', 1, 0)) AS Completed,
           SUM(IIF(Status = N'failed', 1, 0)) AS Failed,
           SUM(IIF(Status = N'cancelled', 1, 0)) AS Cancelled,
           SUM(ISNULL(InputTokens, 0)) AS InputTokens,
           SUM(ISNULL(OutputTokens, 0)) AS OutputTokens,
           SUM(IIF(InputTokens IS NOT NULL AND OutputTokens IS NOT NULL, 1, 0)) AS RequestsWithUsage,
           SUM(ISNULL(DurationMilliseconds, 0)) AS TotalDurationMilliseconds,
           SUM(IIF(DurationMilliseconds IS NOT NULL, 1, 0)) AS TimedRequests
    FROM usage
    GROUP BY OwnerId
),
-- 群組儲存上限：只算啟用中的使用者經由啟用的角色與群組取得的上限，取最小值（同 AccessService.GroupMemberships）。
groups AS (
    SELECT g.UserId, MIN(p.StoredAttachmentLimitBytes) AS GroupLimitBytes
    FROM (SELECT DISTINCT ur.UserId, rg.Id AS GroupId
          FROM [accesscontrol].[UserRoles] AS ur
          JOIN [identity].[Users] AS active ON active.Id = ur.UserId AND active.Enabled = 1 AND active.DeletedAt IS NULL
          JOIN [accesscontrol].[Roles] AS r ON r.Id = ur.RoleId AND r.Enabled = 1
          JOIN [accesscontrol].[RoleGroupRoles] AS link ON link.RoleId = r.Id
          JOIN [accesscontrol].[RoleGroups] AS rg ON rg.Id = link.GroupId AND rg.Enabled = 1
          WHERE ur.UserId IN (SELECT Id FROM @page)) AS g
    JOIN [accesscontrol].[GroupModelPolicies] AS p ON p.GroupId = g.GroupId
    GROUP BY g.UserId
),
files AS (
    SELECT a.OwnerId, SUM(a.Size) AS UsedBytes
    FROM [attachments].[Attachments] AS a
    WHERE a.OwnerId IN (SELECT Id FROM @page)
    GROUP BY a.OwnerId
),
conversations AS (
    -- 包含已刪除的對話：管理者看的是使用者累積的對話量。
    SELECT c.OwnerId, COUNT(*) AS Conversations
    FROM [conversations].[Conversations] AS c
    WHERE c.OwnerId IN (SELECT Id FROM @page)
    GROUP BY c.OwnerId
),
storage AS (
    -- 個人上限優先於群組上限，再來才是預設值（同 AttachmentQuota.Effective）。
    SELECT p.Id,
           ISNULL(f.UsedBytes, 0) AS UsedBytes,
           COALESCE(u.AttachmentLimitBytes, g.GroupLimitBytes, @DefaultAttachmentLimitBytes) AS LimitBytes,
           u.AttachmentLimitBytes AS PersonalLimitBytes,
           g.GroupLimitBytes,
           CASE WHEN u.AttachmentLimitBytes IS NOT NULL THEN N'personal' WHEN g.GroupLimitBytes IS NOT NULL THEN N'group' ELSE N'default' END AS LimitSource
    FROM @page AS p
    JOIN [identity].[Users] AS u ON u.Id = p.Id
    LEFT JOIN groups AS g ON g.UserId = p.Id
    LEFT JOIN files AS f ON f.OwnerId = p.Id
)
SELECT u.Id, u.Account, u.DisplayName, u.LastSeenAt, u.Enabled,
       u.AdEnabled, u.LocalEnabled, u.AdAccount, u.LocalAccount, CAST(IIF(u.PasswordHash IS NULL, 0, 1) AS BIT) AS HasLocalPassword,
       ISNULL(t.Requests, 0) AS Requests, ISNULL(t.Completed, 0) AS Completed, ISNULL(t.Failed, 0) AS Failed, ISNULL(t.Cancelled, 0) AS Cancelled,
       ISNULL(t.InputTokens, 0) AS InputTokens, ISNULL(t.OutputTokens, 0) AS OutputTokens, ISNULL(t.RequestsWithUsage, 0) AS RequestsWithUsage,
       ISNULL(t.TotalDurationMilliseconds, 0) AS TotalDurationMilliseconds, ISNULL(t.TimedRequests, 0) AS TimedRequests,
       ISNULL(c.Conversations, 0) AS Conversations,
       s.UsedBytes, s.LimitBytes, IIF(s.LimitBytes > s.UsedBytes, s.LimitBytes - s.UsedBytes, 0) AS RemainingBytes,
       s.PersonalLimitBytes, s.GroupLimitBytes, @DefaultAttachmentLimitBytes AS DefaultLimitBytes, s.LimitSource
FROM @page AS p
JOIN [identity].[Users] AS u ON u.Id = p.Id
JOIN storage AS s ON s.Id = p.Id
LEFT JOIN totals AS t ON t.OwnerId = p.Id
LEFT JOIN conversations AS c ON c.OwnerId = p.Id
ORDER BY p.Ordinal;

SELECT ur.UserId, ur.RoleId
FROM [accesscontrol].[UserRoles] AS ur
JOIN @page AS p ON p.Id = ur.UserId
ORDER BY ur.UserId, ur.RoleId;

GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'管理頁使用者列表：總筆數、本頁使用者（含用量、對話數、儲存空間）與其角色。', @level0type = N'SCHEMA', @level0name = N'administration', @level1type = N'PROCEDURE', @level1name = N'usp_ListAdminUsers';


GO

