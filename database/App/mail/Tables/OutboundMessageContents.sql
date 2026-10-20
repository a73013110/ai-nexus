CREATE TABLE [mail].[OutboundMessageContents] (
    [MessageId] UNIQUEIDENTIFIER NOT NULL,
    [Protected] BIT              NOT NULL,
    [Html]      NVARCHAR (MAX)   NULL,
    [Text]      NVARCHAR (MAX)   NOT NULL,
    CONSTRAINT [pk_OutboundMessageContents] PRIMARY KEY CLUSTERED ([MessageId] ASC),
    CONSTRAINT [fk_OutboundMessageContents_OutboundMessages] FOREIGN KEY ([MessageId]) REFERENCES [mail].[OutboundMessages] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'郵件入列時渲染好的內文，與 OutboundMessages 一對一；寄出的就是這裡的內容。依保存期限或敏感信寄出後刪除。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessageContents';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'HTML 內文；Protected=1 時是 Data Protection（AiNexus.Mail.Content.v1）的 base64 密文。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessageContents', @level2type = N'COLUMN', @level2name = N'Html';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯郵件的 OutboundMessages 主鍵；刪除郵件時一併刪除。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessageContents', @level2type = N'COLUMN', @level2name = N'MessageId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'內文是否以 Data Protection 加密（敏感信）；金鑰圈遺失時無法解密，該信會失敗。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessageContents', @level2type = N'COLUMN', @level2name = N'Protected';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'純文字內文；Protected=1 時是 Data Protection 的 base64 密文。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessageContents', @level2type = N'COLUMN', @level2name = N'Text';


GO

