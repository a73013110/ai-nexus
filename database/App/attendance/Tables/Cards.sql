CREATE TABLE [attendance].[Cards] (
    [Id]         UNIQUEIDENTIFIER   NOT NULL,
    [CardNumber] NVARCHAR (32)      NOT NULL,
    [Kind]       NVARCHAR (16)      NOT NULL,
    [Note]       NVARCHAR (200)     NULL,
    [CreatedAt]  DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [pk_Cards] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_Cards_CardNumber] CHECK (len(ltrim(rtrim([CardNumber])))>(0)),
    CONSTRAINT [ck_Cards_Kind] CHECK ([Kind]=N'visitor' OR [Kind]=N'employee')
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_Cards_CardNumber]
    ON [attendance].[Cards]([CardNumber] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'已登錄的門禁卡。只登錄 HR 管理的卡；打卡檔裡出現但這裡沒有的卡號，在歸戶 view 中視為未歸戶。持有人不放這裡，見 CardAssignments。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'打卡檔裡的卡號原始字串，不補零、不轉數字；Punches.CardNumber 以相同字串比對，唯一。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'CardNumber';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'登錄時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'卡片種類：employee（員工卡）、visitor（訪客卡）；訪客卡的刷卡照存但不歸戶，也不算未歸戶卡號。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'備註，例如卡片外觀或補發原因。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'Cards', @level2type = N'COLUMN', @level2name = N'Note';


GO

