CREATE TABLE [calendarsync].[CalendarEvents] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [CalendarId]         NVARCHAR (200)     NOT NULL,
    [DesNo]              NVARCHAR (20)      NOT NULL,
    [SegmentKey]         NVARCHAR (40)      NOT NULL,
    [Kind]               NVARCHAR (16)      NOT NULL,
    [ApplicantName]      NVARCHAR (50)      NOT NULL,
    [AgentName]          NVARCHAR (50)      NULL,
    [FlowStatus]         NVARCHAR (16)      NOT NULL,
    [StartsAt]           DATETIMEOFFSET (7) NOT NULL,
    [EndsAt]             DATETIMEOFFSET (7) NOT NULL,
    [ContentHash]        BINARY (32)        NOT NULL,
    [GoogleEventId]      NVARCHAR (1024)    NOT NULL,
    [SyncedHash]         BINARY (32)        NULL,
    [HtmlLink]           NVARCHAR (1000)    NULL,
    [State]              NVARCHAR (16)      NOT NULL,
    [RemovedAt]          DATETIMEOFFSET (7) NULL,
    [Attempts]           INT                NOT NULL,
    [NextAttemptAt]      DATETIMEOFFSET (7) NULL,
    [LastAttemptAt]      DATETIMEOFFSET (7) NULL,
    [LastErrorCode]      NVARCHAR (64)      NULL,
    [LastError]          NVARCHAR (500)     NULL,
    [SyncedAt]           DATETIMEOFFSET (7) NULL,
    [LegacyUuid]         NVARCHAR (200)     NULL,
    [CreatedAt]          DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]          DATETIMEOFFSET (7) NOT NULL,
    [PendingCause]       NVARCHAR (32)      NULL,
    [PendingChangesJson] NVARCHAR (1000)    NULL,
    CONSTRAINT [pk_CalendarEvents] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_CalendarEvents_Attempts] CHECK ([Attempts]>=(0)),
    CONSTRAINT [ck_CalendarEvents_Error] CHECK ([State]=N'failed' OR [LastErrorCode] IS NULL AND [LastError] IS NULL),
    CONSTRAINT [ck_CalendarEvents_Kind] CHECK ([Kind]=N'travel' OR [Kind]=N'leave'),
    CONSTRAINT [ck_CalendarEvents_Period] CHECK ([EndsAt]>[StartsAt]),
    CONSTRAINT [ck_CalendarEvents_Removed] CHECK ([State]<>N'removed' OR [RemovedAt] IS NOT NULL),
    CONSTRAINT [ck_CalendarEvents_State] CHECK ([State]=N'removed' OR [State]=N'synced' OR [State]=N'failed' OR [State]=N'pending')
);


GO

CREATE NONCLUSTERED INDEX [ix_CalendarEvents_CalendarId_EndsAt]
    ON [calendarsync].[CalendarEvents]([CalendarId] ASC, [EndsAt] ASC) WHERE ([State]<>N'removed');


GO

CREATE NONCLUSTERED INDEX [ix_CalendarEvents_CalendarId_NextAttemptAt]
    ON [calendarsync].[CalendarEvents]([CalendarId] ASC, [NextAttemptAt] ASC) WHERE ([State] IN (N'pending', N'failed'));


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_CalendarEvents_CalendarId_DesNo_SegmentKey]
    ON [calendarsync].[CalendarEvents]([CalendarId] ASC, [DesNo] ASC, [SegmentKey] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'公文系統的請假單、出差單在 Google 日曆上的一段期間：一般表單一列，跨區間假單（GD Form_Leave_Group）每段一列。期望內容每輪由 GD 重算，與上次推送的內容不同就待同步；單被退回、銷單、刪除或不再是流程中／核准時標為移除並刪除 Google 事件，列保留供查證。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'代理人姓名；表單沒有代理人時為空值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'AgentName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'請假人或出差人姓名（GD Sys_User.NAME，查不到時為公文帳號），用於事件標題。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'ApplicantName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'連續推送失敗的次數，用於指數退避；成功時歸零。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'Attempts';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Google 日曆 ID（設定 CalendarSync:CalendarId）；不同日曆各自一組列，開發用的測試日曆不會動到正式日曆的列。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'CalendarId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'期望的 Google 事件內容（標題、說明、起訖、顏色）的 SHA-256；事件格式改版時雜湊跟著變，已同步的事件會自動更新一次。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'ContentHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'公文系統的表單文號（GD Form_Leave／Form_Travel 的 DesNo），L 開頭是請假單、T 開頭是出差單。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'DesNo';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事件結束時間，晚於 StartsAt。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'EndsAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次看到的公文狀態：Send（流程中）、Decide（核准）；移除後保留移除前的值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'FlowStatus';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Google 日曆的 event id：新列建立時就決定（等於 Id 的十六進位字串），舊系統轉入的列是 Google 產生的 ID。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'GoogleEventId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Google 回傳的事件網址，管理頁「在 Google 日曆開啟」用；還沒成功建立時為空值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'HtmlLink';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼；新建立的事件以它的 32 位小寫十六進位字串作為 Google event id，建立重試時不會重複。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'表單種類：leave（請假）、travel（出差）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次推送（成功或失敗）的時間。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'LastAttemptAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次推送失敗的簡短說明（不含金鑰與權杖）；只有 failed 才有值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'LastError';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次推送失敗的錯誤代碼（例如 google_rate_limited）；只有 failed 才有值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'LastErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'舊系統 EDocSyncToGoogleCalendar.UUID，只有轉入的列有值：舊系統建立事件後沒寫回 EventId 時，以 Google 事件的私有屬性 UUID 找回，避免重複建立。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'LegacyUuid';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'下一次可以推送的時間；待同步時為空值或規劃當下，失敗時依退避延後。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'NextAttemptAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這一列排進推送的原因：content、restored、form_status、form_missing、segment_gone、unknown、reset；新事件或推送成功後為空值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'PendingCause';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'待推送的欄位變更（JSON 陣列：field、from、to）；推送成功後為空值。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'PendingChangesJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'判定不該再出現在日曆的時間；有值時待同步的動作是刪除。表單又回到流程中或核准時清為空值、重新建立。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'RemovedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一張單的哪一段：一般表單為空字串；跨區間假單為該段開始日期 yyyyMMdd 加上 Form_Leave_Group.DayOfWeek（與舊系統 ADAuthentication 相同，轉入舊資料時直接對應）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'SegmentKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事件開始時間；GD 的時間是台灣時間，以 +08:00 保存。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'StartsAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同步狀態：pending（待同步）、failed（上次推送失敗，到 NextAttemptAt 自動重試）、synced（已同步）、removed（已從 Google 刪除或從未建立，不再處理）。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'State';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次成功推送（建立、更新或刪除）的時間。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'SyncedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次成功推送的 ContentHash；空值表示還沒成功建立過。與 ContentHash 不同就要更新。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'SyncedHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後一次規劃或推送改變這一列的時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'calendarsync', @level1type = N'TABLE', @level1name = N'CalendarEvents', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

