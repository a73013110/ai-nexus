CREATE TABLE [billing].[ModelCharges] (
    [Id]                    UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]               UNIQUEIDENTIFIER   NOT NULL,
    [ConversationId]        UNIQUEIDENTIFIER   NULL,
    [Provider]              NVARCHAR (32)      NOT NULL,
    [ModelId]               NVARCHAR (160)     NOT NULL,
    [Operation]             NVARCHAR (32)      NOT NULL,
    [CreatedAt]             DATETIMEOFFSET (7) NOT NULL,
    [StartedAt]             DATETIMEOFFSET (7) NULL,
    [FinishedAt]            DATETIMEOFFSET (7) NULL,
    [PriceId]               UNIQUEIDENTIFIER   NULL,
    [Currency]              NVARCHAR (3)       NOT NULL,
    [Kind]                  NVARCHAR (16)      NOT NULL,
    [InputPerMillion]       DECIMAL (20, 8)    NOT NULL,
    [CachedInputPerMillion] DECIMAL (20, 8)    NOT NULL,
    [OutputPerMillion]      DECIMAL (20, 8)    NOT NULL,
    [PerRequest]            DECIMAL (20, 8)    NOT NULL,
    [RequestCharge]         NVARCHAR (16)      NOT NULL,
    [InputTokens]           BIGINT             NULL,
    [CachedInputTokens]     BIGINT             NULL,
    [OutputTokens]          BIGINT             NULL,
    [ReasoningTokens]       BIGINT             NULL,
    [UsageComplete]         BIT                NOT NULL,
    [Amount]                DECIMAL (20, 8)    NULL,
    [State]                 NVARCHAR (24)      NOT NULL,
    [Outcome]               NVARCHAR (16)      NOT NULL,
    CONSTRAINT [PK_ModelCharges] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ModelCharges_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]),
    CONSTRAINT [FK_ModelCharges_ModelPrices_PriceId] FOREIGN KEY ([PriceId]) REFERENCES [billing].[ModelPrices] ([Id]),
    CONSTRAINT [FK_ModelCharges_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ModelCharges_ConversationId]
    ON [billing].[ModelCharges]([ConversationId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_ModelCharges_CreatedAt]
    ON [billing].[ModelCharges]([CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_ModelCharges_OwnerId_CreatedAt]
    ON [billing].[ModelCharges]([OwnerId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_ModelCharges_PriceId]
    ON [billing].[ModelCharges]([PriceId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'各呼叫當時的價格與用量快照；未知費用保持空值。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此呼叫的已知費用；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'Amount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每百萬快取輸入 tokens 的單價。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'CachedInputPerMillion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的快取輸入 tokens。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'CachedInputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯對話的識別碼。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'ConversationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'費用幣別代碼；不同幣別不可直接合計。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'Currency';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'工作結束時間。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'FinishedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每百萬輸入 tokens 的單價。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'InputPerMillion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸入 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'InputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'成本類型。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准模型的內部識別碼。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'ModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型呼叫或背景工作的操作類型。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'Operation';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'搜尋或處理操作的結果分類。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'Outcome';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每百萬輸出 tokens 的單價。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'OutputPerMillion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸出 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'OutputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每次呼叫的固定單價。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'PerRequest';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此呼叫採用的不可變價格版本。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'PriceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型或搜尋服務供應商識別碼。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'Provider';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的推理 tokens。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'ReasoningTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'搜尋服務每次請求的費用。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'RequestCharge';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'工作開始執行時間。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'StartedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的生命週期狀態。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'State';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'本次呼叫是否有完整且可計費的實際用量。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelCharges', @level2type = N'COLUMN', @level2name = N'UsageComplete';


GO

