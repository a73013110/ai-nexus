CREATE TABLE [quality].[RetrievalEvaluationResults] (
    [RunId]          UNIQUEIDENTIFIER NOT NULL,
    [CaseIndex]      INT              NOT NULL,
    [Mode]           NVARCHAR (24)    NOT NULL,
    [ActualMode]     NVARCHAR (120)   NOT NULL,
    [Unavailable]    NVARCHAR (80)    NULL,
    [Recall]         FLOAT (53)       NULL,
    [ReciprocalRank] FLOAT (53)       NULL,
    [Ndcg]           FLOAT (53)       NULL,
    [Refused]        BIT              NULL,
    [RewriteMs]      BIGINT           NOT NULL,
    [EmbedMs]        BIGINT           NOT NULL,
    [SearchMs]       BIGINT           NOT NULL,
    [RerankMs]       BIGINT           NOT NULL,
    [ElapsedMs]      BIGINT           NOT NULL,
    CONSTRAINT [PK_RetrievalEvaluationResults] PRIMARY KEY CLUSTERED ([RunId] ASC, [CaseIndex] ASC, [Mode] ASC),
    CONSTRAINT [FK_RetrievalEvaluationResults_RetrievalEvaluations_RunId] FOREIGN KEY ([RunId]) REFERENCES [quality].[RetrievalEvaluations] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'四種檢索模式的相關性、無來源拒答與延遲指標；不保存查詢或檢索來源原文。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'實際檢索模式，包含略過或降級標記。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'ActualMode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測案例的從零開始索引。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'CaseIndex';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'執行耗時，以毫秒計。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'ElapsedMs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'查詢向量化含快取耗時，單位毫秒。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'EmbedMs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'請求的檢索比較模式。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'Mode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'前 K 筆依相關性分級計算的正規化折損累積增益。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'Ndcg';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'前 K 筆命中的相關文件比例；無答案題保持空值。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'Recall';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'第一筆相關命中的排名倒數；無答案題保持空值。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'ReciprocalRank';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'無答案題是否未提供來源；有答案題保持空值。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'Refused';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'重排耗時，單位毫秒。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'RerankMs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'查詢改寫耗時，單位毫秒。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'RewriteMs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯 RetrievalEvaluations 的識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'RunId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'授權候選召回耗時，單位毫秒。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'SearchMs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此模式無法評測的錯誤代碼；空值表示已完成。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluationResults', @level2type = N'COLUMN', @level2name = N'Unavailable';


GO

