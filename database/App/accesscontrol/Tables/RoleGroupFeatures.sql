CREATE TABLE [accesscontrol].[RoleGroupFeatures] (
    [GroupId]      NVARCHAR (64) NOT NULL,
    [FeatureId]    NVARCHAR (64) NOT NULL,
    [Scope]        NVARCHAR (16) NULL,
    [ManagerScope] NVARCHAR (16) NULL,
    CONSTRAINT [PK_RoleGroupFeatures] PRIMARY KEY CLUSTERED ([GroupId] ASC, [FeatureId] ASC),
    CONSTRAINT [ck_RoleGroupFeatures_ManagerScope] CHECK ([ManagerScope]=N'managed-tree' OR [ManagerScope]=N'managed'),
    CONSTRAINT [ck_RoleGroupFeatures_Scope] CHECK ([Scope]=N'all' OR [Scope]=N'unit-tree' OR [Scope]=N'unit' OR [Scope]=N'assigned' OR [Scope]=N'own'),
    CONSTRAINT [FK_RoleGroupFeatures_Features_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [accesscontrol].[Features] ([Id]),
    CONSTRAINT [FK_RoleGroupFeatures_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [accesscontrol].[RoleGroups] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_RoleGroupFeatures_FeatureId]
    ON [accesscontrol].[RoleGroupFeatures]([FeatureId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'功能群組與功能的授權關聯；有效功能取聯集。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroupFeatures';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯功能的識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroupFeatures', @level2type = N'COLUMN', @level2name = N'FeatureId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯功能群組的識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroupFeatures', @level2type = N'COLUMN', @level2name = N'GroupId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'主管另可看的範圍（managed、managed-tree）：組織勾選為主管的成員，另外看得到所管單位（及下層）的資料，與 Scope 以 OR 合併；null 表示不另外。功能支援哪些由程式的功能目錄宣告，寫入時檢查。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroupFeatures', @level2type = N'COLUMN', @level2name = N'ManagerScope';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料範圍（own、assigned、unit、unit-tree、all），一筆授權一個；null 表示功能沒有資料列概念。功能支援哪些範圍由程式的功能目錄宣告，寫入時檢查。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroupFeatures', @level2type = N'COLUMN', @level2name = N'Scope';


GO

