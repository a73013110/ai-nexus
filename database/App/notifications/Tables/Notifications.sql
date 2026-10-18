CREATE TABLE [notifications].[Notifications] (
    [Id]          UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]     UNIQUEIDENTIFIER   NOT NULL,
    [Version]     INT                NOT NULL,
    [EventKey]    NVARCHAR (160)     NOT NULL,
    [Type]        NVARCHAR (80)      NOT NULL,
    [Severity]    NVARCHAR (16)      NOT NULL,
    [IssueCode]   NVARCHAR (40)      NULL,
    [Title]       NVARCHAR (180)     NOT NULL,
    [Body]        NVARCHAR (600)     NOT NULL,
    [TargetKind]  NVARCHAR (32)      NOT NULL,
    [TargetId]    UNIQUEIDENTIFIER   NOT NULL,
    [CreatedAt]   DATETIMEOFFSET (7) NOT NULL,
    [ReadAt]      DATETIMEOFFSET (7) NULL,
    [DismissedAt] DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Notifications_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Notifications_OwnerId_DismissedAt_ReadAt_CreatedAt]
    ON [notifications].[Notifications]([OwnerId] ASC, [DismissedAt] ASC, [ReadAt] ASC, [CreatedAt] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Notifications_OwnerId_EventKey]
    ON [notifications].[Notifications]([OwnerId] ASC, [EventKey] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者個人通知、版本化導向、閱讀狀態與事件去重鍵。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'通知摘要，不包含完整私密原文。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'Body';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'通知移除的時間；空值代表仍可查看。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'DismissedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'通知來源事件的冪等識別碼。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'EventKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'通知已閱讀的時間；空值代表未讀。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'ReadAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'通知呈現層級：info、success 或 error。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'Severity';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'通知所指向的業務識別碼。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'TargetId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'已核准的功能導向類型。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'TargetKind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面顯示標題。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'Title';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事件的種類。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'Type';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務版本號，用於歷史或樂觀並行控制。', @level0type = N'SCHEMA', @level0name = N'notifications', @level1type = N'TABLE', @level1name = N'Notifications', @level2type = N'COLUMN', @level2name = N'Version';


GO

