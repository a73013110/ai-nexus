CREATE TABLE [attendance].[Punches] (
    [Id]             BIGINT             IDENTITY (1, 1) NOT NULL,
    [PunchFileId]    UNIQUEIDENTIFIER   NOT NULL,
    [CardNumber]     NVARCHAR (32)      NOT NULL,
    [PunchedAt]      DATETIMEOFFSET (7) NOT NULL,
    [AttendanceDate] DATE               NOT NULL,
    [MachineCode]    NVARCHAR (16)      NOT NULL,
    [EventCode]      NVARCHAR (16)      NOT NULL,
    CONSTRAINT [pk_Punches] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_Punches_AttendanceDate] CHECK ([AttendanceDate]=CONVERT([date],[PunchedAt])),
    CONSTRAINT [ck_Punches_PunchedAtOffset] CHECK (datepart(tzoffset,[PunchedAt])=(480)),
    CONSTRAINT [fk_Punches_PunchFiles] FOREIGN KEY ([PunchFileId]) REFERENCES [attendance].[PunchFiles] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_Punches_AttendanceDate]
    ON [attendance].[Punches]([AttendanceDate] ASC)
    INCLUDE([CardNumber], [PunchedAt]);


GO

CREATE NONCLUSTERED INDEX [ix_Punches_PunchFileId]
    ON [attendance].[Punches]([PunchFileId] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_Punches_CardNumber_PunchedAt_MachineCode]
    ON [attendance].[Punches]([CardNumber] ASC, [PunchedAt] ASC, [MachineCode] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'打卡機的原始刷卡，每行一筆。不保存持有人，也不對 Cards 設外鍵：打卡檔可能出現尚未登錄的卡號，匯入不能因此失敗或建假卡；持有人由 vw_AttributedPunches 依持有期間在讀取時計算。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Punches';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'出勤日，依刷卡時間（不是檔名）取本地日期；check 限制必須等於 PunchedAt 的日期，單獨存下來供索引與區間查詢。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Punches', @level2type = N'COLUMN', @level2name = N'AttendanceDate';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'打卡檔的卡號原始字串；不對應 Cards 外鍵，與 Cards.CardNumber 以相同字串比對。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Punches', @level2type = N'COLUMN', @level2name = N'CardNumber';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'打卡檔最後一欄（推測是事件代號）；原樣保存、不解讀。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Punches', @level2type = N'COLUMN', @level2name = N'EventCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼（自動遞增，量大不用 GUID）。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Punches', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'打卡檔第一欄（推測是機器代號，目前只有一台）；原樣保存、不解讀，是去重鍵的一部分。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Punches', @level2type = N'COLUMN', @level2name = N'MachineCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'來源檔，關聯 PunchFiles 主鍵；若同一筆刷卡已由另一個檔寫入，歸屬先寫入的檔。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Punches', @level2type = N'COLUMN', @level2name = N'PunchFileId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'刷卡時間，打卡機的台灣本地時間，固定 +08:00（check 限制）。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Punches', @level2type = N'COLUMN', @level2name = N'PunchedAt';


GO

