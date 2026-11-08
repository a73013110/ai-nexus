CREATE TABLE [scheduling].[ScheduledTaskRuns] (
    [Id]             UNIQUEIDENTIFIER   NOT NULL,
    [TaskKey]        NVARCHAR (100)     NOT NULL,
    [TriggerKind]    NVARCHAR (16)      NOT NULL,
    [RequestedById]  UNIQUEIDENTIFIER   NULL,
    [Host]           NVARCHAR (100)     NOT NULL,
    [StartedAt]      DATETIMEOFFSET (7) NOT NULL,
    [FinishedAt]     DATETIMEOFFSET (7) NULL,
    [Status]         NVARCHAR (16)      NOT NULL,
    [Stage]          NVARCHAR (120)     NULL,
    [CompletedUnits] INT                NULL,
    [TotalUnits]     INT                NULL,
    [Summary]        NVARCHAR (500)     NULL,
    [ErrorCode]      NVARCHAR (80)      NULL,
    [IssueCode]      NVARCHAR (40)      NULL,
    [ErrorMessage]   NVARCHAR (500)     NULL,
    [TraceId]        NVARCHAR (32)      NULL,
    CONSTRAINT [pk_ScheduledTaskRuns] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_ScheduledTaskRuns_Error] CHECK ([Status]=N'failed' OR [ErrorCode] IS NULL AND [IssueCode] IS NULL AND [ErrorMessage] IS NULL),
    CONSTRAINT [ck_ScheduledTaskRuns_Finished] CHECK ([Status]=N'running' AND [FinishedAt] IS NULL OR [Status]<>N'running' AND [FinishedAt] IS NOT NULL AND [FinishedAt]>=[StartedAt]),
    CONSTRAINT [ck_ScheduledTaskRuns_Requester] CHECK ([TriggerKind]=N'manual' OR [RequestedById] IS NULL),
    CONSTRAINT [ck_ScheduledTaskRuns_Status] CHECK ([Status]=N'interrupted' OR [Status]=N'cancelled' OR [Status]=N'failed' OR [Status]=N'idle' OR [Status]=N'succeeded' OR [Status]=N'running'),
    CONSTRAINT [ck_ScheduledTaskRuns_TriggerKind] CHECK ([TriggerKind]=N'startup' OR [TriggerKind]=N'manual' OR [TriggerKind]=N'scheduled'),
    CONSTRAINT [ck_ScheduledTaskRuns_Units] CHECK (([CompletedUnits] IS NULL OR [CompletedUnits]>=(0)) AND ([TotalUnits] IS NULL OR [TotalUnits]>=(0))),
    CONSTRAINT [fk_ScheduledTaskRuns_ScheduledTasks] FOREIGN KEY ([TaskKey]) REFERENCES [scheduling].[ScheduledTasks] ([Key]),
    CONSTRAINT [fk_ScheduledTaskRuns_Users] FOREIGN KEY ([RequestedById]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_ScheduledTaskRuns_RequestedById]
    ON [scheduling].[ScheduledTaskRuns]([RequestedById] ASC) WHERE ([RequestedById] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_ScheduledTaskRuns_StartedAt]
    ON [scheduling].[ScheduledTaskRuns]([StartedAt] ASC)
    INCLUDE([Status]);


GO

CREATE NONCLUSTERED INDEX [ix_ScheduledTaskRuns_TaskKey_StartedAt]
    ON [scheduling].[ScheduledTaskRuns]([TaskKey] ASC, [StartedAt] DESC)
    INCLUDE([Status], [FinishedAt]);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'排程任務每一輪的執行紀錄（含沒事可做的 idle）；idle 依 Scheduling:IdleRunRetentionDays、其他依 Scheduling:RunRetentionDays 保存，由 scheduling.run-retention 任務清理。業務明細（例如日曆同步的事件數）仍記在各模組自己的表。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'目前已完成的單位數；任務沒有回報進度時為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'CompletedUnits';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'失敗的錯誤代碼；其他狀態為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'失敗的安全說明（固定提示與查證代碼，不含例外訊息）；其他狀態為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'ErrorMessage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結束時間；執行中為空值，結束後不早於 StartedAt。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'FinishedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'執行這一輪的主機名稱。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'Host';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'失敗時的查證代碼，對應系統日誌；其他狀態為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'手動執行的管理員，關聯 identity.Users 主鍵；只有 manual 才可有值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'RequestedById';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'目前階段的簡短說明，由任務回報。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'Stage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'開始時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'StartedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結果：running（執行中）、succeeded（完成並有處理資料）、idle（沒事可做）、failed（失敗）、cancelled（管理員停止）、interrupted（程序關閉或中止，下次接續）。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一輪做了什麼的一句摘要，由任務回傳（例如「清除 120 筆」）。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'Summary';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'所屬任務，關聯 scheduling.ScheduledTasks 主鍵。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'TaskKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'總單位數；未知或沒有回報時為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'TotalUnits';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一輪的 W3C trace ID，對應系統日誌。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'TraceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'觸發方式：scheduled（依排程）、manual（手動執行）、startup（程式啟動時的任務）。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTaskRuns', @level2type = N'COLUMN', @level2name = N'TriggerKind';


GO

