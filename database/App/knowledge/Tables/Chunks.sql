CREATE TABLE [knowledge].[Chunks] (
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [SearchId]      INT              IDENTITY (1, 1) NOT NULL,
    [DocumentId]    UNIQUEIDENTIFIER NOT NULL,
    [StartPage]     INT              NOT NULL,
    [EndPage]       INT              NOT NULL,
    [Ordinal]       INT              NOT NULL,
    [HeadingPath]   NVARCHAR (400)   NOT NULL,
    [Text]          NVARCHAR (4000)  NOT NULL,
    [ContentHash]   BINARY (32)      NOT NULL,
    [TokenEstimate] INT              NOT NULL,
    CONSTRAINT [PK_Chunks] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Chunks_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [knowledge].[Documents] ([Id]) ON DELETE CASCADE
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Chunks_DocumentId_Ordinal]
    ON [knowledge].[Chunks]([DocumentId] ASC, [Ordinal] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Chunks_SearchId]
    ON [knowledge].[Chunks]([SearchId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結構化檢索片段、頁碼與內容指紋；查詢先套用資料 ACL。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'ContentHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯知識文件的識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'DocumentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'片段結束的原始文件頁碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'EndPage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'由標題階層組成的結構路徑。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'HeadingPath';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一父物件內的呈現順序。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'Ordinal';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'全文索引使用的整數唯一鍵；保留未來 ANN 映射。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'SearchId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'片段開始的原始文件頁碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'StartPage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文件頁面／片段的擷取文字。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'Text';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'依 CJK 與其他字元比例估算的片段 token 數。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Chunks', @level2type = N'COLUMN', @level2name = N'TokenEstimate';


GO

