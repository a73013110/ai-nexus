CREATE TABLE [websearch].[WebSearches] (
    [Id]             UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]        UNIQUEIDENTIFIER   NOT NULL,
    [ConversationId] UNIQUEIDENTIFIER   NOT NULL,
    [IdempotencyKey] NVARCHAR (80)      NOT NULL,
    [RequestHash]    NVARCHAR (64)      NOT NULL,
    [Status]         NVARCHAR (16)      NOT NULL,
    [ResultsJson]    NVARCHAR (MAX)     NOT NULL,
    [CreatedAt]      DATETIMEOFFSET (7) NOT NULL,
    [RunId]          UNIQUEIDENTIFIER   NULL,
    CONSTRAINT [PK_WebSearches] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_WebSearches_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_WebSearches_OwnerId_CreatedAt]
    ON [websearch].[WebSearches]([OwnerId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_WebSearches_RunId]
    ON [websearch].[WebSearches]([RunId] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_WebSearches_OwnerId_IdempotencyKey]
    ON [websearch].[WebSearches]([OwnerId] ASC, [IdempotencyKey] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者明確啟用的網路搜尋、冪等識別、結果與費用。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯對話的識別碼。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'ConversationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'擁有者範圍內的冪等請求識別，避免重試重複處理。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'IdempotencyKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'請求內容指紋，用於辨識冪等識別碼衝突。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'RequestHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'搜尋結果的 JSON 快照。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'ResultsJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'觸發搜尋的 GenerationRuns 識別碼。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'RunId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務執行狀態。', @level0type = N'SCHEMA', @level0name = N'websearch', @level1type = N'TABLE', @level1name = N'WebSearches', @level2type = N'COLUMN', @level2name = N'Status';


GO

