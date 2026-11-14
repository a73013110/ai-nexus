CREATE TABLE [repositories].[ManagedRepositories] (
    [Id]            UNIQUEIDENTIFIER   NOT NULL,
    [GiteaId]       BIGINT             NOT NULL,
    [Owner]         NVARCHAR (100)     NOT NULL,
    [Name]          NVARCHAR (100)     NOT NULL,
    [Description]   NVARCHAR (500)     NULL,
    [IsPrivate]     BIT                NOT NULL,
    [IsArchived]    BIT                NOT NULL,
    [GrantingTeams] NVARCHAR (1000)    NULL,
    [Requestable]   BIT                CONSTRAINT [df_ManagedRepositories_Requestable] DEFAULT ((0)) NOT NULL,
    [SyncedAt]      DATETIMEOFFSET (7) NOT NULL,
    [MissingSince]  DATETIMEOFFSET (7) NULL,
    [RowVersion]    ROWVERSION         NOT NULL,
    CONSTRAINT [pk_ManagedRepositories] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE NONCLUSTERED INDEX [ix_ManagedRepositories_Owner_Name]
    ON [repositories].[ManagedRepositories]([Owner] ASC, [Name] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_ManagedRepositories_GiteaId]
    ON [repositories].[ManagedRepositories]([GiteaId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'由服務帳號同步的 Gitea 組織儲存庫目錄；授權與申請的對象。Gitea 已沒有的不刪除，記 MissingSince。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 的儲存庫說明（截斷到 500 字）。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'Description';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 的儲存庫 ID；改名或轉移組織時以它對應。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'GiteaId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Owners 以外仍授予此庫存取的團隊名稱（頓號分隔）；有值時目錄顯示警告。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'GrantingTeams';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 是否已封存。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'IsArchived';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 是否設為私有；不是私有時組織成員都能讀，逐人授權沒有效果。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'IsPrivate';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 讀不到此庫（刪除或服務帳號失去權限）的起始時間；重新出現時清除。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'MissingSince';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 儲存庫名稱（最後一次同步）。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Gitea 組織名稱（最後一次同步）。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'Owner';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否開放申請；由管理員設定。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'Requestable';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'並行控制版本；管理員修改設定時比對。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'RowVersion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次在 Gitea 讀到此庫的時間。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'ManagedRepositories', @level2type = N'COLUMN', @level2name = N'SyncedAt';


GO

