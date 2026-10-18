CREATE TABLE [knowledge].[EmbeddingProfiles] (
    [Id]               INT                IDENTITY (1, 1) NOT NULL,
    [Key]              NVARCHAR (200)     NOT NULL,
    [Provider]         NVARCHAR (32)      NOT NULL,
    [Model]            NVARCHAR (160)     NOT NULL,
    [Dimensions]       INT                NOT NULL,
    [InputFormat]      NVARCHAR (32)      NOT NULL,
    [QueryInstruction] NVARCHAR (500)     NOT NULL,
    [Revision]         NVARCHAR (64)      NOT NULL,
    [Status]           NVARCHAR (16)      NOT NULL,
    [CreatedAt]        DATETIMEOFFSET (7) NOT NULL,
    [ActivatedAt]      DATETIMEOFFSET (7) NULL,
    [RetiredAt]        DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_EmbeddingProfiles] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_EmbeddingProfiles_Key]
    ON [knowledge].[EmbeddingProfiles]([Key] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_EmbeddingProfiles_Status]
    ON [knowledge].[EmbeddingProfiles]([Status] ASC) WHERE ([Status]='active');


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'向量空間的不可變快照，切段規則納入 Key 指紋；同時最多一個 active。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此 profile 完整覆蓋並切換為 active 的 UTC 時間。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'ActivatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'向量維度，限已建立資料表的 allowlist。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'Dimensions';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'查詢輸入格式 plain 或 qwen-query。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'InputFormat';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'供應商、模型、維度及輸入／切段規則的唯一指紋。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'Key';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建立此向量空間時的模型識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'Model';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型或搜尋服務供應商識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'Provider';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'qwen-query 的檢索任務指令快照。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'QueryInstruction';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'此 profile 退役的 UTC 時間；作為保留期清理依據。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'RetiredAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外部來源或 repository 的固定版本識別。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'Revision';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務執行狀態。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'EmbeddingProfiles', @level2type = N'COLUMN', @level2name = N'Status';


GO

