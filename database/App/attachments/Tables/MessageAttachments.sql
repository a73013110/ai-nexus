CREATE TABLE [attachments].[MessageAttachments] (
    [MessageId]    UNIQUEIDENTIFIER NOT NULL,
    [AttachmentId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_MessageAttachments] PRIMARY KEY CLUSTERED ([MessageId] ASC, [AttachmentId] ASC),
    CONSTRAINT [FK_MessageAttachments_Attachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [attachments].[Attachments] ([Id]),
    CONSTRAINT [FK_MessageAttachments_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [conversations].[Messages] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_MessageAttachments_AttachmentId]
    ON [attachments].[MessageAttachments]([AttachmentId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'訊息與附件的關聯及呈現順序。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'MessageAttachments';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'引用的附件識別碼。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'MessageAttachments', @level2type = N'COLUMN', @level2name = N'AttachmentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯訊息的識別碼。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'MessageAttachments', @level2type = N'COLUMN', @level2name = N'MessageId';


GO

