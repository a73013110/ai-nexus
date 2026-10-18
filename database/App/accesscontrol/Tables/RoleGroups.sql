CREATE TABLE [accesscontrol].[RoleGroups] (
    [Id]      NVARCHAR (64)  NOT NULL,
    [Name]    NVARCHAR (120) NOT NULL,
    [Enabled] BIT            NOT NULL,
    CONSTRAINT [PK_RoleGroups] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'角色所加入的功能群組，集中管理功能及模型政策。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroups';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否啟用；停用不刪除歷史資料。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroups', @level2type = N'COLUMN', @level2name = N'Enabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroups', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的顯示名稱。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'RoleGroups', @level2type = N'COLUMN', @level2name = N'Name';


GO

