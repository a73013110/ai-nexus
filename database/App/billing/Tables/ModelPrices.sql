CREATE TABLE [billing].[ModelPrices] (
    [Id]                    UNIQUEIDENTIFIER   NOT NULL,
    [Provider]              NVARCHAR (32)      NOT NULL,
    [ModelId]               NVARCHAR (160)     NOT NULL,
    [Currency]              NVARCHAR (3)       NOT NULL,
    [Kind]                  NVARCHAR (16)      NOT NULL,
    [InputPerMillion]       DECIMAL (20, 8)    NOT NULL,
    [CachedInputPerMillion] DECIMAL (20, 8)    NOT NULL,
    [OutputPerMillion]      DECIMAL (20, 8)    NOT NULL,
    [PerRequest]            DECIMAL (20, 8)    NOT NULL,
    [RequestCharge]         NVARCHAR (16)      NOT NULL,
    [Note]                  NVARCHAR (500)     NOT NULL,
    [EffectiveAt]           DATETIMEOFFSET (7) NOT NULL,
    [CreatedAt]             DATETIMEOFFSET (7) NOT NULL,
    [CreatedBy]             UNIQUEIDENTIFIER   NOT NULL,
    CONSTRAINT [PK_ModelPrices] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ModelPrices_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ModelPrices_CreatedBy]
    ON [billing].[ModelPrices]([CreatedBy] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_ModelPrices_Provider_ModelId_EffectiveAt]
    ON [billing].[ModelPrices]([Provider] ASC, [ModelId] ASC, [EffectiveAt] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'依供應商、模型、幣別及成本類型保存的不可變價格版本。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每百萬快取輸入 tokens 的單價。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'CachedInputPerMillion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建立紀錄的使用者識別碼。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'CreatedBy';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'費用幣別代碼；不同幣別不可直接合計。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'Currency';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此價格版本開始生效的時間。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'EffectiveAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每百萬輸入 tokens 的單價。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'InputPerMillion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'成本類型。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准模型的內部識別碼。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'ModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者提供的補充說明。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'Note';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每百萬輸出 tokens 的單價。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'OutputPerMillion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每次呼叫的固定單價。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'PerRequest';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型或搜尋服務供應商識別碼。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'Provider';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'搜尋服務每次請求的費用。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'TABLE', @level1name = N'ModelPrices', @level2type = N'COLUMN', @level2name = N'RequestCharge';


GO

