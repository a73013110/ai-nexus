-- 內建角色、群組、功能、預設授權與預設選單。可重複執行：只新增缺少的列（以主鍵比對），不覆寫管理員之後的調整；
-- 要改既有環境的值，另寫 Data/<版本>/ 的一次性腳本。
-- 發版時在結構與全文之後執行（Publish-IIS.ps1 放進套件的 database/release/，見 docs/operations/database-setup.md）；整合測試的範本資料庫也執行這一份。
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

-- 只放可授予的功能（頁面與動作），ID 與 backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs 一致（FeatureCatalogSeedTests 雙向比對）。
-- 網址、預設名稱與圖示由程式目錄提供；Name 為 NULL 代表使用目錄預設名稱，只有管理員改名時才寫入覆寫。選單位置在下方的 NavigationItems。
MERGE [accesscontrol].[Features] AS t
USING (VALUES
    (N'dashboard'),
    (N'chat'),
    (N'projects'),
    (N'files'),
    (N'knowledge'),
    (N'knowledge.retrieval'),
    (N'artifacts'),
    (N'sharing'),
    (N'jobs'),
    (N'repositories'),
    (N'attendance.records'),
    (N'attendance.cards'),
    (N'attendance.imports'),
    (N'attendance.calendar'),
    (N'identity.users'),
    (N'access-control.roles'),
    (N'access-control.groups'),
    (N'access-control.features'),
    (N'access-control.navigation'),
    (N'organization'),
    (N'operations.usage'),
    (N'billing.prices'),
    (N'monitoring'),
    (N'audit'),
    (N'audit.usage'),
    (N'diagnostics.logs'),
    -- 動作：只是授權，不出現在選單。
    (N'identity.users.import'),
    (N'identity.users.sync'),
    (N'diagnostics.logs.detail'),
    (N'diagnostics.logs.export'),
    (N'attendance.cards.maintain'),
    (N'attendance.imports.reimport'),
    (N'attendance.calendar.maintain'),
    (N'organization.import'),
    (N'mail.messages'),
    (N'mail.messages.resend'),
    (N'quality'),
    (N'quality.retrieval'),
    (N'design')
) AS s ([Id])
ON t.[Id] = s.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Name], [Enabled]) VALUES (s.[Id], NULL, 1);

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
    (N'administrators', N'平台管理'),
    (N'hr', N'人事')
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
-- Scope 是所有成員的資料範圍（ADR 0017）：目錄有宣告範圍的功能必須給一個它支援的範圍，其他功能為 NULL；
-- ManagerScope 是主管另可看的範圍（ADR 0022），只能給目錄有宣告主管範圍的功能，NULL＝不另外（FeatureCatalogSeedTests 檢查）。
-- administrators 擁有目錄的每個功能（頁面與動作），有範圍的給宣告的最寬範圍，依目錄順序列出；目錄新增功能時一併加在這裡（FeatureCatalogSeedTests 檢查）。
MERGE [accesscontrol].[RoleGroupFeatures] AS t
USING (VALUES
    (N'workspace', N'dashboard', NULL, NULL),
    (N'workspace', N'chat', NULL, NULL),
    (N'workspace', N'projects', NULL, NULL),
    (N'workspace', N'files', NULL, NULL),
    (N'workspace', N'knowledge', NULL, NULL),
    (N'workspace', N'artifacts', NULL, NULL),
    (N'workspace', N'sharing', NULL, NULL),
    (N'workspace', N'jobs', NULL, NULL),
    (N'workspace', N'repositories', NULL, NULL),
    -- 出勤紀錄的預設範圍：每位成員看自己的（ADR 0018）；組織勾選為主管的人另外看得到所管單位及下層（ADR 0022），不必另建主管群組。
    (N'workspace', N'attendance.records', N'own', N'managed-tree'),
    (N'workspace', N'quality', NULL, NULL),
    (N'workspace', N'quality.retrieval', NULL, NULL),
    (N'administrators', N'dashboard', NULL, NULL),
    (N'administrators', N'chat', NULL, NULL),
    (N'administrators', N'projects', NULL, NULL),
    (N'administrators', N'files', NULL, NULL),
    (N'administrators', N'knowledge', NULL, NULL),
    (N'administrators', N'knowledge.retrieval', NULL, NULL),
    (N'administrators', N'artifacts', NULL, NULL),
    (N'administrators', N'sharing', NULL, NULL),
    (N'administrators', N'jobs', NULL, NULL),
    (N'administrators', N'repositories', NULL, NULL),
    (N'administrators', N'attendance.records', N'all', NULL),
    (N'administrators', N'attendance.cards', NULL, NULL),
    (N'administrators', N'attendance.cards.maintain', NULL, NULL),
    (N'administrators', N'attendance.imports', NULL, NULL),
    (N'administrators', N'attendance.imports.reimport', NULL, NULL),
    (N'administrators', N'attendance.calendar', NULL, NULL),
    (N'administrators', N'attendance.calendar.maintain', NULL, NULL),
    (N'administrators', N'identity.users', NULL, NULL),
    (N'administrators', N'identity.users.import', NULL, NULL),
    (N'administrators', N'identity.users.sync', NULL, NULL),
    (N'administrators', N'access-control.roles', NULL, NULL),
    (N'administrators', N'access-control.groups', NULL, NULL),
    (N'administrators', N'access-control.features', NULL, NULL),
    (N'administrators', N'access-control.navigation', NULL, NULL),
    (N'administrators', N'organization', NULL, NULL),
    (N'administrators', N'organization.import', NULL, NULL),
    (N'administrators', N'operations.usage', NULL, NULL),
    (N'administrators', N'billing.prices', NULL, NULL),
    (N'administrators', N'monitoring', NULL, NULL),
    (N'administrators', N'audit', N'all', NULL),
    (N'administrators', N'audit.usage', N'all', NULL),
    (N'administrators', N'diagnostics.logs', NULL, NULL),
    (N'administrators', N'diagnostics.logs.detail', NULL, NULL),
    (N'administrators', N'diagnostics.logs.export', NULL, NULL),
    (N'administrators', N'mail.messages', NULL, NULL),
    (N'administrators', N'mail.messages.resend', NULL, NULL),
    (N'administrators', N'quality', NULL, NULL),
    (N'administrators', N'quality.retrieval', NULL, NULL),
    (N'administrators', N'design', NULL, NULL),
    -- HR：出勤的管理頁與全公司的出勤紀錄，不含使用者管理（ADR 0018）；與 administrators 分開，HR 不必是管理員。成員由管理員加入，Seed 不放任何成員。
    (N'hr', N'attendance.records', N'all', NULL),
    (N'hr', N'attendance.cards', NULL, NULL),
    (N'hr', N'attendance.cards.maintain', NULL, NULL),
    (N'hr', N'attendance.imports', NULL, NULL),
    (N'hr', N'attendance.imports.reimport', NULL, NULL),
    (N'hr', N'attendance.calendar', NULL, NULL),
    (N'hr', N'attendance.calendar.maintain', NULL, NULL)
) AS s ([GroupId], [FeatureId], [Scope], [ManagerScope])
ON t.[GroupId] = s.[GroupId] AND t.[FeatureId] = s.[FeatureId]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([GroupId], [FeatureId], [Scope], [ManagerScope]) VALUES (s.[GroupId], s.[FeatureId], s.[Scope], s.[ManagerScope]);

-- 預設選單樹。節點 Id 是可讀代稱（<分群>.<功能 ID 的點換成連字號>），同層 SortOrder 間隔 10；page 的名稱與圖示取自程式目錄，所以 Name、Icon 為 NULL。
-- 系統管理的頁面一律放進子分群，不直接掛在 system 底下：分群混著散頁時，圖示格狀會在托盤之間夾出零散的格子。
-- 目錄每個 Page 至少要放一次，新頁面才不會預設看不到（FeatureCatalogSeedTests 檢查）；動作與閱讀器不放進選單。
DECLARE @navigation TABLE (
    [Id]        NVARCHAR (64)  NOT NULL PRIMARY KEY,
    [ParentId]  NVARCHAR (64)  NULL,
    [Kind]      NVARCHAR (10)  NOT NULL,
    [FeatureId] NVARCHAR (64)  NULL,
    [Name]      NVARCHAR (120) NULL,
    [SortOrder] INT            NOT NULL);
INSERT INTO @navigation ([Id], [ParentId], [Kind], [FeatureId], [Name], [SortOrder])
VALUES
    (N'dashboard', NULL, N'page', N'dashboard', NULL, 10),
    (N'work', NULL, N'group', NULL, N'AI 工作台', 20),
    (N'work.chat', N'work', N'page', N'chat', NULL, 10),
    (N'work.projects', N'work', N'page', N'projects', NULL, 20),
    (N'work.files', N'work', N'page', N'files', NULL, 30),
    (N'work.knowledge', N'work', N'page', N'knowledge', NULL, 40),
    (N'work.artifacts', N'work', N'page', N'artifacts', NULL, 50),
    (N'collaboration', NULL, N'group', NULL, N'協作', 30),
    (N'collaboration.sharing', N'collaboration', N'page', N'sharing', NULL, 10),
    (N'collaboration.jobs', N'collaboration', N'page', N'jobs', NULL, 20),
    (N'collaboration.repositories', N'collaboration', N'page', N'repositories', NULL, 30),
    (N'office', NULL, N'group', NULL, N'公司事務', 35),
    (N'office.attendance-records', N'office', N'page', N'attendance.records', NULL, 10),
    (N'system', NULL, N'group', NULL, N'系統管理', 40),
    (N'system.access', N'system', N'group', NULL, N'身分與權限', 10),
    (N'system.access.identity-users', N'system.access', N'page', N'identity.users', NULL, 10),
    (N'system.access.organization', N'system.access', N'page', N'organization', NULL, 15),
    (N'system.access.access-control-roles', N'system.access', N'page', N'access-control.roles', NULL, 20),
    (N'system.access.access-control-groups', N'system.access', N'page', N'access-control.groups', NULL, 30),
    (N'system.access.access-control-features', N'system.access', N'page', N'access-control.features', NULL, 40),
    (N'system.access.access-control-navigation', N'system.access', N'page', N'access-control.navigation', NULL, 50),
    (N'system.usage', N'system', N'group', NULL, N'用量與計費', 20),
    (N'system.usage.operations-usage', N'system.usage', N'page', N'operations.usage', NULL, 10),
    (N'system.usage.billing-prices', N'system.usage', N'page', N'billing.prices', NULL, 20),
    (N'system.data', N'system', N'group', NULL, N'資料與檢索', 30),
    (N'system.data.knowledge-retrieval', N'system.data', N'page', N'knowledge.retrieval', NULL, 20),
    (N'system.observability', N'system', N'group', NULL, N'監控與紀錄', 40),
    (N'system.observability.monitoring', N'system.observability', N'page', N'monitoring', NULL, 10),
    (N'system.observability.diagnostics-logs', N'system.observability', N'page', N'diagnostics.logs', NULL, 20),
    (N'system.observability.audit', N'system.observability', N'page', N'audit', NULL, 30),
    (N'system.observability.audit-usage', N'system.observability', N'page', N'audit.usage', NULL, 35),
    (N'system.observability.mail-messages', N'system.observability', N'page', N'mail.messages', NULL, 40),
    (N'system.evaluation', N'system', N'group', NULL, N'評測與開發', 50),
    (N'system.evaluation.quality', N'system.evaluation', N'page', N'quality', NULL, 10),
    (N'system.evaluation.quality-retrieval', N'system.evaluation', N'page', N'quality.retrieval', NULL, 20),
    (N'system.evaluation.design', N'system.evaluation', N'page', N'design', NULL, 30),
    (N'system.hr', N'system', N'group', NULL, N'人事與出勤', 60),
    (N'system.hr.attendance-cards', N'system.hr', N'page', N'attendance.cards', NULL, 10),
    (N'system.hr.attendance-imports', N'system.hr', N'page', N'attendance.imports', NULL, 20),
    (N'system.hr.attendance-calendar', N'system.hr', N'page', N'attendance.calendar', NULL, 30);

-- 一層一層寫入：每輪只寫父節點已存在的列，沒有新列就停（外鍵要求父節點先存在）。
-- 既有環境的選單可能被管理員改過：上層分群已刪除，或同層已有同一頁面（唯一索引會擋），這些預設節點直接略過，Seed 不因此失敗。
-- 移動過的預設節點以 Id 比對、保持原位；管理員刪除、而上層仍在的預設節點會再加回來（與其他表的 insert-only 慣例相同）。
WHILE 1 = 1
BEGIN
    MERGE [accesscontrol].[NavigationItems] AS t
    USING (
        SELECT n.[Id], n.[ParentId], n.[Kind], n.[FeatureId], n.[Name], n.[SortOrder]
        FROM @navigation AS n
        WHERE (n.[ParentId] IS NULL OR EXISTS (SELECT 1 FROM [accesscontrol].[NavigationItems] AS p WHERE p.[Id] = n.[ParentId]))
          AND NOT EXISTS (
              SELECT 1 FROM [accesscontrol].[NavigationItems] AS d
              WHERE d.[FeatureId] = n.[FeatureId]
                AND (d.[ParentId] = n.[ParentId] OR (d.[ParentId] IS NULL AND n.[ParentId] IS NULL)))
    ) AS s
    ON t.[Id] = s.[Id]
    WHEN NOT MATCHED BY TARGET THEN
        INSERT ([Id], [ParentId], [Kind], [FeatureId], [Name], [Icon], [SortOrder])
        VALUES (s.[Id], s.[ParentId], s.[Kind], s.[FeatureId], s.[Name], NULL, s.[SortOrder]);
    IF @@ROWCOUNT = 0 BREAK;
END;

COMMIT;
