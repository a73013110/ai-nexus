-- 內建選單分群、角色、群組、功能與預設授權。可重複執行：只新增缺少的列，不覆寫管理員之後的調整；
-- 要改既有環境的值，另寫 Data/<版本>/ 的一次性腳本。
-- 由原本 EF HasData 的內容轉寫；整合測試的 SQLite 範本以 backend/tests/AiNexus.IntegrationTests/Support/BaselineSeed.cs 寫入同一份資料，兩邊一起改。
-- 部署順序與執行方式見 docs/development/database-first-rollout.md 第 4 節（草稿，尚待納入部署腳本）。
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

MERGE [accesscontrol].[NavigationGroups] AS t
USING (VALUES
    (N'work', NULL, N'AI 工作台', 10),
    (N'collaboration', NULL, N'協作', 20),
    (N'system', NULL, N'系統管理', 30)
) AS s ([Id], [ParentId], [Name], [SortOrder])
ON t.[Id] = s.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [ParentId], [Name], [SortOrder]) VALUES (s.[Id], s.[ParentId], s.[Name], s.[SortOrder]);

MERGE [accesscontrol].[Features] AS t
USING (VALUES
    (N'dashboard', N'總覽', N'/dashboard', 5, NULL, N'dashboard'),
    (N'chat', N'對話', N'/chat', 10, N'work', N'chat'),
    (N'projects', N'專案', N'/projects', 15, N'work', N'projects'),
    (N'files', N'檔案庫', N'/files', 20, N'work', N'files'),
    (N'knowledge', N'知識庫', N'/knowledge', 30, N'work', N'library'),
    (N'artifacts', N'成果文件', N'/artifacts', 40, N'work', N'document'),
    (N'shared', N'分享', N'/shared', 50, N'collaboration', N'share'),
    (N'tasks', N'背景任務', N'/tasks', 55, N'collaboration', N'tasks'),
    (N'repositories', N'程式庫', N'/repositories', 60, N'collaboration', N'git'),
    (N'admin', N'存取管理', N'/admin', 90, N'system', N'lock'),
    (N'operations', N'平台營運', N'/admin/operations', 91, N'system', N'chart'),
    (N'integrations', N'資料來源', N'/integrations', 92, N'system', N'integrations'),
    (N'monitoring', N'即時監控', N'/admin/monitoring', 93, N'system', N'activity'),
    (N'logs.query', N'系統日誌', N'/admin/logs', 94, N'system', N'logs'),
    -- route 為空：只是授權，不出現在選單。
    (N'logs.detail', N'日誌診斷詳情', N'', 95, N'system', N'search'),
    (N'logs.export', N'日誌匯出', N'', 96, N'system', N'download'),
    (N'audit', N'活動稽核', N'/admin/audit', 97, N'system', N'audit'),
    (N'quality', N'品質評測', N'/quality', 98, N'system', N'shield'),
    -- 介面元件工作臺是純前端頁面、沒有 API；獨立成一個 grant 才能放進系統管理選單並單獨授予。
    (N'design', N'介面元件', N'/design', 99, N'system', N'sliders')
) AS s ([Id], [Name], [Route], [SortOrder], [GroupId], [Icon])
ON t.[Id] = s.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Name], [Route], [SortOrder], [GroupId], [Icon], [Enabled])
    VALUES (s.[Id], s.[Name], s.[Route], s.[SortOrder], s.[GroupId], s.[Icon], 1);

MERGE [accesscontrol].[Roles] AS t
USING (VALUES
    (N'member', N'一般使用者'),
    (N'administrator', N'平台管理員')
) AS s ([Id], [Name])
ON t.[Id] = s.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Name], [Enabled]) VALUES (s.[Id], s.[Name], 1);

MERGE [accesscontrol].[RoleGroups] AS t
USING (VALUES
    (N'workspace', N'基本工作區'),
    (N'administrators', N'平台管理')
) AS s ([Id], [Name])
ON t.[Id] = s.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Name], [Enabled]) VALUES (s.[Id], s.[Name], 1);

MERGE [accesscontrol].[RoleGroupRoles] AS t
USING (VALUES
    (N'member', N'workspace'),
    (N'administrator', N'administrators')
) AS s ([RoleId], [GroupId])
ON t.[RoleId] = s.[RoleId] AND t.[GroupId] = s.[GroupId]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([RoleId], [GroupId]) VALUES (s.[RoleId], s.[GroupId]);

-- 預設授權只寫進內建群組；新增的功能不會自動加到既有的自訂群組。
MERGE [accesscontrol].[RoleGroupFeatures] AS t
USING (VALUES
    (N'workspace', N'dashboard'),
    (N'workspace', N'chat'),
    (N'workspace', N'projects'),
    (N'workspace', N'files'),
    (N'workspace', N'knowledge'),
    (N'workspace', N'artifacts'),
    (N'workspace', N'shared'),
    (N'workspace', N'tasks'),
    (N'workspace', N'repositories'),
    (N'workspace', N'quality'),
    (N'administrators', N'admin'),
    (N'administrators', N'operations'),
    (N'administrators', N'integrations'),
    (N'administrators', N'monitoring'),
    (N'administrators', N'logs.query'),
    (N'administrators', N'logs.detail'),
    (N'administrators', N'logs.export'),
    (N'administrators', N'audit'),
    (N'administrators', N'design')
) AS s ([GroupId], [FeatureId])
ON t.[GroupId] = s.[GroupId] AND t.[FeatureId] = s.[FeatureId]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([GroupId], [FeatureId]) VALUES (s.[GroupId], s.[FeatureId]);

COMMIT;
