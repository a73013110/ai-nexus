CREATE TABLE [attendance].[PunchImports] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [PunchFileId]      UNIQUEIDENTIFIER   NOT NULL,
    [TriggerKind]      NVARCHAR (16)      NOT NULL,
    [RequestedById]    UNIQUEIDENTIFIER   NULL,
    [StartedAt]        DATETIMEOFFSET (7) NOT NULL,
    [FinishedAt]       DATETIMEOFFSET (7) NOT NULL,
    [SourceModifiedAt] DATETIMEOFFSET (7) NOT NULL,
    [SourceSize]       BIGINT             NOT NULL,
    [PunchCount]       INT                NOT NULL,
    [Status]           NVARCHAR (16)      NOT NULL,
    [ErrorMessage]     NVARCHAR (1000)    NULL,
    CONSTRAINT [pk_PunchImports] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_PunchImports_Error] CHECK ([Status]=N'failed' OR [ErrorMessage] IS NULL),
    CONSTRAINT [ck_PunchImports_Period] CHECK ([FinishedAt]>=[StartedAt]),
    CONSTRAINT [ck_PunchImports_PunchCount] CHECK ([PunchCount]>=(0)),
    CONSTRAINT [ck_PunchImports_Requester] CHECK ([TriggerKind]=N'manual' OR [RequestedById] IS NULL),
    CONSTRAINT [ck_PunchImports_SourceSize] CHECK ([SourceSize]>=(0)),
    CONSTRAINT [ck_PunchImports_Status] CHECK ([Status]=N'failed' OR [Status]=N'succeeded'),
    CONSTRAINT [ck_PunchImports_TriggerKind] CHECK ([TriggerKind]=N'manual' OR [TriggerKind]=N'scheduled'),
    CONSTRAINT [fk_PunchImports_PunchFiles] FOREIGN KEY ([PunchFileId]) REFERENCES [attendance].[PunchFiles] ([Id]),
    CONSTRAINT [fk_PunchImports_Users] FOREIGN KEY ([RequestedById]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_PunchImports_PunchFileId_StartedAt]
    ON [attendance].[PunchImports]([PunchFileId] ASC, [StartedAt] DESC);


GO

CREATE NONCLUSTERED INDEX [ix_PunchImports_RequestedById]
    ON [attendance].[PunchImports]([RequestedById] ASC) WHERE ([RequestedById] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_PunchImports_StartedAt]
    ON [attendance].[PunchImports]([StartedAt] DESC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每次匯入打卡檔一列（成功或失敗），含排程與 HR 手動重匯；只增不改，供匯入紀錄頁與失敗追查。檔案目前狀態在 PunchFiles。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'失敗的錯誤訊息；成功時必須為空值。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'ErrorMessage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結束時間（成功或失敗），不早於 StartedAt；中途程序中斷來不及記錄的匯入沒有這一列。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'FinishedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這次實際寫入 Punches 的筆數（扣掉已由其他檔寫入的重複刷卡）；失敗時為 0。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'PunchCount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'匯入的來源檔，關聯 PunchFiles 主鍵。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'PunchFileId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'手動重匯的操作者，關聯 identity.Users 主鍵；只有 manual 才可有值，排程為空值，manual 也可為空值（操作者無法辨識時）。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'RequestedById';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這次匯入時看到的檔案最後修改時間；成功時會成為 PunchFiles.ImportedModifiedAt。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'SourceModifiedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這次匯入時看到的檔案大小（bytes）；成功時會成為 PunchFiles.ImportedSize。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'SourceSize';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'開始匯入的時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'StartedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結果：succeeded、failed；失敗時整個交易回復。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'觸發方式：scheduled（背景工作掃描）、manual（HR 指定日期重匯）。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchImports', @level2type = N'COLUMN', @level2name = N'TriggerKind';


GO

