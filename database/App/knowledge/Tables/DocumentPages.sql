CREATE TABLE [knowledge].[DocumentPages] (
    [DocumentId]  UNIQUEIDENTIFIER NOT NULL,
    [PageNumber]  INT              NOT NULL,
    [Text]        NVARCHAR (MAX)   NOT NULL,
    [Extraction]  NVARCHAR (16)    NOT NULL,
    [NeedsReview] BIT              NOT NULL,
    CONSTRAINT [PK_DocumentPages] PRIMARY KEY CLUSTERED ([DocumentId] ASC, [PageNumber] ASC),
    CONSTRAINT [FK_DocumentPages_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [knowledge].[Documents] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文件逐頁擷取的文字、頁碼與 OCR 結果。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'DocumentPages';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯知識文件的識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'DocumentPages', @level2type = N'COLUMN', @level2name = N'DocumentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'附件文字擷取方法或結果。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'DocumentPages', @level2type = N'COLUMN', @level2name = N'Extraction';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此評測結果是否需要人工覆核。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'DocumentPages', @level2type = N'COLUMN', @level2name = N'NeedsReview';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文件頁碼，從 1 開始。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'DocumentPages', @level2type = N'COLUMN', @level2name = N'PageNumber';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文件頁面／片段的擷取文字。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'DocumentPages', @level2type = N'COLUMN', @level2name = N'Text';


GO

