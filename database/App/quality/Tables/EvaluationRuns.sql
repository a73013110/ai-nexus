CREATE TABLE [quality].[EvaluationRuns] (
    [Id]           UNIQUEIDENTIFIER   NOT NULL,
    [SetId]        UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]      UNIQUEIDENTIFIER   NOT NULL,
    [JobId]        UNIQUEIDENTIFIER   NOT NULL,
    [SetVersion]   INT                NOT NULL,
    [SetTitle]     NVARCHAR (120)     NOT NULL,
    [CasesJson]    NVARCHAR (MAX)     NOT NULL,
    [VariantsJson] NVARCHAR (MAX)     NOT NULL,
    [CreatedAt]    DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_EvaluationRuns] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_EvaluationRuns_BackgroundJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [jobs].[BackgroundJobs] ([Id]),
    CONSTRAINT [FK_EvaluationRuns_EvaluationSets_SetId] FOREIGN KEY ([SetId]) REFERENCES [quality].[EvaluationSets] ([Id]),
    CONSTRAINT [FK_EvaluationRuns_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_EvaluationRuns_OwnerId]
    ON [quality].[EvaluationRuns]([OwnerId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_EvaluationRuns_SetId_CreatedAt]
    ON [quality].[EvaluationRuns]([SetId] ASC, [CreatedAt] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_EvaluationRuns_JobId]
    ON [quality].[EvaluationRuns]([JobId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測執行的題庫與模型設定快照、背景工作與狀態。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'固定評測案例的 JSON 快照。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'CasesJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯背景工作識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'JobId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測題庫識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'SetId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測執行當時的題庫名稱快照。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'SetTitle';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測執行當時的題庫版本。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'SetVersion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測模型／參數組合的 JSON 快照。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationRuns', @level2type = N'COLUMN', @level2name = N'VariantsJson';


GO

