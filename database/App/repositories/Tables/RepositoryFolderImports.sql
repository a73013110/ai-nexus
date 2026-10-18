CREATE TABLE [repositories].[RepositoryFolderImports] (
    [Id]           UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]      UNIQUEIDENTIFIER   NOT NULL,
    [JobId]        UNIQUEIDENTIFIER   NOT NULL,
    [BaseUrl]      NVARCHAR (500)     NOT NULL,
    [Repository]   NVARCHAR (201)     NOT NULL,
    [Commit]       NVARCHAR (64)      NOT NULL,
    [Path]         NVARCHAR (500)     NOT NULL,
    [CollectionId] UNIQUEIDENTIFIER   NOT NULL,
    [PathsJson]    NVARCHAR (MAX)     NOT NULL,
    [Imported]     INT                NOT NULL,
    [Skipped]      INT                NOT NULL,
    [CreatedAt]    DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_RepositoryFolderImports] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RepositoryFolderImports_BackgroundJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [jobs].[BackgroundJobs] ([Id]),
    CONSTRAINT [FK_RepositoryFolderImports_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_RepositoryFolderImports_JobId]
    ON [repositories].[RepositoryFolderImports]([JobId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_RepositoryFolderImports_OwnerId_CreatedAt]
    ON [repositories].[RepositoryFolderImports]([OwnerId] ASC, [CreatedAt] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'程式庫資料夾匯入知識庫的固定版本、檔案清單與背景工作。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 連線主機位址。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'BaseUrl';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯知識庫的識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'CollectionId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'匯入當時固定的 commit SHA。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'Commit';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'已匯入或沿用的檔案數。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'Imported';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯背景工作識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'JobId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'repository 內的資料夾路徑；空字串為根目錄。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'Path';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建立時選定的檔案路徑 JSON 陣列。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'PathsJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea repository 的 owner/name 識別。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'Repository';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'因非文字或超過上限而略過的檔案數。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryFolderImports', @level2type = N'COLUMN', @level2name = N'Skipped';


GO

