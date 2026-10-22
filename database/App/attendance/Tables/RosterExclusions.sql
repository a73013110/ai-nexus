CREATE TABLE [attendance].[RosterExclusions] (
    [UserId]    UNIQUEIDENTIFIER   NOT NULL,
    [Note]      NVARCHAR (200)     NULL,
    [CreatedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [pk_RosterExclusions] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [fk_RosterExclusions_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'HR 標記為不列入出勤的使用者，有列＝不列入，沒有列＝預設列入。只是 HR 看全公司時的檢視篩選，不是權限：本人與主管仍依資料範圍看得到。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'RosterExclusions';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'標記時間，採 UTC offset；變更由稽核記錄。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'RosterExclusions', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'不列入的原因，例如高階主管或外派。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'RosterExclusions', @level2type = N'COLUMN', @level2name = N'Note';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'不列入出勤的使用者，關聯 identity.Users 主鍵；每人最多一列。', @level0type = N'SCHEMA', @level0name = N'attendance', @level1type = N'TABLE', @level1name = N'RosterExclusions', @level2type = N'COLUMN', @level2name = N'UserId';


GO

