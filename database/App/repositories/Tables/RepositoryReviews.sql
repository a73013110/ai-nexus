CREATE TABLE [repositories].[RepositoryReviews] (
    [Id]                       UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]                  UNIQUEIDENTIFIER   NOT NULL,
    [JobId]                    UNIQUEIDENTIFIER   NOT NULL,
    [BaseUrl]                  NVARCHAR (500)     NOT NULL,
    [Repository]               NVARCHAR (201)     NOT NULL,
    [Commit]                   NVARCHAR (64)      NOT NULL,
    [BaseCommit]               NVARCHAR (64)      NULL,
    [ModelId]                  NVARCHAR (160)     NOT NULL,
    [ConfigurationFingerprint] NVARCHAR (64)      NOT NULL,
    [Note]                     NVARCHAR (2000)    NOT NULL,
    [IdempotencyKey]           NVARCHAR (80)      NOT NULL,
    [RequestHash]              NVARCHAR (64)      NOT NULL,
    [SnapshotJson]             NVARCHAR (MAX)     NOT NULL,
    [CreatedAt]                DATETIMEOFFSET (7) NOT NULL,
    [CommentUrl]               NVARCHAR (500)     NULL,
    [CommentedAt]              DATETIMEOFFSET (7) NULL,
    [PullRequest]              INT                NULL,
    [Purpose]                  NVARCHAR (16)      DEFAULT (N'') NOT NULL,
    CONSTRAINT [PK_RepositoryReviews] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RepositoryReviews_BackgroundJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [jobs].[BackgroundJobs] ([Id]),
    CONSTRAINT [FK_RepositoryReviews_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_RepositoryReviews_JobId]
    ON [repositories].[RepositoryReviews]([JobId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_RepositoryReviews_OwnerId_CreatedAt]
    ON [repositories].[RepositoryReviews]([OwnerId] ASC, [CreatedAt] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_RepositoryReviews_OwnerId_IdempotencyKey]
    ON [repositories].[RepositoryReviews]([OwnerId] ASC, [IdempotencyKey] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'固定 commit 或區間 diff 的私人 review、模型設定指紋與背景任務。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'區間 review 的起點 commit SHA；空值表示單一 commit。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'BaseCommit';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 連線主機位址。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'BaseUrl';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'報告貼到 Pull Request 的留言網址；空值表示尚未張貼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'CommentUrl';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'報告貼到 Pull Request 的時間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'CommentedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'匯入當時固定的 commit SHA。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'Commit';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'固定模型與生成設定的 SHA-256 指紋。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'ConfigurationFingerprint';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'擁有者範圍內的冪等請求識別，避免重試重複處理。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'IdempotencyKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯背景工作識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'JobId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准模型的內部識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'ModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者提供的補充說明。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'Note';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'來源 Pull Request 編號；空值表示直接選擇的 commit 或區間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'PullRequest';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Review 目的：review（程式碼檢閱）或 summary（變更摘要）；與快照相同，列表不必讀取快照。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'Purpose';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea repository 的 owner/name 識別。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'Repository';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'請求內容指紋，用於辨識冪等識別碼衝突。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'RequestHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'分享時的固定內容快照；不隨後續編輯變動。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviews', @level2type = N'COLUMN', @level2name = N'SnapshotJson';


GO

