CREATE TABLE [quality].[EvaluationResults] (
    [RunId]            UNIQUEIDENTIFIER NOT NULL,
    [CaseIndex]        INT              NOT NULL,
    [VariantIndex]     INT              NOT NULL,
    [Output]           NVARCHAR (MAX)   NOT NULL,
    [Truncated]        BIT              NOT NULL,
    [RequiredMatches]  INT              NOT NULL,
    [RequiredTotal]    INT              NOT NULL,
    [ForbiddenMatches] INT              NOT NULL,
    [ElapsedMs]        BIGINT           NOT NULL,
    [InputTokens]      BIGINT           NULL,
    [OutputTokens]     BIGINT           NULL,
    [ReviewScore]      INT              NULL,
    [ReviewNote]       NVARCHAR (2000)  NOT NULL,
    [ReviewerId]       UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_EvaluationResults] PRIMARY KEY CLUSTERED ([RunId] ASC, [CaseIndex] ASC, [VariantIndex] ASC),
    CONSTRAINT [FK_EvaluationResults_EvaluationRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [quality].[EvaluationRuns] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EvaluationResults_Users_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_EvaluationResults_ReviewerId]
    ON [quality].[EvaluationResults]([ReviewerId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'各案例／模型組合的輸出、指標、用量與人工覆核結果。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測案例的從零開始索引。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'CaseIndex';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'執行耗時，以毫秒計。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'ElapsedMs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'命中的禁止條件數。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'ForbiddenMatches';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸入 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'InputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測模型的實際回答。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'Output';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸出 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'OutputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'命中的必要條件數。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'RequiredMatches';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'必要條件總數。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'RequiredTotal';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'人工覆核意見。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'ReviewNote';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'人工覆核分數；未覆核保持空值。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'ReviewScore';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'人工覆核者的使用者識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'ReviewerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯 EvaluationRuns 的識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'RunId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測輸出是否因上限截斷。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'Truncated';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測模型／參數組合的從零開始索引。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'EvaluationResults', @level2type = N'COLUMN', @level2name = N'VariantIndex';


GO

