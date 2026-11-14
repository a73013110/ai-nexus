CREATE TABLE [repositories].[RepositoryAccessRequests] (
    [Id]           UNIQUEIDENTIFIER   NOT NULL,
    [RepositoryId] UNIQUEIDENTIFIER   NOT NULL,
    [UserId]       UNIQUEIDENTIFIER   NOT NULL,
    [Permission]   NVARCHAR (8)       NOT NULL,
    [ExpiresAt]    DATETIMEOFFSET (7) NULL,
    [Reason]       NVARCHAR (500)     NOT NULL,
    [CreatedAt]    DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [pk_RepositoryAccessRequests] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_RepositoryAccessRequests_ExpiresAt] CHECK ([ExpiresAt] IS NULL OR [ExpiresAt]>[CreatedAt]),
    CONSTRAINT [ck_RepositoryAccessRequests_Permission] CHECK ([Permission]=N'read' OR [Permission]=N'write'),
    CONSTRAINT [fk_RepositoryAccessRequests_ManagedRepositories] FOREIGN KEY ([RepositoryId]) REFERENCES [repositories].[ManagedRepositories] ([Id]),
    CONSTRAINT [fk_RepositoryAccessRequests_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryAccessRequests_RepositoryId]
    ON [repositories].[RepositoryAccessRequests]([RepositoryId] ASC);


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryAccessRequests_UserId_CreatedAt]
    ON [repositories].[RepositoryAccessRequests]([UserId] ASC, [CreatedAt] DESC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'儲存庫權限申請的內容；簽核狀態在 flow.FlowInstances（SubjectId＝Id）。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryAccessRequests';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送出申請的時間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryAccessRequests', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請的到期時間（到期日隔天 00:00 台灣時間）；NULL 表示不設期限。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryAccessRequests', @level2type = N'COLUMN', @level2name = N'ExpiresAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼，也是流程實例的 SubjectId。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryAccessRequests', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請的權限：read 或 write。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryAccessRequests', @level2type = N'COLUMN', @level2name = N'Permission';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請理由。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryAccessRequests', @level2type = N'COLUMN', @level2name = N'Reason';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請的儲存庫。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryAccessRequests', @level2type = N'COLUMN', @level2name = N'RepositoryId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請並將取得權限的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryAccessRequests', @level2type = N'COLUMN', @level2name = N'UserId';


GO

