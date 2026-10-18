CREATE TABLE [knowledge].[MessageCitations] (
    [MessageId]  UNIQUEIDENTIFIER NOT NULL,
    [Number]     INT              NOT NULL,
    [DocumentId] UNIQUEIDENTIFIER NOT NULL,
    [PageNumber] INT              NOT NULL,
    [EndPage]    INT              NOT NULL,
    [Title]      NVARCHAR (180)   NOT NULL,
    [Excerpt]    NVARCHAR (800)   NOT NULL,
    CONSTRAINT [PK_MessageCitations] PRIMARY KEY CLUSTERED ([MessageId] ASC, [Number] ASC),
    CONSTRAINT [FK_MessageCitations_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [knowledge].[Documents] ([Id]),
    CONSTRAINT [FK_MessageCitations_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_MessageCitations_DocumentId]
    ON [knowledge].[MessageCitations]([DocumentId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'回答生成當時的知識引用、文件頁碼與摘要快照。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'MessageCitations';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯知識文件的識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'MessageCitations', @level2type = N'COLUMN', @level2name = N'DocumentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'片段結束的原始文件頁碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'MessageCitations', @level2type = N'COLUMN', @level2name = N'EndPage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'檢索或引用時保存的文字摘要。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'MessageCitations', @level2type = N'COLUMN', @level2name = N'Excerpt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯訊息的識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'MessageCitations', @level2type = N'COLUMN', @level2name = N'MessageId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'回答引用的順序編號。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'MessageCitations', @level2type = N'COLUMN', @level2name = N'Number';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文件頁碼，從 1 開始。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'MessageCitations', @level2type = N'COLUMN', @level2name = N'PageNumber';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面顯示標題。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'MessageCitations', @level2type = N'COLUMN', @level2name = N'Title';


GO

