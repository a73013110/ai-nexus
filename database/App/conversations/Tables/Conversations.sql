CREATE TABLE [conversations].[Conversations] (
    [Id]                UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]           UNIQUEIDENTIFIER   NOT NULL,
    [ProjectId]         UNIQUEIDENTIFIER   NULL,
    [Title]             NVARCHAR (120)     NOT NULL,
    [ActiveLeafId]      UNIQUEIDENTIFIER   NULL,
    [CreatedAt]         DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]         DATETIMEOFFSET (7) NOT NULL,
    [IsDeleted]         BIT                NOT NULL,
    [IsFavorite]        BIT                NOT NULL,
    [IsArchived]        BIT                NOT NULL,
    [SystemInstruction] NVARCHAR (4000)    NOT NULL,
    CONSTRAINT [PK_Conversations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Conversations_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]),
    CONSTRAINT [FK_Conversations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Conversations_OwnerId_IsDeleted_IsArchived_IsFavorite_UpdatedAt]
    ON [conversations].[Conversations]([OwnerId] ASC, [IsDeleted] ASC, [IsArchived] ASC, [IsFavorite] ASC, [UpdatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_Conversations_OwnerId_IsDeleted_UpdatedAt]
    ON [conversations].[Conversations]([OwnerId] ASC, [IsDeleted] ASC, [UpdatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_Conversations_ProjectId]
    ON [conversations].[Conversations]([ProjectId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者私人對話、目前訊息分支、收藏封存與自訂指令。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對話目前顯示分支的最後訊息識別碼。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'ActiveLeafId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否封存對話；封存後不再接受新的生成。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'IsArchived';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否邏輯刪除；不自動刪除歷史紀錄。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'IsDeleted';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否標記收藏。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'IsFavorite';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯專案的識別碼。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'ProjectId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對話專用的回答指令。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'SystemInstruction';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面顯示標題。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'Title';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料最後修改時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'conversations', @level1type = N'TABLE', @level1name = N'Conversations', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

