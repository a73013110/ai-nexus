CREATE TABLE [knowledge].[Collections] (
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [Description] NVARCHAR (2000)  NOT NULL,
    CONSTRAINT [PK_Collections] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Collections_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'知識庫的資料資源關聯及索引資訊。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Collections';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的用途說明。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Collections', @level2type = N'COLUMN', @level2name = N'Description';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Collections', @level2type = N'COLUMN', @level2name = N'Id';


GO

