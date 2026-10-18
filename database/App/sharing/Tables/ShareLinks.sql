CREATE TABLE [sharing].[ShareLinks] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]            UNIQUEIDENTIFIER   NOT NULL,
    [SourceId]           UNIQUEIDENTIFIER   NOT NULL,
    [Kind]               NVARCHAR (24)      NOT NULL,
    [Title]              NVARCHAR (120)     NOT NULL,
    [SnapshotJson]       NVARCHAR (MAX)     NOT NULL,
    [IncludeAttachments] BIT                NOT NULL,
    [IsRevoked]          BIT                NOT NULL,
    [CreatedAt]          DATETIMEOFFSET (7) NOT NULL,
    [ExpiresAt]          DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_ShareLinks] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ShareLinks_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]),
    CONSTRAINT [FK_ShareLinks_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ShareLinks_ExpiresAt]
    ON [sharing].[ShareLinks]([ExpiresAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_ShareLinks_OwnerId_CreatedAt]
    ON [sharing].[ShareLinks]([OwnerId] ASC, [CreatedAt] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'分享版本快照、有效期限、附件選項及撤銷狀態。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'分享到期時間。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'ExpiresAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否明確允許分享附件。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'IncludeAttachments';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'分享是否已撤銷。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'IsRevoked';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'分享內容的種類。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'分享時的固定內容快照；不隨後續編輯變動。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'SnapshotJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部資料來源識別碼。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'SourceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面顯示標題。', @level0type = N'SCHEMA', @level0name = N'sharing', @level1type = N'TABLE', @level1name = N'ShareLinks', @level2type = N'COLUMN', @level2name = N'Title';


GO

