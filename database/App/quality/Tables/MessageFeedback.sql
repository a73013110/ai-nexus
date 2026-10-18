CREATE TABLE [quality].[MessageFeedback] (
    [MessageId] UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]   UNIQUEIDENTIFIER   NOT NULL,
    [Rating]    INT                NOT NULL,
    [Reason]    NVARCHAR (24)      NOT NULL,
    [Note]      NVARCHAR (2000)    NOT NULL,
    [UpdatedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_MessageFeedback] PRIMARY KEY CLUSTERED ([MessageId] ASC),
    CONSTRAINT [FK_MessageFeedback_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [conversations].[Messages] ([Id]),
    CONSTRAINT [FK_MessageFeedback_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_MessageFeedback_OwnerId_UpdatedAt]
    ON [quality].[MessageFeedback]([OwnerId] ASC, [UpdatedAt] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者對 AI 回答的私人評分與意見。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'MessageFeedback';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯訊息的識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'MessageFeedback', @level2type = N'COLUMN', @level2name = N'MessageId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者提供的補充說明。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'MessageFeedback', @level2type = N'COLUMN', @level2name = N'Note';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'MessageFeedback', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者對回答的評分。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'MessageFeedback', @level2type = N'COLUMN', @level2name = N'Rating';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'回饋理由。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'MessageFeedback', @level2type = N'COLUMN', @level2name = N'Reason';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料最後修改時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'MessageFeedback', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

