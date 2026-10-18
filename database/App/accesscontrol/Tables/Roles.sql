CREATE TABLE [accesscontrol].[Roles] (
    [Id]      NVARCHAR (64)  NOT NULL,
    [Name]    NVARCHAR (120) NOT NULL,
    [Enabled] BIT            NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'可分派給使用者的角色；停用後不再提供有效授權。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Roles';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否啟用；停用不刪除歷史資料。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Roles', @level2type = N'COLUMN', @level2name = N'Enabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Roles', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的顯示名稱。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Roles', @level2type = N'COLUMN', @level2name = N'Name';


GO

