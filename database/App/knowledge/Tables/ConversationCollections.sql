CREATE TABLE [knowledge].[ConversationCollections] (
    [ConversationId] UNIQUEIDENTIFIER NOT NULL,
    [CollectionId]   UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_ConversationCollections] PRIMARY KEY CLUSTERED ([ConversationId] ASC, [CollectionId] ASC),
    CONSTRAINT [FK_ConversationCollections_Collections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [knowledge].[Collections] ([Id]),
    CONSTRAINT [FK_ConversationCollections_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_ConversationCollections_CollectionId]
    ON [knowledge].[ConversationCollections]([CollectionId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對話選定的知識庫來源關聯。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ConversationCollections';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯知識庫的識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ConversationCollections', @level2type = N'COLUMN', @level2name = N'CollectionId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯對話的識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ConversationCollections', @level2type = N'COLUMN', @level2name = N'ConversationId';


GO

