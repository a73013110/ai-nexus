CREATE TABLE [accesscontrol].[Features] (
    [Id]      NVARCHAR (64)  NOT NULL,
    [Name]    NVARCHAR (120) NULL,
    [Enabled] BIT            NOT NULL,
    CONSTRAINT [PK_Features] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'可授予的功能（頁面與動作）：啟用狀態與名稱覆寫；網址、預設名稱與圖示由程式的 FeatureCatalog 提供。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否啟用；停用不刪除歷史資料。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Enabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'管理員覆寫的顯示名稱；null 時使用程式目錄的預設名稱。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'Features', @level2type = N'COLUMN', @level2name = N'Name';


GO

