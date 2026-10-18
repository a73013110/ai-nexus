CREATE TABLE [inference].[RunEvents] (
    [RunId]     UNIQUEIDENTIFIER   NOT NULL,
    [Sequence]  BIGINT             NOT NULL,
    [Type]      NVARCHAR (16)      NOT NULL,
    [Status]    NVARCHAR (16)      NOT NULL,
    [Delta]     NVARCHAR (MAX)     NULL,
    [IssueCode] NVARCHAR (40)      NULL,
    [ErrorCode] NVARCHAR (80)      NULL,
    [CreatedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_RunEvents] PRIMARY KEY CLUSTERED ([RunId] ASC, [Sequence] ASC),
    CONSTRAINT [FK_RunEvents_GenerationRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [inference].[GenerationRuns] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_RunEvents_CreatedAt]
    ON [inference].[RunEvents]([CreatedAt] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'生成事件的有序 SSE 重播紀錄；完整生成狀態以 GenerationRuns 為準。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'生成文字增量或完整快照。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents', @level2type = N'COLUMN', @level2name = N'Delta';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對外安全的錯誤代碼，不含密碼或完整例外。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯 GenerationRuns 的識別碼。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents', @level2type = N'COLUMN', @level2name = N'RunId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事件在同一生成中的遞增序號。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents', @level2type = N'COLUMN', @level2name = N'Sequence';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務執行狀態。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事件的種類。', @level0type = N'SCHEMA', @level0name = N'inference', @level1type = N'TABLE', @level1name = N'RunEvents', @level2type = N'COLUMN', @level2name = N'Type';


GO

