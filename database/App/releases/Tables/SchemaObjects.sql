CREATE TABLE [releases].[SchemaObjects] (
    [SchemaName] [sysname]      NOT NULL,
    [Name]       [sysname]      NOT NULL,
    [Type]       NVARCHAR (16)  NOT NULL,
    [Hash]       BINARY (32)    NOT NULL,
    [Definition] NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [pk_SchemaObjects] PRIMARY KEY CLUSTERED ([SchemaName] ASC, [Name] ASC)
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一版應有的結構快照，每個物件一列；由 update.sql 整批替換，內容取自發版時的演練庫（ADR 0028）。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'SchemaObjects';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'定義本文：程式物件為 sys.sql_modules.definition，資料表為欄位、索引與約束組成的固定格式；供差異比對顯示。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'SchemaObjects', @level2type = N'COLUMN', @level2name = N'Definition';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'正規化定義本文的 SHA2-256，由 releases.udf_SchemaObjects 計算；是否異動以它為準。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'SchemaObjects', @level2type = N'COLUMN', @level2name = N'Hash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'物件名稱；觸發程序與所屬資料表同 schema。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'SchemaObjects', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'物件所在的 schema。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'SchemaObjects', @level2type = N'COLUMN', @level2name = N'SchemaName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'物件種類：table、view、procedure、function、trigger、synonym。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'SchemaObjects', @level2type = N'COLUMN', @level2name = N'Type';


GO

