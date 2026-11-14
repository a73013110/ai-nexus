CREATE TABLE [repositories].[RepositoryCollaborators] (
    [RepositoryId] UNIQUEIDENTIFIER   NOT NULL,
    [Login]        NVARCHAR (100)     NOT NULL,
    [Permission]   NVARCHAR (16)      NOT NULL,
    [ObservedAt]   DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [pk_RepositoryCollaborators] PRIMARY KEY CLUSTERED ([RepositoryId] ASC, [Login] ASC),
    CONSTRAINT [fk_RepositoryCollaborators_ManagedRepositories] FOREIGN KEY ([RepositoryId]) REFERENCES [repositories].[ManagedRepositories] ([Id])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同步時在 Gitea 讀到的協作者（實際狀態）；與 RepositoryGrants 比對出未登記存取。每輪整庫改寫。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryCollaborators';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 登入名。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryCollaborators', @level2type = N'COLUMN', @level2name = N'Login';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'讀到的時間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryCollaborators', @level2type = N'COLUMN', @level2name = N'ObservedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 回報的協作者權限。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryCollaborators', @level2type = N'COLUMN', @level2name = N'Permission';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'儲存庫。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryCollaborators', @level2type = N'COLUMN', @level2name = N'RepositoryId';


GO

