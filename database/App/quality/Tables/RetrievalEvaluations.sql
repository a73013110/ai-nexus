CREATE TABLE [quality].[RetrievalEvaluations] (
    [Id]                       UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]                  UNIQUEIDENTIFIER   NOT NULL,
    [JobId]                    UNIQUEIDENTIFIER   NOT NULL,
    [Title]                    NVARCHAR (120)     NOT NULL,
    [CollectionsJson]          NVARCHAR (MAX)     NOT NULL,
    [CasesJson]                NVARCHAR (MAX)     NOT NULL,
    [ConfigurationFingerprint] NVARCHAR (64)      NOT NULL,
    [ProfileKey]               NVARCHAR (200)     NOT NULL,
    [TopK]                     INT                NOT NULL,
    [CreatedAt]                DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_RetrievalEvaluations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RetrievalEvaluations_BackgroundJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [jobs].[BackgroundJobs] ([Id]),
    CONSTRAINT [FK_RetrievalEvaluations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_RetrievalEvaluations_OwnerId_CreatedAt]
    ON [quality].[RetrievalEvaluations]([OwnerId] ASC, [CreatedAt] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_RetrievalEvaluations_JobId]
    ON [quality].[RetrievalEvaluations]([JobId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'檢索驗收集、授權知識庫、索引與設定指紋及可續跑背景工作；不保存檢索來源原文。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'固定評測案例的 JSON 快照。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'CasesJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測所使用的知識庫識別碼陣列；執行與讀取時重新檢查授權。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'CollectionsJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'固定模型與生成設定的 SHA-256 指紋。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'ConfigurationFingerprint';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯背景工作識別碼。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'JobId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測所固定的向量空間及切段規則識別。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'ProfileKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面顯示標題。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'Title';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'評測所固定的最大檢索結果數。', @level0type = N'SCHEMA', @level0name = N'quality', @level1type = N'TABLE', @level1name = N'RetrievalEvaluations', @level2type = N'COLUMN', @level2name = N'TopK';


GO

