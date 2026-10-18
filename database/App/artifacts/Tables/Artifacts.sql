CREATE TABLE [artifacts].[Artifacts] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [Version]         INT              NOT NULL,
    [SourceMessageId] UNIQUEIDENTIFIER NULL,
    [ProjectId]       UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_Artifacts] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Artifacts_Messages_SourceMessageId] FOREIGN KEY ([SourceMessageId]) REFERENCES [conversations].[Messages] ([Id]),
    CONSTRAINT [FK_Artifacts_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]),
    CONSTRAINT [FK_Artifacts_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Artifacts_ProjectId]
    ON [artifacts].[Artifacts]([ProjectId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_Artifacts_SourceMessageId]
    ON [artifacts].[Artifacts]([SourceMessageId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者保存的成果文件與目前版本。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'Artifacts';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'Artifacts', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯專案的識別碼。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'Artifacts', @level2type = N'COLUMN', @level2name = N'ProjectId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此成果版本所引用的來源訊息識別碼。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'Artifacts', @level2type = N'COLUMN', @level2name = N'SourceMessageId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務版本號，用於歷史或樂觀並行控制。', @level0type = N'SCHEMA', @level0name = N'artifacts', @level1type = N'TABLE', @level1name = N'Artifacts', @level2type = N'COLUMN', @level2name = N'Version';


GO

