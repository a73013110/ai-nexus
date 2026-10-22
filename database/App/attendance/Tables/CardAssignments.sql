CREATE TABLE [attendance].[CardAssignments] (
    [Id]         UNIQUEIDENTIFIER   NOT NULL,
    [CardId]     UNIQUEIDENTIFIER   NOT NULL,
    [UserId]     UNIQUEIDENTIFIER   NULL,
    [HolderName] NVARCHAR (120)     NULL,
    [StartsAt]   DATETIMEOFFSET (7) NOT NULL,
    [EndsAt]     DATETIMEOFFSET (7) NULL,
    [Note]       NVARCHAR (200)     NULL,
    [CreatedAt]  DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [pk_CardAssignments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_CardAssignments_Holder] CHECK ([UserId] IS NOT NULL OR isnull(len(ltrim(rtrim([HolderName]))),(0))>(0)),
    CONSTRAINT [ck_CardAssignments_Period] CHECK ([EndsAt] IS NULL OR [EndsAt]>[StartsAt]),
    CONSTRAINT [fk_CardAssignments_Cards] FOREIGN KEY ([CardId]) REFERENCES [attendance].[Cards] ([Id]),
    CONSTRAINT [fk_CardAssignments_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_CardAssignments_CardId_StartsAt]
    ON [attendance].[CardAssignments]([CardId] ASC, [StartsAt] ASC)
    INCLUDE([EndsAt], [UserId], [HolderName]);


GO

CREATE NONCLUSTERED INDEX [ix_CardAssignments_UserId_StartsAt]
    ON [attendance].[CardAssignments]([UserId] ASC, [StartsAt] ASC)
    INCLUDE([CardId], [EndsAt]) WHERE ([UserId] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'卡號持有期間：刷卡時間落在 [StartsAt, EndsAt) 就歸給該持有人。同一張卡的期間不可重疊（由程式保證，資料庫不擋）；同一人可同時持有多張卡。修正對應只改這裡，刷卡原始資料不動。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯卡片的 Cards 主鍵。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments', @level2type = N'COLUMN', @level2name = N'CardId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建立時間，採 UTC offset；修改與刪除由稽核記錄。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持有結束時間（不含）；空值表示持有中，有值時必須晚於 StartsAt。換人時把前一段結束在新段的 StartsAt。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments', @level2type = N'COLUMN', @level2name = N'EndsAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持有人姓名，給還沒有帳號的人辨識用；綁上 UserId 後保留不清，顯示以 Users 為準。UserId 與本欄至少一個有值。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments', @level2type = N'COLUMN', @level2name = N'HolderName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'備註，例如換卡或離職原因。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments', @level2type = N'COLUMN', @level2name = N'Note';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持有開始時間（含），採 UTC offset；起始預設取第一筆未歸戶刷卡的時間。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments', @level2type = N'COLUMN', @level2name = N'StartsAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持有人，關聯 identity.Users 主鍵；空值表示持有人還沒有帳號（新進員工），此時須填 HolderName，帳號建立後由 HR 綁上即可讓先前的刷卡歸戶。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'CardAssignments', @level2type = N'COLUMN', @level2name = N'UserId';


GO

