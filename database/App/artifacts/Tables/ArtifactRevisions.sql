CREATE TABLE [artifacts].[ArtifactRevisions] (
    [ArtifactId] UNIQUEIDENTIFIER   NOT NULL,
    [Version]    INT                NOT NULL,
    [AuthorId]   UNIQUEIDENTIFIER   NOT NULL,
    [Title]      NVARCHAR (120)     NOT NULL,
    [Content]    NVARCHAR (MAX)     NOT NULL,
    [CreatedAt]  DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_ArtifactRevisions] PRIMARY KEY CLUSTERED ([ArtifactId] ASC, [Version] ASC),
    CONSTRAINT [FK_ArtifactRevisions_Artifacts_ArtifactId] FOREIGN KEY ([ArtifactId]) REFERENCES [artifacts].[Artifacts] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ArtifactRevisions_Users_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ArtifactRevisions_AuthorId]
    ON [artifacts].[ArtifactRevisions]([AuthorId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'成果文件不可變版本、內容與作者。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'ArtifactRevisions';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯成果文件的識別碼。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'ArtifactRevisions', @level2type = N'COLUMN', @level2name = N'ArtifactId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建立此版本的使用者識別碼。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'ArtifactRevisions', @level2type = N'COLUMN', @level2name = N'AuthorId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此版本的成果內容。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'ArtifactRevisions', @level2type = N'COLUMN', @level2name = N'Content';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'ArtifactRevisions', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面顯示標題。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'ArtifactRevisions', @level2type = N'COLUMN', @level2name = N'Title';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務版本號，用於歷史或樂觀並行控制。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'ArtifactRevisions', @level2type = N'COLUMN', @level2name = N'Version';


GO

