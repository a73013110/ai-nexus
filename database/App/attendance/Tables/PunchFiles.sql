CREATE TABLE [attendance].[PunchFiles] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [FileName]           NVARCHAR (128)     NOT NULL,
    [ImportedModifiedAt] DATETIMEOFFSET (7) NULL,
    [ImportedSize]       BIGINT             NULL,
    [LastImportId]       UNIQUEIDENTIFIER   NULL,
    CONSTRAINT [pk_PunchFiles] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_PunchFiles_FileName] CHECK (len(ltrim(rtrim([FileName])))>(0)),
    CONSTRAINT [ck_PunchFiles_ImportedSize] CHECK ([ImportedSize]>=(0)),
    CONSTRAINT [ck_PunchFiles_ImportedVersion] CHECK ([ImportedModifiedAt] IS NULL AND [ImportedSize] IS NULL OR [ImportedModifiedAt] IS NOT NULL AND [ImportedSize] IS NOT NULL),
    CONSTRAINT [fk_PunchFiles_PunchImports] FOREIGN KEY ([LastImportId]) REFERENCES [attendance].[PunchImports] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_PunchFiles_LastImportId]
    ON [attendance].[PunchFiles]([LastImportId] ASC) WHERE ([LastImportId] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_PunchFiles_FileName]
    ON [attendance].[PunchFiles]([FileName] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'打卡機輸出的每日檔（yyyyMMdd.txt）目前的狀態，一個檔一列，只用來判斷要不要重匯並串起 Punches；逐次匯入的歷史在 PunchImports，歷史欄位不在這裡重複。重匯＝在同一個交易刪除 Punches 中該檔先前的列再寫入。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchFiles';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'檔名（不含路徑），格式 yyyyMMdd.txt；唯一。檔名日期只是輸出日，不是出勤日，檔案可含前一天深夜的刷卡。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchFiles', @level2type = N'COLUMN', @level2name = N'FileName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchFiles', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'上次成功匯入時檔案的最後修改時間；與目前檔案不同就重匯。從未成功匯入過為空值（視為要匯入）。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchFiles', @level2type = N'COLUMN', @level2name = N'ImportedModifiedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'上次成功匯入時檔案的大小（bytes），與 ImportedModifiedAt 同時有值或同時為空；任一不同就重匯。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchFiles', @level2type = N'COLUMN', @level2name = N'ImportedSize';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最近一次匯入（成功或失敗），關聯 PunchImports 主鍵；最近一次失敗也要重匯，所以判斷時要看它的 Status。失敗的交易整個回復，Punches 維持上次成功的內容。檔案剛建立、尚未寫入匯入紀錄時為空值。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'PunchFiles', @level2type = N'COLUMN', @level2name = N'LastImportId';


GO

