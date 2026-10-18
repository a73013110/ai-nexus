CREATE TABLE [repositories].[RepositoryImports] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [OwnerId]      UNIQUEIDENTIFIER NOT NULL,
    [CollectionId] UNIQUEIDENTIFIER NOT NULL,
    [DocumentId]   UNIQUEIDENTIFIER NOT NULL,
    [Repository]   NVARCHAR (201)   NOT NULL,
    [Path]         NVARCHAR (500)   NOT NULL,
    [Commit]       NVARCHAR (64)    NOT NULL,
    [BaseUrl]      NVARCHAR (500)   NOT NULL,
    CONSTRAINT [PK_RepositoryImports] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RepositoryImports_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [knowledge].[Documents] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_RepositoryImports_DocumentId]
    ON [repositories].[RepositoryImports]([DocumentId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_RepositoryImports_OwnerId_CollectionId_Repository_Commit_Path]
    ON [repositories].[RepositoryImports]([OwnerId] ASC, [CollectionId] ASC, [Repository] ASC, [Commit] ASC, [Path] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'程式庫文件匯入所固定的主機、repository、commit 與檔案路徑。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 連線主機位址。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports', @level2type = N'COLUMN', @level2name = N'BaseUrl';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯知識庫的識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports', @level2type = N'COLUMN', @level2name = N'CollectionId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'匯入當時固定的 commit SHA。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports', @level2type = N'COLUMN', @level2name = N'Commit';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯知識文件的識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports', @level2type = N'COLUMN', @level2name = N'DocumentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'repository 內的檔案路徑，不是伺服器路徑。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports', @level2type = N'COLUMN', @level2name = N'Path';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea repository 的 owner/name 識別。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryImports', @level2type = N'COLUMN', @level2name = N'Repository';


GO

