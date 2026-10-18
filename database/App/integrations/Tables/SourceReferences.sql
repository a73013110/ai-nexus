CREATE TABLE [integrations].[SourceReferences] (
    [ArtifactId] UNIQUEIDENTIFIER   NOT NULL,
    [SourceId]   NVARCHAR (32)      NOT NULL,
    [ExternalId] NVARCHAR (160)     NOT NULL,
    [Revision]   NVARCHAR (160)     NOT NULL,
    [ImportedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_SourceReferences] PRIMARY KEY CLUSTERED ([ArtifactId] ASC),
    CONSTRAINT [FK_SourceReferences_Artifacts_ArtifactId] FOREIGN KEY ([ArtifactId]) REFERENCES [artifacts].[Artifacts] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_SourceReferences_SourceId_ExternalId]
    ON [integrations].[SourceReferences]([SourceId] ASC, [ExternalId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部來源匯入的識別、來源版本與匯入時間。', @level0type = N'SCHEMA', @level0name = N'integrations', @level1type = N'TABLE', @level1name = N'SourceReferences';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯成果文件的識別碼。', @level0type = N'SCHEMA', @level0name = N'integrations', @level1type = N'TABLE', @level1name = N'SourceReferences', @level2type = N'COLUMN', @level2name = N'ArtifactId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部來源中的紀錄識別碼。', @level0type = N'SCHEMA', @level0name = N'integrations', @level1type = N'TABLE', @level1name = N'SourceReferences', @level2type = N'COLUMN', @level2name = N'ExternalId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此來源版本明確匯入的時間。', @level0type = N'SCHEMA', @level0name = N'integrations', @level1type = N'TABLE', @level1name = N'SourceReferences', @level2type = N'COLUMN', @level2name = N'ImportedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部來源或 repository 的固定版本識別。', @level0type = N'SCHEMA', @level0name = N'integrations', @level1type = N'TABLE', @level1name = N'SourceReferences', @level2type = N'COLUMN', @level2name = N'Revision';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部資料來源識別碼。', @level0type = N'SCHEMA', @level0name = N'integrations', @level1type = N'TABLE', @level1name = N'SourceReferences', @level2type = N'COLUMN', @level2name = N'SourceId';


GO

