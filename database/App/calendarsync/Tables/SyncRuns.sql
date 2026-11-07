CREATE TABLE [calendarsync].[SyncRuns] (
    [Id]            UNIQUEIDENTIFIER   NOT NULL,
    [CalendarId]    NVARCHAR (200)     NOT NULL,
    [TriggerKind]   NVARCHAR (16)      NOT NULL,
    [RequestedById] UNIQUEIDENTIFIER   NULL,
    [StartedAt]     DATETIMEOFFSET (7) NOT NULL,
    [FinishedAt]    DATETIMEOFFSET (7) NOT NULL,
    [Status]        NVARCHAR (16)      NOT NULL,
    [SourceCount]   INT                NULL,
    [CreatedCount]  INT                NOT NULL,
    [UpdatedCount]  INT                NOT NULL,
    [DeletedCount]  INT                NOT NULL,
    [FailedCount]   INT                NOT NULL,
    [HeldCount]     INT                NOT NULL,
    [ErrorCode]     NVARCHAR (64)      NULL,
    [ErrorMessage]  NVARCHAR (1000)    NULL,
    CONSTRAINT [pk_SyncRuns] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_SyncRuns_Counts] CHECK ([CreatedCount]>=(0) AND [UpdatedCount]>=(0) AND [DeletedCount]>=(0) AND [FailedCount]>=(0) AND [HeldCount]>=(0) AND ([SourceCount] IS NULL OR [SourceCount]>=(0))),
    CONSTRAINT [ck_SyncRuns_Error] CHECK ([Status]=N'failed' OR [Status]=N'held' OR [ErrorCode] IS NULL),
    CONSTRAINT [ck_SyncRuns_Period] CHECK ([FinishedAt]>=[StartedAt]),
    CONSTRAINT [ck_SyncRuns_Requester] CHECK ([TriggerKind]=N'manual' OR [RequestedById] IS NULL),
    CONSTRAINT [ck_SyncRuns_Status] CHECK ([Status]=N'failed' OR [Status]=N'held' OR [Status]=N'partial' OR [Status]=N'succeeded'),
    CONSTRAINT [ck_SyncRuns_TriggerKind] CHECK ([TriggerKind]=N'manual' OR [TriggerKind]=N'scheduled'),
    CONSTRAINT [fk_SyncRuns_Users] FOREIGN KEY ([RequestedById]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_SyncRuns_CalendarId_StartedAt]
    ON [calendarsync].[SyncRuns]([CalendarId] ASC, [StartedAt] DESC);


GO

CREATE NONCLUSTERED INDEX [ix_SyncRuns_RequestedById]
    ON [calendarsync].[SyncRuns]([RequestedById] ASC) WHERE ([RequestedById] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_SyncRuns_StartedAt]
    ON [calendarsync].[SyncRuns]([StartedAt] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同步的執行紀錄：只記有異動（建立、更新、刪除）、有失敗、被安全閥擋下、無法執行或手動觸發的一輪；沒有異動的排程輪只更新記憶體中的最後檢查時間。依 CalendarSync:RunRetentionDays 保存。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一輪同步的 Google 日曆 ID。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'CalendarId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一輪在 Google 建立的事件數。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'CreatedCount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一輪從 Google 刪除的事件數。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'DeletedCount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'held 或 failed 的錯誤代碼；其他狀態為空值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'錯誤的簡短說明（不含金鑰與權杖）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'ErrorMessage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一輪推送失敗的事件數。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'FailedCount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結束時間，不早於 StartedAt。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'FinishedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'被安全閥擋下、沒有標為移除的事件數。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'HeldCount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'手動觸發的操作者，關聯 identity.Users 主鍵；只有 manual 才可有值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'RequestedById';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'從 GD 讀到、應在日曆上的期間數；讀不到 GD 時為空值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'SourceCount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'開始時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'StartedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結果：succeeded（全部成功）、partial（有事件推送失敗，會自動重試）、held（要刪除的事件過多，刪除被擋下等待確認）、failed（無法執行，例如 GD 或 Google 認證不可用）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'觸發方式：scheduled（背景工作）、manual（管理頁立即同步）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'TriggerKind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一輪在 Google 更新的事件數。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRuns', @level2type = N'COLUMN', @level2name = N'UpdatedCount';


GO

