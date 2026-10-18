CREATE TABLE [accesscontrol].[RoleGroupFeatures] (
    [GroupId]   NVARCHAR (64) NOT NULL,
    [FeatureId] NVARCHAR (64) NOT NULL,
    CONSTRAINT [PK_RoleGroupFeatures] PRIMARY KEY CLUSTERED ([GroupId] ASC, [FeatureId] ASC),
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

