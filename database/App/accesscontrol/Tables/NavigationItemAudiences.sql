CREATE TABLE [accesscontrol].[NavigationItemAudiences] (
    [NavigationItemId] NVARCHAR (64) NOT NULL,
    [GroupId]          NVARCHAR (64) NOT NULL,
    CONSTRAINT [pk_NavigationItemAudiences] PRIMARY KEY CLUSTERED ([NavigationItemId] ASC, [GroupId] ASC),
    CONSTRAINT [fk_NavigationItemAudiences_NavigationItems] FOREIGN KEY ([NavigationItemId]) REFERENCES [accesscontrol].[NavigationItems] ([Id]),
    CONSTRAINT [fk_NavigationItemAudiences_RoleGroups] FOREIGN KEY ([GroupId]) REFERENCES [accesscontrol].[RoleGroups] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_NavigationItemAudiences_GroupId]
    ON [accesscontrol].[NavigationItemAudiences]([GroupId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部連結的可見功能群組；連結沒有任何列時所有登入者可見。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItemAudiences';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯功能群組的識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItemAudiences', @level2type = N'COLUMN', @level2name = N'GroupId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯選單節點的識別碼；只能是 link 節點。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'NavigationItemAudiences', @level2type = N'COLUMN', @level2name = N'NavigationItemId';


GO

