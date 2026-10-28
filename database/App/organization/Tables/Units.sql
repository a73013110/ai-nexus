CREATE TABLE [organization].[Units] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [ParentId]  UNIQUEIDENTIFIER NULL,
    [Name]      NVARCHAR (120)   NOT NULL,
    [Enabled]   BIT              CONSTRAINT [df_Units_Enabled] DEFAULT ((1)) NOT NULL,
    [SortOrder] INT              NOT NULL,
    [GdDepNo]   INT              NULL,
    [HeadRank]  TINYINT          NULL,
    CONSTRAINT [pk_Units] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_Units_HeadRank] CHECK ([HeadRank] IS NULL OR [HeadRank]>=(1) AND [HeadRank]<=(9) AND [ParentId] IS NOT NULL),
    CONSTRAINT [ck_Units_Name] CHECK (len(ltrim(rtrim([Name])))>(0)),
    CONSTRAINT [ck_Units_SortOrder] CHECK ([SortOrder]>=(0) AND [SortOrder]<=(10000)),
    CONSTRAINT [fk_Units_Units] FOREIGN KEY ([ParentId]) REFERENCES [organization].[Units] ([Id])
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_Units_GdDepNo]
    ON [organization].[Units]([GdDepNo] ASC) WHERE ([GdDepNo] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_Units_ParentId_HeadRank]
    ON [organization].[Units]([ParentId] ASC, [HeadRank] ASC) WHERE ([HeadRank] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_Units_ParentId_Name]
    ON [organization].[Units]([ParentId] ASC, [Name] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'組織單位（部門）樹；只用來計算資料範圍與主管關係，不授予功能。停用的單位連同下層都不參與範圍計算。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否啟用；停用時保留成員與歷史資料引用，但它與下層單位都不參與資料範圍。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'Enabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'來源公文系統（GD）部門的 DEP_NO；null 表示管理員手建。重複匯入以它對應同一個單位（唯一），GD 已沒有該部門時單位停用、保留此值。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'GdDepNo';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'首長單位的層級（1～9，數字越小越高）；null 表示一般單位。首長單位的主管管轄上層底下除更高層級首長單位以外的單位與其下層，上層自己不再勾選主管。同一個上層內不可重複，本地設定、GD 匯入不覆寫（上層被匯入改變時清除）。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'HeadRank';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼，由伺服器產生；業務資料寫入當時的單位時引用它。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'單位名稱；同一個上層底下不可重複。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'上層單位；null 表示最上層。不可成環、深度上限由程式檢查。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'ParentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一層內的顯示順序（0～10000）；數值越小越前。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'Units', @level2type = N'COLUMN', @level2name = N'SortOrder';


GO

