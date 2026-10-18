CREATE TABLE [repositories].[RepositoryReviewResults] (
    [ReviewId]     UNIQUEIDENTIFIER NOT NULL,
    [Ordinal]      INT              NOT NULL,
    [Output]       NVARCHAR (MAX)   NOT NULL,
    [Truncated]    BIT              NOT NULL,
    [InputTokens]  BIGINT           NULL,
    [OutputTokens] BIGINT           NULL,
    [ElapsedMs]    BIGINT           NOT NULL,
    CONSTRAINT [PK_RepositoryReviewResults] PRIMARY KEY CLUSTERED ([ReviewId] ASC, [Ordinal] ASC),
    CONSTRAINT [FK_RepositoryReviewResults_RepositoryReviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [repositories].[RepositoryReviews] ([Id])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Review 各區段的持久結果及用量；重試沿用已完成區段。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviewResults';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'執行耗時，以毫秒計。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviewResults', @level2type = N'COLUMN', @level2name = N'ElapsedMs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸入 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviewResults', @level2type = N'COLUMN', @level2name = N'InputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一父物件內的呈現順序。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviewResults', @level2type = N'COLUMN', @level2name = N'Ordinal';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測模型的實際回答。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviewResults', @level2type = N'COLUMN', @level2name = N'Output';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸出 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviewResults', @level2type = N'COLUMN', @level2name = N'OutputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯私人程式碼 review 的識別碼。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviewResults', @level2type = N'COLUMN', @level2name = N'ReviewId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測輸出是否因上限截斷。', @level0type = N'SCHEMA', @level0name = N'repositories', @level1type = N'TABLE', @level1name = N'RepositoryReviewResults', @level2type = N'COLUMN', @level2name = N'Truncated';


GO

