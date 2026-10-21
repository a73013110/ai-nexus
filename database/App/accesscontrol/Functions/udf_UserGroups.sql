-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 使用者目前有效的群組：啟用且未刪除的使用者，經由啟用的角色取得的啟用群組，每對 (UserId, GroupId) 一列。
--              「啟用且未刪除」與 Identity 的 ActiveUsers（IActiveUsers）相同；授權判斷留在程式（AccessService，ADR 0011），兩邊要一起改。
--              供 SQL 報表（儲存上限、管理列表）使用；呼叫端以 UserId 過濾，條件會展開進查詢計畫。
-- =============================================
CREATE FUNCTION [accesscontrol].[udf_UserGroups] ()
RETURNS TABLE
AS
RETURN
    SELECT DISTINCT ur.UserId, rg.Id AS GroupId
    FROM [accesscontrol].[UserRoles] AS ur
    JOIN [identity].[Users] AS u ON u.Id = ur.UserId AND u.Enabled = 1 AND u.DeletedAt IS NULL
    JOIN [accesscontrol].[Roles] AS r ON r.Id = ur.RoleId AND r.Enabled = 1
    JOIN [accesscontrol].[RoleGroupRoles] AS link ON link.RoleId = r.Id
    JOIN [accesscontrol].[RoleGroups] AS rg ON rg.Id = link.GroupId AND rg.Enabled = 1;

GO

