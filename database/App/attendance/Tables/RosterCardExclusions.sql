CREATE TABLE [attendance].[RosterCardExclusions] (
    [CardNumber] NVARCHAR (32)      NOT NULL,
    [Note]       NVARCHAR (200)     NULL,
    [CreatedAt]  DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [pk_RosterCardExclusions] PRIMARY KEY CLUSTERED ([CardNumber] ASC),
    CONSTRAINT [ck_RosterCardExclusions_CardNumber] CHECK (len(ltrim(rtrim([CardNumber])))>(0))
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'HR 依卡號標記為不列入出勤：沒有帳號的資料（只有姓名的持有期間、未歸戶刷卡）依附在卡號上，有列＝不列入。使用者另由 RosterExclusions 標記；只是 HR 看全公司時的檢視篩選，不是權限。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'RosterCardExclusions';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'卡號原始字串，與 Cards.CardNumber、Punches.CardNumber 以相同字串比對；不關聯 Cards，未登錄的卡號也能標。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'RosterCardExclusions', @level2type = N'COLUMN', @level2name = N'CardNumber';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'標記時間，採 UTC offset；變更由稽核記錄。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'RosterCardExclusions', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'不列入的原因，例如測試卡或已離職的臨時人員。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'RosterCardExclusions', @level2type = N'COLUMN', @level2name = N'Note';


GO

