CREATE TABLE [inference].[GenerationRuns] (
    [Id]                     UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]                UNIQUEIDENTIFIER   NOT NULL,
    [TraceId]                NVARCHAR (32)      NULL,
    [ParentSpanId]           NVARCHAR (16)      NULL,
    [OperationId]            UNIQUEIDENTIFIER   NOT NULL,
    [ActiveOwnerId]          UNIQUEIDENTIFIER   NULL,
    [ExecutorId]             UNIQUEIDENTIFIER   NULL,
    [LeaseExpiresAt]         DATETIMEOFFSET (7) NULL,
    [ConversationId]         UNIQUEIDENTIFIER   NOT NULL,
    [UserMessageId]          UNIQUEIDENTIFIER   NOT NULL,
    [AssistantMessageId]     UNIQUEIDENTIFIER   NOT NULL,
    [ModelId]                NVARCHAR (160)     NOT NULL,
    [Provider]               NVARCHAR (32)      NOT NULL,
    [ProviderModelId]        NVARCHAR (150)     NOT NULL,
    [ParametersJson]         NVARCHAR (MAX)     NOT NULL,
    [IdempotencyKey]         NVARCHAR (80)      NOT NULL,
    [RequestHash]            NVARCHAR (64)      NOT NULL,
    [Status]                 NVARCHAR (16)      NOT NULL,
    [Content]                NVARCHAR (MAX)     NOT NULL,
    [LastSequence]           BIGINT             NOT NULL,
    [IssueCode]              NVARCHAR (40)      NULL,
    [ErrorCode]              NVARCHAR (80)      NULL,
    [CreatedAt]              DATETIMEOFFSET (7) NOT NULL,
    [StartedAt]              DATETIMEOFFSET (7) NULL,
    [FinishedAt]             DATETIMEOFFSET (7) NULL,
    [ReservedTokens]         BIGINT             NOT NULL,
    [InputTokens]            BIGINT             NULL,
    [OutputTokens]           BIGINT             NULL,
    [DurationMilliseconds]   BIGINT             NULL,
    [GenerationMilliseconds] BIGINT             NULL,
    CONSTRAINT [PK_GenerationRuns] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_GenerationRuns_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]),
    CONSTRAINT [FK_GenerationRuns_Messages_AssistantMessageId] FOREIGN KEY ([AssistantMessageId]) REFERENCES [conversations].[Messages] ([Id]),
    CONSTRAINT [FK_GenerationRuns_Messages_UserMessageId] FOREIGN KEY ([UserMessageId]) REFERENCES [conversations].[Messages] ([Id]),
    CONSTRAINT [FK_GenerationRuns_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_GenerationRuns_ActiveOwnerId_LeaseExpiresAt]
    ON [inference].[GenerationRuns]([ActiveOwnerId] ASC, [LeaseExpiresAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_GenerationRuns_AssistantMessageId]
    ON [inference].[GenerationRuns]([AssistantMessageId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_GenerationRuns_ConversationId_CreatedAt]
    ON [inference].[GenerationRuns]([ConversationId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_GenerationRuns_CreatedAt]
    ON [inference].[GenerationRuns]([CreatedAt] ASC)
    INCLUDE([OwnerId], [ModelId], [Status], [InputTokens], [OutputTokens], [DurationMilliseconds]);


GO

CREATE NONCLUSTERED INDEX [IX_GenerationRuns_OwnerId_CreatedAt]
    ON [inference].[GenerationRuns]([OwnerId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_GenerationRuns_UserMessageId]
    ON [inference].[GenerationRuns]([UserMessageId] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_GenerationRuns_ActiveOwnerId]
    ON [inference].[GenerationRuns]([ActiveOwnerId] ASC) WHERE ([ActiveOwnerId] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_GenerationRuns_OwnerId_IdempotencyKey]
    ON [inference].[GenerationRuns]([OwnerId] ASC, [IdempotencyKey] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'聊天生成的持久狀態、冪等請求、執行租約、回答及用量。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'仍在執行的擁有者；filtered unique index 限制每人一個生成。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ActiveOwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此次生成的 AI 回答訊息識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'AssistantMessageId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'生成中或已完成的回答文字快照。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'Content';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯對話的識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ConversationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'DurationMilliseconds';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對外安全的錯誤代碼，不含密碼或完整例外。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'處理此次生成的伺服器程序識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ExecutorId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'工作結束時間。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'FinishedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'從生成開始至終止的耗時毫秒；未開始的請求保持空值。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'GenerationMilliseconds';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'擁有者範圍內的冪等請求識別，避免重試重複處理。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'IdempotencyKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸入 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'InputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後已持久化的生成事件序號。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'LastSequence';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'生成 executor 租約的到期時間。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'LeaseExpiresAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准模型的內部識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持久作業識別，跨佇列與重試保持不變。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'OperationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸出 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'OutputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'執行參數的 JSON 快照，不含服務密鑰。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ParametersJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'排程來源的 W3C span 識別，重試沿用同一 trace。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ParentSpanId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型或搜尋服務供應商識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'Provider';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送往指定供應商的原生模型識別碼，與核准路由識別碼分開保存。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ProviderModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'請求內容指紋，用於辨識冪等識別碼衝突。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'RequestHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'ReservedTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'工作開始執行時間。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'StartedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務執行狀態。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'W3C 流程追蹤識別，僅由伺服器建立。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'TraceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此次生成的使用者提問訊息識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'GenerationRuns', @level2type = N'COLUMN', @level2name = N'UserMessageId';


GO

