CREATE TABLE [accesscontrol].[RoleGroupRoles] (
    [RoleId]  NVARCHAR (64) NOT NULL,
    [GroupId] NVARCHAR (64) NOT NULL,
    CONSTRAINT [PK_RoleGroupRoles] PRIMARY KEY CLUSTERED ([RoleId] ASC, [GroupId] ASC),
    CONSTRAINT [FK_RoleGroupRoles_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [accesscontrol].[RoleGroups] ([Id]),
    CONSTRAINT [FK_RoleGroupRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [accesscontrol].[Roles] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_RoleGroupRoles_GroupId]
    ON [accesscontrol].[RoleGroupRoles]([GroupId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'角色與功能群組的授權關聯。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroupRoles';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯功能群組的識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroupRoles', @level2type = N'COLUMN', @level2name = N'GroupId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯角色的識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroupRoles', @level2type = N'COLUMN', @level2name = N'RoleId';


GO

