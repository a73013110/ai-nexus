CREATE TABLE [accesscontrol].[Features] (
    [Id]        NVARCHAR (64)  NOT NULL,
    [Name]      NVARCHAR (120) NULL,
    [SortOrder] INT            NOT NULL,
    [Enabled]   BIT            NOT NULL,
    [GroupId]   NVARCHAR (64)  NULL,
    CONSTRAINT [PK_Features] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Features_NavigationGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [accesscontrol].[NavigationGroups] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Features_GroupId]
    ON [accesscontrol].[Features]([GroupId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'授權用的功能列：啟用狀態、名稱覆寫與現行選單分群；頁面網址、預設名稱與圖示由程式的 FeatureCatalog 提供。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否啟用；停用不刪除歷史資料。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Enabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'所在的選單分群；null 表示位於選單最上層。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'GroupId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'管理員覆寫的顯示名稱；null 時使用程式目錄的預設名稱。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一選單分群內的顯示順序；數值越小越前。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'SortOrder';


GO

