-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 每位使用者的附件儲存空間：已用、有效上限、剩餘、個人／群組／預設上限與上限來源，每位使用者一列（含停用與已刪除的使用者）。
--              @DefaultLimitBytes 由程式傳入（Files:DefaultOwnerLimitBytes）；欄位順序與 AttachmentStorageDto 一致（OwnerId 除外）。
--              呼叫端以 OwnerId 過濾，APPLY 依每位使用者以索引查詢，不彙總整張附件表。
-- =============================================
CREATE FUNCTION [files].[udf_AttachmentStorage] (@DefaultLimitBytes BIGINT)
RETURNS TABLE
AS
RETURN
    SELECT u.Id AS OwnerId,
           used.UsedBytes,
           cap.LimitBytes,
           IIF(cap.LimitBytes > used.UsedBytes, cap.LimitBytes - used.UsedBytes, 0) AS RemainingBytes,
           u.AttachmentLimitBytes AS PersonalLimitBytes,
           grp.GroupLimitBytes,
           @DefaultLimitBytes AS DefaultLimitBytes,
           CASE WHEN u.AttachmentLimitBytes IS NOT NULL THEN N'personal' WHEN grp.GroupLimitBytes IS NOT NULL THEN N'group' ELSE N'default' END AS LimitSource
    FROM [identity].[Users] AS u
    -- 多個有效群組取最小的上限；沒有群組、或群組都沒設上限時為 NULL。停用的使用者沒有有效群組。
    CROSS APPLY (SELECT MIN(p.StoredAttachmentLimitBytes) AS GroupLimitBytes
                 FROM [accesscontrol].[udf_UserGroups]() AS g
                 JOIN [accesscontrol].[GroupModelPolicies] AS p ON p.GroupId = g.GroupId
                 WHERE g.UserId = u.Id) AS grp
    -- 不分狀態：預約中（pending）與待清理（deleting）的附件仍佔站外空間，配額預約靠它防止超用。
    CROSS APPLY (SELECT ISNULL(SUM(a.Size), 0) AS UsedBytes
                 FROM [files].[Attachments] AS a
                 WHERE a.OwnerId = u.Id) AS used
    -- 個人上限優先於群組上限（管理員可為個人放寬），再來才是預設值。
    CROSS APPLY (SELECT COALESCE(u.AttachmentLimitBytes, grp.GroupLimitBytes, @DefaultLimitBytes) AS LimitBytes) AS cap;

GO

