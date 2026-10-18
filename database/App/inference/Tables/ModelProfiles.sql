CREATE TABLE [inference].[ModelProfiles] (
    [Id]                NVARCHAR (160) NOT NULL,
    [Provider]          NVARCHAR (32)  NOT NULL,
    [ProviderModelId]   NVARCHAR (150) NOT NULL,
    [DisplayName]       NVARCHAR (120) NOT NULL,
    [ContextTokens]     INT            NOT NULL,
    [MaxOutputTokens]   INT            NOT NULL,
    [SupportsStreaming] BIT            NOT NULL,
    [SupportsUsage]     BIT            NOT NULL,
    CONSTRAINT [PK_ModelProfiles] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准模型的能力、上下文與輸出限制。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型上下文容量，以 tokens 計。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles', @level2type = N'COLUMN', @level2name = N'ContextTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型的介面顯示名稱。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles', @level2type = N'COLUMN', @level2name = N'DisplayName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型核准的最大輸出 tokens。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles', @level2type = N'COLUMN', @level2name = N'MaxOutputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型或搜尋服務供應商識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles', @level2type = N'COLUMN', @level2name = N'Provider';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送往指定供應商的原生模型識別碼，與核准路由識別碼分開保存。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles', @level2type = N'COLUMN', @level2name = N'ProviderModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型是否支援串流輸出。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles', @level2type = N'COLUMN', @level2name = N'SupportsStreaming';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型是否會回報實際用量。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelProfiles', @level2type = N'COLUMN', @level2name = N'SupportsUsage';


GO

