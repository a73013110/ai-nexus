CREATE TABLE [accesscontrol].[NavigationGroups] (
    [Id]        NVARCHAR (64)  NOT NULL,
    [ParentId]  NVARCHAR (64)  NULL,
    [Name]      NVARCHAR (120) NOT NULL,
    [SortOrder] INT            NOT NULL,
    CONSTRAINT [PK_NavigationGroups] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_NavigationGroups_NavigationGroups_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [accesscontrol].[NavigationGroups] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_NavigationGroups_ParentId]
    ON [accesscontrol].[NavigationGroups]([ParentId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'工作區選單的分群；可巢狀，只決定功能入口的排列，不影響授權。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationGroups';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationGroups', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的顯示名稱。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationGroups', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'上層分群；null 表示位於選單最上層。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationGroups', @level2type = N'COLUMN', @level2name = N'ParentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一層內的顯示順序；數值越小越前，同一層的功能排在分群之前。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationGroups', @level2type = N'COLUMN', @level2name = N'SortOrder';


GO

