CREATE TABLE [calendarsync].[SyncRunItems] (
    [RunId]         UNIQUEIDENTIFIER   NOT NULL,
    [EventId]       UNIQUEIDENTIFIER   NOT NULL,
    [Outcome]       NVARCHAR (16)      NOT NULL,
    [DesNo]         NVARCHAR (20)      NOT NULL,
    [Kind]          NVARCHAR (16)      NOT NULL,
    [ApplicantName] NVARCHAR (50)      NOT NULL,
    [StartsAt]      DATETIMEOFFSET (7) NOT NULL,
    [EndsAt]        DATETIMEOFFSET (7) NOT NULL,
    [Cause]         NVARCHAR (32)      NULL,
    [ChangesJson]   NVARCHAR (1000)    NULL,
    [ErrorCode]     NVARCHAR (64)      NULL,
    [ErrorMessage]  NVARCHAR (500)     NULL,
    CONSTRAINT [pk_SyncRunItems] PRIMARY KEY CLUSTERED ([RunId] ASC, [EventId] ASC),
    CONSTRAINT [ck_SyncRunItems_Outcome] CHECK ([Outcome]=N'held' OR [Outcome]=N'failed' OR [Outcome]=N'deleted' OR [Outcome]=N'updated' OR [Outcome]=N'created'),
    CONSTRAINT [fk_SyncRunItems_SyncRuns] FOREIGN KEY ([RunId]) REFERENCES [calendarsync].[SyncRuns] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'一輪同步逐筆處理的事件：推送的結果或被安全閥擋下的移除，內容是當下的快照，不隨事件之後的異動改變。隨 SyncRuns 一起保存與刪除。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'請假人或出差人姓名（當下的快照）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'ApplicantName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'為什麼要處理：content（內容變更）、restored（單據恢復）、form_status（單據狀態變更）、form_missing（單據已不存在）、segment_gone（這段期間已不在單據上）、unknown（查不到原因）、reset（重設日曆）；新事件為空值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'Cause';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'變更的欄位與前後值（JSON 陣列：field、from、to）；沒有可比對的欄位時為空值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'ChangesJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'公文系統的表單文號。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'DesNo';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事件結束時間（當下的快照）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'EndsAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'推送失敗的錯誤代碼；只有 failed 才有值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'推送失敗的簡短說明（不含金鑰與權杖）；只有 failed 才有值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'ErrorMessage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事件列的識別碼（CalendarEvents.Id）；不設外鍵，重設日曆會先清事件列。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'EventId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'表單種類：leave（請假）、travel（出差）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結果：created（新增）、updated（更新）、deleted（刪除）、failed（推送失敗）、held（被安全閥擋下）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'Outcome';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'所屬的一輪，關聯 calendarsync.SyncRuns 主鍵；一輪刪除時連帶刪除。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'RunId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事件開始時間（當下的快照）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'SyncRunItems', @level2type = N'COLUMN', @level2name = N'StartsAt';


GO

