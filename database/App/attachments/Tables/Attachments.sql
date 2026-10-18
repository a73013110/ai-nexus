CREATE TABLE [attachments].[Attachments] (
    [Id]            UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]       UNIQUEIDENTIFIER   NOT NULL,
    [FileName]      NVARCHAR (180)     NOT NULL,
    [ContentType]   NVARCHAR (80)      NOT NULL,
    [Size]          BIGINT             NOT NULL,
    [StorageKey]    NVARCHAR (32)      NOT NULL,
    [StorageState]  NVARCHAR (16)      NOT NULL,
    [ExtractedText] NVARCHAR (MAX)     NULL,
    [InLibrary]     BIT                NOT NULL,
    [CreatedAt]     DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_Attachments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CK_Attachments_Size] CHECK ([Size]>(0)),
    CONSTRAINT [CK_Attachments_StorageState] CHECK ([StorageState]='deleting' OR [StorageState]='ready' OR [StorageState]='pending'),
    CONSTRAINT [FK_Attachments_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Attachments_OwnerId_CreatedAt]
    ON [attachments].[Attachments]([OwnerId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_Attachments_OwnerId_InLibrary_CreatedAt_Id]
    ON [attachments].[Attachments]([OwnerId] ASC, [InLibrary] ASC, [CreatedAt] ASC, [Id] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_Attachments_StorageState_CreatedAt]
    ON [attachments].[Attachments]([StorageState] ASC, [CreatedAt] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Attachments_StorageKey]
    ON [attachments].[Attachments]([StorageKey] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'站外附件原檔的 metadata、儲存識別、擷取文字及生命週期；不保存原始 bytes。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准的 MIME 型別。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'ContentType';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'附件分析後的文字。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'ExtractedText';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'原始附件檔名，不作為伺服器儲存路徑。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'FileName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否由個人檔案庫獨立保留原檔；移除對話或知識索引不會刪除保留的檔案。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'InLibrary';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'原始附件大小，以 bytes 計。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'Size';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'站外原檔的不可變隨機識別碼；不含使用者路徑或檔名。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'StorageKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'原檔儲存狀態 pending／ready／deleting；刪檔成功才釋放 metadata 與容量。', @level0type = N'SCHEMA', @level0name = N'attachments', @level1type = N'TABLE', @level1name = N'Attachments', @level2type = N'COLUMN', @level2name = N'StorageState';


GO

