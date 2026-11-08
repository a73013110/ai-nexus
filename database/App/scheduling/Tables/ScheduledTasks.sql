CREATE TABLE [scheduling].[ScheduledTasks] (
    [Key]                 NVARCHAR (100)     NOT NULL,
    [IsPaused]            BIT                NOT NULL,
    [PausedById]          UNIQUEIDENTIFIER   NULL,
    [PausedAt]            DATETIMEOFFSET (7) NULL,
    [NextRunAt]           DATETIMEOFFSET (7) NULL,
    [RunRequestedAt]      DATETIMEOFFSET (7) NULL,
    [RunRequestedById]    UNIQUEIDENTIFIER   NULL,
    [RunParameters]       NVARCHAR (1000)    NULL,
    [CancelRequested]     BIT                NOT NULL,
    [LeaseToken]          UNIQUEIDENTIFIER   NULL,
    [LeaseUntil]          DATETIMEOFFSET (7) NULL,
    [LeaseHost]           NVARCHAR (100)     NULL,
    [ConsecutiveFailures] INT                NOT NULL,
    [UpdatedAt]           DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [pk_ScheduledTasks] PRIMARY KEY CLUSTERED ([Key] ASC),
    CONSTRAINT [ck_ScheduledTasks_ConsecutiveFailures] CHECK ([ConsecutiveFailures]>=(0)),
    CONSTRAINT [ck_ScheduledTasks_Lease] CHECK ([LeaseToken] IS NULL AND [LeaseUntil] IS NULL AND [LeaseHost] IS NULL OR [LeaseToken] IS NOT NULL AND [LeaseUntil] IS NOT NULL AND [LeaseHost] IS NOT NULL),
    CONSTRAINT [ck_ScheduledTasks_Pause] CHECK ([IsPaused]=(1) AND [PausedAt] IS NOT NULL OR [IsPaused]=(0) AND [PausedAt] IS NULL AND [PausedById] IS NULL),
    CONSTRAINT [ck_ScheduledTasks_Request] CHECK ([RunRequestedAt] IS NOT NULL OR [RunRequestedById] IS NULL AND [RunParameters] IS NULL),
    CONSTRAINT [fk_ScheduledTasks_PausedById] FOREIGN KEY ([PausedById]) REFERENCES [identity].[Users] ([Id]),
    CONSTRAINT [fk_ScheduledTasks_RunRequestedById] FOREIGN KEY ([RunRequestedById]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_ScheduledTasks_PausedById]
    ON [scheduling].[ScheduledTasks]([PausedById] ASC) WHERE ([PausedById] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_ScheduledTasks_RunRequestedById]
    ON [scheduling].[ScheduledTasks]([RunRequestedById] ASC) WHERE ([RunRequestedById] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'排程任務的執行狀態：一個任務一列，以程式宣告的 Key 對應；排程規則與名稱在程式與設定（Scheduling:Tasks），這裡只存下次執行時間、暫停、手動要求與跨程序租約。程式不認得的 Key（其他版本或已移除的任務）一律略過。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'要求停止執行中的這一輪；持有租約的程序在下一次續租時讀到並取消，結束時清除。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'CancelRequested';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'連續失敗的次數，成功或沒事可做時歸零；達到門檻時通知管理員。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'ConsecutiveFailures';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否由管理員暫停：暫停時不依排程執行，仍可手動執行。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'IsPaused';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'任務的穩定識別碼（例如 mail.retention），由程式宣告，寫入後不可改名。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'Key';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持有租約的主機名稱，供管理頁顯示在哪台機器執行。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'LeaseHost';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'執行中這一輪的租約（fencing token）；只有持有相同值的程序能續租與寫入結果，空值表示沒有在執行。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'LeaseToken';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'租約到期時間；執行中的程序定期延長，過期表示該程序已中止，其他程序可接手。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'LeaseUntil';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'下次依排程執行的時間；空值表示不再依排程執行（例如啟動任務已成功）。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'NextRunAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'暫停的時間；未暫停時為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'PausedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'暫停的管理員，關聯 identity.Users 主鍵；未暫停時為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'PausedById';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'手動執行附帶的參數（JSON），由任務自行解讀；沒有手動要求時為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'RunParameters';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'手動執行的要求時間；認領這一輪時清除。執行中收到的要求在這一輪結束後接著執行。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'RunRequestedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'要求手動執行的管理員，關聯 identity.Users 主鍵；系統要求（例如其他功能觸發）為空值。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'RunRequestedById';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後更新時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'scheduling', @level1type = N'TABLE', @level1name = N'ScheduledTasks', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

