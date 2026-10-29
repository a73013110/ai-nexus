CREATE TABLE [releases].[IgnoredObjects] (
    [Pattern] NVARCHAR (300) NOT NULL,
    CONSTRAINT [pk_IgnoredObjects] PRIMARY KEY CLUSTERED ([Pattern] ASC)
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結構異動偵測略過的物件：database/App/export-exclusions.txt 轉成的 LIKE 樣式（schema.name），由 update.sql 整批替換。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'IgnoredObjects';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'比對 schema.name 的 LIKE 樣式；方括號、百分比與底線已跳脫，萬用字元 * ? 轉成 % _。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'IgnoredObjects', @level2type = N'COLUMN', @level2name = N'Pattern';


GO

