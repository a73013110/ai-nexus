CREATE TABLE [quality].[EvaluationSets] (
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [Description] NVARCHAR (2000)  NOT NULL,
    [CasesJson]   NVARCHAR (MAX)   NOT NULL,
    [Version]     INT              NOT NULL,
    CONSTRAINT [PK_EvaluationSets] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_EvaluationSets_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測題庫、固定測試案例與版本。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationSets';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'固定評測案例的 JSON 快照。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationSets', @level2type = N'COLUMN', @level2name = N'CasesJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的用途說明。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationSets', @level2type = N'COLUMN', @level2name = N'Description';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationSets', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務版本號，用於歷史或樂觀並行控制。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationSets', @level2type = N'COLUMN', @level2name = N'Version';


GO

