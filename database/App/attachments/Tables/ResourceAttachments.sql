CREATE TABLE [attachments].[ResourceAttachments] (
    [ResourceId]   UNIQUEIDENTIFIER NOT NULL,
    [AttachmentId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_ResourceAttachments] PRIMARY KEY CLUSTERED ([ResourceId] ASC, [AttachmentId] ASC),
    CONSTRAINT [FK_ResourceAttachments_Attachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [attachments].[Attachments] ([Id]),
    CONSTRAINT [FK_ResourceAttachments_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ResourceAttachments_AttachmentId]
    ON [attachments].[ResourceAttachments]([AttachmentId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'知識庫或專案資源與附件的引用關聯。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'ResourceAttachments';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'引用的附件識別碼。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'ResourceAttachments', @level2type = N'COLUMN', @level2name = N'AttachmentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯 Resources 的識別碼。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'ResourceAttachments', @level2type = N'COLUMN', @level2name = N'ResourceId';


GO

