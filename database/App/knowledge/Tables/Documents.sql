CREATE TABLE [knowledge].[Documents] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [CollectionId] UNIQUEIDENTIFIER NULL,
    [AttachmentId] UNIQUEIDENTIFIER NULL,
    [FileName]     NVARCHAR (180)   NOT NULL,
    [ContentType]  NVARCHAR (80)    NOT NULL,
    [Status]       NVARCHAR (16)    NOT NULL,
    [PageCount]    INT              NOT NULL,
    [ChunkCount]   INT              NOT NULL,
    [Warning]      NVARCHAR (500)   NULL,
    [IsDeleted]    BIT              NOT NULL,
    [JobId]        UNIQUEIDENTIFIER NULL,
    [TextContent]  NVARCHAR (MAX)   NULL,
    [TextVersion]  INT              NOT NULL,
    CONSTRAINT [PK_Documents] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Documents_Attachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [files].[Attachments] ([Id]),
    CONSTRAINT [FK_Documents_Collections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [knowledge].[Collections] ([Id]),
    CONSTRAINT [FK_Documents_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Documents_AttachmentId]
    ON [knowledge].[Documents]([AttachmentId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_Documents_CollectionId_Status_IsDeleted]
    ON [knowledge].[Documents]([CollectionId] ASC, [Status] ASC, [IsDeleted] ASC)
    INCLUDE([Id], [FileName], [ChunkCount]);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'知識庫文件的原始附件、分析／索引狀態與 embedding profile。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'引用的附件識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'AttachmentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文件已建立的檢索片段數。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'ChunkCount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯知識庫的識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'CollectionId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准的 MIME 型別。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'ContentType';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'原始附件檔名，不作為伺服器儲存路徑。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'FileName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否邏輯刪除；不自動刪除歷史紀錄。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'IsDeleted';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯背景工作識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'JobId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文件總頁數。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'PageCount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務執行狀態。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'純文字來源的可編輯內容；一般上傳原檔保持空值。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'TextContent';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'純文字內容的樂觀並行版本號。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'TextVersion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'處理過程中的非致命提示。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'Documents', @level2type = N'COLUMN', @level2name = N'Warning';


GO

