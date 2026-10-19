CREATE TABLE [accesscontrol].[NavigationItems] (
    [Id]           NVARCHAR (64)   NOT NULL,
    [ParentId]     NVARCHAR (64)   NULL,
    [Kind]         NVARCHAR (10)   NOT NULL,
    [FeatureId]    NVARCHAR (64)   NULL,
    [Name]         NVARCHAR (120)  NULL,
    [Url]          NVARCHAR (2048) NULL,
    [OpenInNewTab] BIT             CONSTRAINT [df_NavigationItems_OpenInNewTab] DEFAULT ((1)) NOT NULL,
    [Icon]         NVARCHAR (50)   NULL,
    [SortOrder]    INT             NOT NULL,
    CONSTRAINT [pk_NavigationItems] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_NavigationItems_Kind] CHECK ([Kind]=N'link' OR [Kind]=N'page' OR [Kind]=N'group'),
    CONSTRAINT [ck_NavigationItems_KindColumns] CHECK ([Kind]=N'group' AND [FeatureId] IS NULL AND [Name] IS NOT NULL AND [Url] IS NULL OR [Kind]=N'page' AND [FeatureId] IS NOT NULL AND [Name] IS NULL AND [Url] IS NULL AND [Icon] IS NULL OR [Kind]=N'link' AND [FeatureId] IS NULL AND [Name] IS NOT NULL AND [Url] IS NOT NULL),
    CONSTRAINT [ck_NavigationItems_SortOrder] CHECK ([SortOrder]>=(0) AND [SortOrder]<=(10000)),
    CONSTRAINT [fk_NavigationItems_Features] FOREIGN KEY ([FeatureId]) REFERENCES [accesscontrol].[Features] ([Id]),
    CONSTRAINT [fk_NavigationItems_NavigationItems] FOREIGN KEY ([ParentId]) REFERENCES [accesscontrol].[NavigationItems] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_NavigationItems_FeatureId]
    ON [accesscontrol].[NavigationItems]([FeatureId] ASC);


GO

CREATE NONCLUSTERED INDEX [ix_NavigationItems_ParentId]
    ON [accesscontrol].[NavigationItems]([ParentId] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_NavigationItems_ParentId_FeatureId]
    ON [accesscontrol].[NavigationItems]([ParentId] ASC, [FeatureId] ASC) WHERE ([FeatureId] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'工作區選單的節點：分群、功能頁面與外部連結；只決定入口的排列與外部連結的可見範圍，不影響功能授權。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'page 節點指向的功能；其他種類為 null。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'FeatureId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'group、link 的圖示名稱，可空；page 一律用程式目錄的圖示，為 null。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'Icon';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼；內建節點為可讀代稱，管理端新增的由伺服器產生 GUID。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'節點種類：group 分群、page 功能頁面、link 外部連結。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'group、link 的顯示名稱；page 為 null，名稱取功能的名稱覆寫或程式目錄的預設名稱。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部連結是否在新分頁開啟；只對 link 有意義。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'OpenInNewTab';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'上層分群；null 表示位於選單最上層。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'ParentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一層內的顯示順序（0～10000）；數值越小越前。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'SortOrder';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'link 的外部網址（https，主機須在 AccessControl:NavigationLinkHosts 允許清單內）；其他種類為 null。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItems', @level2type = N'COLUMN', @level2name = N'Url';


GO

