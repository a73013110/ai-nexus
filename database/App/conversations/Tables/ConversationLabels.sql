CREATE TABLE [conversations].[ConversationLabels] (
    [ConversationId] UNIQUEIDENTIFIER NOT NULL,
    [Name]           NVARCHAR (24)    NOT NULL,
    CONSTRAINT [PK_ConversationLabels] PRIMARY KEY CLUSTERED ([ConversationId] ASC, [Name] ASC),
    CONSTRAINT [FK_ConversationLabels_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者對話的分類標籤。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'ConversationLabels';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯對話的識別碼。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'ConversationLabels', @level2type = N'COLUMN', @level2name = N'ConversationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的顯示名稱。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'ConversationLabels', @level2type = N'COLUMN', @level2name = N'Name';


GO

