CREATE TABLE [organization].[Units] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [ParentId]  UNIQUEIDENTIFIER NULL,
    [Name]      NVARCHAR (120)   NOT NULL,
    [Enabled]   BIT              CONSTRAINT [df_Units_Enabled] DEFAULT ((1)) NOT NULL,
    [SortOrder] INT              NOT NULL,
    CONSTRAINT [pk_Units] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_Units_Name] CHECK (len(ltrim(rtrim([Name])))>(0)),
    CONSTRAINT [ck_Units_SortOrder] CHECK ([SortOrder]>=(0) AND [SortOrder]<=(10000)),
    CONSTRAINT [fk_Units_Units] FOREIGN KEY ([ParentId]) REFERENCES [organization].[Units] ([Id])
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_Units_ParentId_Name]
    ON [organization].[Units]([ParentId] ASC, [Name] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'組織單位（部門）樹；只用來計算資料範圍與主管關係，不授予功能。停用的單位連同下層都不參與範圍計算。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否啟用；停用時保留成員與歷史資料引用，但它與下層單位都不參與資料範圍。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'Enabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼，由伺服器產生；業務資料寫入當時的單位時引用它。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'單位名稱；同一個上層底下不可重複。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'上層單位；null 表示最上層。不可成環、深度上限由程式檢查。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'ParentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一層內的顯示順序（0～10000）；數值越小越前。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'SortOrder';


GO

