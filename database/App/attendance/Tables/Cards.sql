CREATE TABLE [attendance].[Cards] (
    [Id]                UNIQUEIDENTIFIER   NOT NULL,
    [CardNumber]        NVARCHAR (32)      NOT NULL,
    [Note]              NVARCHAR (200)     NULL,
    [CreatedAt]         DATETIMEOFFSET (7) NOT NULL,
    [HiddenFromRecords] BIT                CONSTRAINT [df_Cards_HiddenFromRecords] DEFAULT ((0)) NOT NULL,
    CONSTRAINT [pk_Cards] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_Cards_CardNumber] CHECK (len(ltrim(rtrim([CardNumber])))>(0))
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_Cards_CardNumber]
    ON [attendance].[Cards]([CardNumber] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'門禁卡，一個卡號一列。打卡檔出現的卡號在匯入時自動建立（與刷卡同一個交易），HR 也可以先建立還沒刷過的卡；持有人不放這裡，見 CardAssignments。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'打卡檔裡的卡號原始字串，不補零、不轉數字；Punches.CardNumber 以相同字串比對，唯一。建立後不可修改，否則既有刷卡會對不到。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'CardNumber';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建立時間（匯入時自動建立，或 HR 新增），採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'在出勤紀錄隱藏（董事、外派、訪客卡、測試卡）：HR 看全公司時不列出這張卡的持有期間與刷卡，也不提示待指派。只是檢視篩選、不是權限，本人與主管仍依資料範圍看得到。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'HiddenFromRecords';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'備註，例如卡片用途、外觀或補發原因。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'Note';


GO

