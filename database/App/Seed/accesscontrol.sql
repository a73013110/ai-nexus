-- 內建選單分群、角色、群組、功能與預設授權。可重複執行：只新增缺少的列，不覆寫管理員之後的調整；
-- 要改既有環境的值，另寫 Data/<版本>/ 的一次性腳本。
-- 由 scripts/Deploy-Database.ps1 在結構與全文之後執行（docs/operations/database-setup.md）；整合測試的範本資料庫也執行這一份。
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

-- 只放可授予的功能（頁面與動作），ID 與 backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs 一致（FeatureCatalogSeedTests 雙向比對）。
-- 網址、預設名稱與圖示由程式目錄提供；Name 為 NULL 代表使用目錄預設名稱，只有管理員改名時才寫入覆寫。
-- SortOrder 與 GroupId 是現行選單的預設位置。
MERGE [accesscontrol].[Features] AS t
USING (VALUES
    (N'dashboard', 5, NULL),
    (N'chat', 10, N'work'),
    (N'projects', 15, N'work'),
    (N'files', 20, N'work'),
    (N'knowledge', 30, N'work'),
    (N'artifacts', 40, N'work'),
    (N'sharing', 50, N'collaboration'),
    (N'jobs', 55, N'collaboration'),
    (N'repositories', 60, N'collaboration'),
    (N'identity.users', 80, N'system'),
    (N'access-control.roles', 81, N'system'),
    (N'access-control.groups', 82, N'system'),
    (N'access-control.features', 83, N'system'),
    (N'operations.usage', 84, N'system'),
    (N'billing.prices', 85, N'system'),
    (N'knowledge.retrieval', 86, N'system'),
    (N'integrations', 92, N'system'),
    (N'monitoring', 93, N'system'),
    (N'diagnostics.logs', 94, N'system'),
    -- 動作：只是授權，不出現在選單。
    (N'diagnostics.logs.detail', 95, N'system'),
    (N'diagnostics.logs.export', 96, N'system'),
    (N'audit', 97, N'system'),
    (N'quality', 98, N'system'),
    (N'quality.retrieval', 99, N'system'),
    (N'design', 100, N'system')
) AS s ([Id], [SortOrder], [GroupId])
ON t.[Id] = s.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Name], [SortOrder], [GroupId], [Enabled])
    VALUES (s.[Id], NULL, s.[SortOrder], s.[GroupId], 1);

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
    (N'workspace', N'sharing'),
    (N'workspace', N'jobs'),
    (N'workspace', N'repositories'),
    (N'workspace', N'quality'),
    (N'workspace', N'quality.retrieval'),
    (N'administrators', N'identity.users'),
    (N'administrators', N'access-control.roles'),
    (N'administrators', N'access-control.groups'),
    (N'administrators', N'access-control.features'),
    (N'administrators', N'operations.usage'),
    (N'administrators', N'billing.prices'),
    (N'administrators', N'knowledge.retrieval'),
    (N'administrators', N'integrations'),
    (N'administrators', N'monitoring'),
    (N'administrators', N'diagnostics.logs'),
    (N'administrators', N'diagnostics.logs.detail'),
    (N'administrators', N'diagnostics.logs.export'),
    (N'administrators', N'audit'),
    (N'administrators', N'design')
) AS s ([GroupId], [FeatureId])
ON t.[GroupId] = s.[GroupId] AND t.[FeatureId] = s.[FeatureId]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([GroupId], [FeatureId]) VALUES (s.[GroupId], s.[FeatureId]);

COMMIT;
