CREATE TABLE [collaboration].[ResourceGroups] (
    [ResourceId] UNIQUEIDENTIFIER NOT NULL,
    [GroupId]    NVARCHAR (64)    NOT NULL,
    CONSTRAINT [PK_ResourceGroups] PRIMARY KEY CLUSTERED ([ResourceId] ASC, [GroupId] ASC),
    CONSTRAINT [FK_ResourceGroups_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ResourceGroups_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [accesscontrol].[RoleGroups] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ResourceGroups_GroupId]
    ON [collaboration].[ResourceGroups]([GroupId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資源對功能群組授予的唯讀權限。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'ResourceGroups';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯功能群組的識別碼。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'ResourceGroups', @level2type = N'COLUMN', @level2name = N'GroupId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯 Resources 的識別碼。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'ResourceGroups', @level2type = N'COLUMN', @level2name = N'ResourceId';


GO

