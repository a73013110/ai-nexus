CREATE TABLE [administration].[AdministratorBootstraps] (
    [UserId]    UNIQUEIDENTIFIER   NOT NULL,
    [GrantedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_AdministratorBootstraps] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [FK_AdministratorBootstraps_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'管理者首次啟動授權的永久標記，防止撤銷後重新授權。', @level0type = N'SCHEMA', @level0name = N'administration', @level1type = N'TABLE', @level1name = N'AdministratorBootstraps';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'角色或資源授權建立時間。', @level0type = N'SCHEMA', @level0name = N'administration', @level1type = N'TABLE', @level1name = N'AdministratorBootstraps', @level2type = N'COLUMN', @level2name = N'GrantedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯使用者的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'administration', @level1type = N'TABLE', @level1name = N'AdministratorBootstraps', @level2type = N'COLUMN', @level2name = N'UserId';


GO

