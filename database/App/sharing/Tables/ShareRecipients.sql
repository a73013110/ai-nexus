CREATE TABLE [sharing].[ShareRecipients] (
    [ShareId] UNIQUEIDENTIFIER NOT NULL,
    [UserId]  UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_ShareRecipients] PRIMARY KEY CLUSTERED ([ShareId] ASC, [UserId] ASC),
    CONSTRAINT [FK_ShareRecipients_ShareLinks_ShareId] FOREIGN KEY ([ShareId]) REFERENCES [sharing].[ShareLinks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShareRecipients_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ShareRecipients_UserId_ShareId]
    ON [sharing].[ShareRecipients]([UserId] ASC, [ShareId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'分享的具名收件人；與原資源 ACL 分開判定。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareRecipients';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯分享的識別碼。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareRecipients', @level2type = N'COLUMN', @level2name = N'ShareId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯使用者的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareRecipients', @level2type = N'COLUMN', @level2name = N'UserId';


GO

