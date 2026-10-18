CREATE TABLE [accesscontrol].[Features] (
    [Id]        NVARCHAR (64)  NOT NULL,
    [Name]      NVARCHAR (120) NOT NULL,
    [Route]     NVARCHAR (160) NOT NULL,
    [SortOrder] INT            NOT NULL,
    [Enabled]   BIT            NOT NULL,
    [GroupId]   NVARCHAR (64)  NULL,
    [Icon]      NVARCHAR (40)  DEFAULT (N'') NOT NULL,
    CONSTRAINT [PK_Features] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Features_NavigationGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [accesscontrol].[NavigationGroups] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Features_GroupId]
    ON [accesscontrol].[Features]([GroupId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模組註冊的功能入口、顯示名稱、路由、選單分群及啟用狀態。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否啟用；停用不刪除歷史資料。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Enabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'所在的選單分群；null 表示位於選單最上層。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'GroupId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'前端圖示名稱；未知名稱由前端改用預設圖示。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Icon';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的顯示名稱。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'功能入口的本站路由；API 授權仍由後端政策判定。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Route';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一選單分群內的顯示順序；數值越小越前。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'SortOrder';


GO

