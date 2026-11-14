CREATE TABLE [repositories].[RepositoryGrants] (
    [Id]           UNIQUEIDENTIFIER   NOT NULL,
    [RepositoryId] UNIQUEIDENTIFIER   NOT NULL,
    [UserId]       UNIQUEIDENTIFIER   NULL,
    [Login]        NVARCHAR (100)     NULL,
    [Permission]   NVARCHAR (8)       NOT NULL,
    [ExpiresAt]    DATETIMEOFFSET (7) NULL,
    [Source]       NVARCHAR (16)      NOT NULL,
    [RequestId]    UNIQUEIDENTIFIER   NULL,
    [GrantedById]  UNIQUEIDENTIFIER   NULL,
    [GrantedAt]    DATETIMEOFFSET (7) NOT NULL,
    [Reason]       NVARCHAR (500)     NULL,
    [EndedAt]      DATETIMEOFFSET (7) NULL,
    [EndedById]    UNIQUEIDENTIFIER   NULL,
    [EndReason]    NVARCHAR (16)      NULL,
    [SyncState]    NVARCHAR (16)      NOT NULL,
    [SyncedAt]     DATETIMEOFFSET (7) NULL,
    [SyncError]    NVARCHAR (64)      NULL,
    [RowVersion]   ROWVERSION         NOT NULL,
    CONSTRAINT [pk_RepositoryGrants] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_RepositoryGrants_Ended] CHECK ([EndedAt] IS NULL AND [EndReason] IS NULL AND [EndedById] IS NULL OR [EndedAt] IS NOT NULL AND [EndReason] IS NOT NULL),
    CONSTRAINT [ck_RepositoryGrants_EndReason] CHECK ([EndReason] IS NULL OR [EndReason]=N'revoked' OR [EndReason]=N'expired' OR [EndReason]=N'replaced' OR [EndReason]=N'released'),
    CONSTRAINT [ck_RepositoryGrants_Grantee] CHECK ([UserId] IS NOT NULL AND [Login] IS NULL OR [UserId] IS NULL AND [Login] IS NOT NULL),
    CONSTRAINT [ck_RepositoryGrants_Permission] CHECK ([Permission]=N'read' OR [Permission]=N'write' OR [Permission]=N'admin'),
    CONSTRAINT [ck_RepositoryGrants_Source] CHECK ([Source]=N'request' AND [RequestId] IS NOT NULL OR ([Source]=N'steward' OR [Source]=N'admin') AND [RequestId] IS NULL AND [GrantedById] IS NOT NULL),
    CONSTRAINT [ck_RepositoryGrants_SyncState] CHECK ([SyncState]=N'pending' OR [SyncState]=N'applied' OR [SyncState]=N'failed'),
    CONSTRAINT [fk_RepositoryGrants_EndedById] FOREIGN KEY ([EndedById]) REFERENCES [identity].[Users] ([Id]),
    CONSTRAINT [fk_RepositoryGrants_GrantedById] FOREIGN KEY ([GrantedById]) REFERENCES [identity].[Users] ([Id]),
    CONSTRAINT [fk_RepositoryGrants_ManagedRepositories] FOREIGN KEY ([RepositoryId]) REFERENCES [repositories].[ManagedRepositories] ([Id]),
    CONSTRAINT [fk_RepositoryGrants_RepositoryAccessRequests] FOREIGN KEY ([RequestId]) REFERENCES [repositories].[RepositoryAccessRequests] ([Id]),
    CONSTRAINT [fk_RepositoryGrants_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryGrants_EndedById]
    ON [repositories].[RepositoryGrants]([EndedById] ASC) WHERE ([EndedById] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryGrants_ExpiresAt]
    ON [repositories].[RepositoryGrants]([ExpiresAt] ASC) WHERE ([EndedAt] IS NULL AND [ExpiresAt] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryGrants_GrantedById]
    ON [repositories].[RepositoryGrants]([GrantedById] ASC) WHERE ([GrantedById] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryGrants_RequestId]
    ON [repositories].[RepositoryGrants]([RequestId] ASC) WHERE ([RequestId] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryGrants_SyncState]
    ON [repositories].[RepositoryGrants]([SyncState] ASC) WHERE ([SyncState]<>N'applied');


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryGrants_UserId]
    ON [repositories].[RepositoryGrants]([UserId] ASC) WHERE ([UserId] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_RepositoryGrants_RepositoryId_Login]
    ON [repositories].[RepositoryGrants]([RepositoryId] ASC, [Login] ASC) WHERE ([EndedAt] IS NULL AND [Login] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_RepositoryGrants_RepositoryId_UserId]
    ON [repositories].[RepositoryGrants]([RepositoryId] ASC, [UserId] ASC) WHERE ([EndedAt] IS NULL AND [UserId] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'儲存庫授權（期望狀態）：每人每庫只有一筆未結束的授權；同步任務據此設定 Gitea 協作者。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'revoked 撤銷、expired 到期、replaced 被新的授權取代（例如升級）、released 本人放棄。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'EndReason';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'授權結束時間；NULL 表示有效。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'EndedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結束授權的 Users 主鍵；到期時為 NULL。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'EndedById';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'到期時間；到期由同步任務結束授權並移除協作者。NULL 表示不設期限。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'ExpiresAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'授予時間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'GrantedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'授予者；申請來源是最後核准的人，沒有任何關卡自動核准時為 NULL。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'GrantedById';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'非人員帳號（部署用）的 Gitea 登入名；只有管理員能授予。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'Login';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 協作者權限：read、write 或 admin（admin 只有管理員能給）。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'Permission';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'授予理由（申請理由或直接授予時填寫）。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'Reason';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'授權的儲存庫。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'RepositoryId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准的申請；只有 request 來源有值。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'RequestId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'並行控制版本；改到期日或撤銷時比對。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'RowVersion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'來源：request 申請核准、steward 負責人直接授予、admin 管理員授予。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'Source';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次同步失敗的錯誤代碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'SyncError';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 是否已一致：pending 等待同步、applied 已套用、failed 上次失敗（下一輪重試）。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'SyncState';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次同步嘗試的時間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'SyncedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'取得權限的 Users 主鍵；Gitea 登入名在同步時以目前的 AD 帳號換算。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryGrants', @level2type = N'COLUMN', @level2name = N'UserId';


GO

