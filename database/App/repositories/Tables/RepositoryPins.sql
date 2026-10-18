CREATE TABLE [repositories].[RepositoryPins] (
    [Id]          UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]     UNIQUEIDENTIFIER   NOT NULL,
    [BaseUrl]     NVARCHAR (500)     NOT NULL,
    [Repository]  NVARCHAR (201)     NOT NULL,
    [Description] NVARCHAR (500)     NOT NULL,
    [Private]     BIT                NOT NULL,
    [PinnedAt]    DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_RepositoryPins] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RepositoryPins_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_RepositoryPins_OwnerId_BaseUrl_Repository]
    ON [repositories].[RepositoryPins]([OwnerId] ASC, [BaseUrl] ASC, [Repository] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者釘選的 Gitea 程式庫；名稱與說明為釘選當時的快照。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryPins';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 連線主機位址。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryPins', @level2type = N'COLUMN', @level2name = N'BaseUrl';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'釘選當時的程式庫說明。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryPins', @level2type = N'COLUMN', @level2name = N'Description';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryPins', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryPins', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'釘選的時間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryPins', @level2type = N'COLUMN', @level2name = N'PinnedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'釘選當時是否為私人程式庫。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryPins', @level2type = N'COLUMN', @level2name = N'Private';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea repository 的 owner/name 識別。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryPins', @level2type = N'COLUMN', @level2name = N'Repository';


GO

