CREATE TABLE [repositories].[RepositoryStewards] (
    [RepositoryId] UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [pk_RepositoryStewards] PRIMARY KEY CLUSTERED ([RepositoryId] ASC, [UserId] ASC),
    CONSTRAINT [fk_RepositoryStewards_ManagedRepositories] FOREIGN KEY ([RepositoryId]) REFERENCES [repositories].[ManagedRepositories] ([Id]),
    CONSTRAINT [fk_RepositoryStewards_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_RepositoryStewards_UserId]
    ON [repositories].[RepositoryStewards]([UserId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'儲存庫負責人：可直接授予、撤銷與改到期日，也是 repository-stewards 審核人規則的來源。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryStewards';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'負責的儲存庫。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryStewards', @level2type = N'COLUMN', @level2name = N'RepositoryId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'負責人的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryStewards', @level2type = N'COLUMN', @level2name = N'UserId';


GO

