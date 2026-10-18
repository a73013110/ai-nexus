CREATE TABLE [inference].[ModelInvocations] (
    [Id]                   UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]              UNIQUEIDENTIFIER   NOT NULL,
    [Kind]                 NVARCHAR (32)      NOT NULL,
    [ModelId]              NVARCHAR (160)     NOT NULL,
    [Provider]             NVARCHAR (32)      NOT NULL,
    [DurationMilliseconds] BIGINT             NULL,
    [Status]               NVARCHAR (16)      NOT NULL,
    [ReservedTokens]       BIGINT             NOT NULL,
    [InputTokens]          BIGINT             NULL,
    [OutputTokens]         BIGINT             NULL,
    [CreatedAt]            DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_ModelInvocations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ModelInvocations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ModelInvocations_CreatedAt]
    ON [inference].[ModelInvocations]([CreatedAt] ASC)
    INCLUDE([OwnerId], [Kind], [ModelId], [Status], [InputTokens], [OutputTokens], [DurationMilliseconds]);


GO

CREATE NONCLUSTERED INDEX [IX_ModelInvocations_OwnerId_CreatedAt]
    ON [inference].[ModelInvocations]([OwnerId] ASC, [CreatedAt] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文字、OCR、embedding 等模型呼叫的狀態與實際用量。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'DurationMilliseconds';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸入 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'InputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型呼叫種類。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准模型的內部識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'ModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型回報的輸出 tokens；未知保持空值。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'OutputTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型或搜尋服務供應商識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'Provider';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'ReservedTokens';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務執行狀態。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'ModelInvocations', @level2type = N'COLUMN', @level2name = N'Status';


GO

