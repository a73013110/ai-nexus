CREATE TABLE [conversations].[Messages] (
    [Id]             UNIQUEIDENTIFIER   NOT NULL,
    [ConversationId] UNIQUEIDENTIFIER   NOT NULL,
    [ParentId]       UNIQUEIDENTIFIER   NULL,
    [Role]           NVARCHAR (16)      NOT NULL,
    [Content]        NVARCHAR (MAX)     NOT NULL,
    [Status]         NVARCHAR (16)      NOT NULL,
    [CreatedAt]      DATETIMEOFFSET (7) NOT NULL,
    [RunId]          UNIQUEIDENTIFIER   NULL,
    [ModelId]        NVARCHAR (160)     NULL,
    [IssueCode]      NVARCHAR (40)      NULL,
    [ErrorCode]      NVARCHAR (80)      NULL,
    CONSTRAINT [PK_Messages] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]),
    CONSTRAINT [FK_Messages_Messages_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [conversations].[Messages] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Messages_ConversationId_CreatedAt]
    ON [conversations].[Messages]([ConversationId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_Messages_ParentId]
    ON [conversations].[Messages]([ParentId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對話訊息樹；提問、回答、重新生成與編輯保留各版本。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'訊息文字內容。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'Content';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯對話的識別碼。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'ConversationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對外安全的錯誤代碼，不含密碼或完整例外。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准模型的內部識別碼。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'ModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'父訊息識別碼；編輯與重新生成形成分支樹。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'ParentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'訊息角色：user 或 assistant。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'Role';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'產生此訊息的 GenerationRuns 識別碼。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'RunId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務執行狀態。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Messages', @level2type = N'COLUMN', @level2name = N'Status';


GO

