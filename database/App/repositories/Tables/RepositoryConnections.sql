CREATE TABLE [repositories].[RepositoryConnections] (
    [OwnerId]        UNIQUEIDENTIFIER   NOT NULL,
    [BaseUrl]        NVARCHAR (500)     NOT NULL,
    [Login]          NVARCHAR (100)     NOT NULL,
    [ProtectedToken] NVARCHAR (MAX)     NOT NULL,
    [ConnectedAt]    DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_RepositoryConnections] PRIMARY KEY CLUSTERED ([OwnerId] ASC),
    CONSTRAINT [FK_RepositoryConnections_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者個人的 Gitea 連線及 Data Protection 保護的存取 token。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryConnections';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 連線主機位址。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryConnections', @level2type = N'COLUMN', @level2name = N'BaseUrl';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者建立外部服務連線的時間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryConnections', @level2type = N'COLUMN', @level2name = N'ConnectedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部服務的使用者登入名稱。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryConnections', @level2type = N'COLUMN', @level2name = N'Login';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryConnections', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'以 ASP.NET Data Protection 保護的外部 token；不可在 API、稽核或日誌回傳。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryConnections', @level2type = N'COLUMN', @level2name = N'ProtectedToken';


GO

