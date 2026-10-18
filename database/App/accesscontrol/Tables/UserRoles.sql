CREATE TABLE [accesscontrol].[UserRoles] (
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [RoleId] NVARCHAR (64)    NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [accesscontrol].[Roles] ([Id]),
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_UserRoles_RoleId]
    ON [accesscontrol].[UserRoles]([RoleId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者與角色的分派關聯。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'UserRoles';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯角色的識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'UserRoles', @level2type = N'COLUMN', @level2name = N'RoleId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯使用者的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'UserRoles', @level2type = N'COLUMN', @level2name = N'UserId';


GO

