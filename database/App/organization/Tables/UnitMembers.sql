CREATE TABLE [organization].[UnitMembers] (
    [UnitId]    UNIQUEIDENTIFIER NOT NULL,
    [UserId]    UNIQUEIDENTIFIER NOT NULL,
    [IsPrimary] BIT              NOT NULL,
    [IsManager] BIT              NOT NULL,
    CONSTRAINT [pk_UnitMembers] PRIMARY KEY CLUSTERED ([UnitId] ASC, [UserId] ASC),
    CONSTRAINT [fk_UnitMembers_Units] FOREIGN KEY ([UnitId]) REFERENCES [organization].[Units] ([Id]),
    CONSTRAINT [fk_UnitMembers_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_UnitMembers_UserId]
    ON [organization].[UnitMembers]([UserId] ASC)
    INCLUDE([IsPrimary], [IsManager]);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_UnitMembers_UserId_Primary]
    ON [organization].[UnitMembers]([UserId] ASC) WHERE ([IsPrimary]=(1));


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者目前所屬的組織單位；一人可屬多個單位，最多一個主要所屬。只記錄現況，不保留異動歷史。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'UnitMembers';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否為該單位的主管。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'UnitMembers', @level2type = N'COLUMN', @level2name = N'IsManager';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否為使用者的主要所屬單位；每位使用者最多一筆（filtered unique index）。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'UnitMembers', @level2type = N'COLUMN', @level2name = N'IsPrimary';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯組織單位的 Units 主鍵。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'UnitMembers', @level2type = N'COLUMN', @level2name = N'UnitId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯使用者的 Users 主鍵；使用者只做軟刪除，所屬保留到管理員移除。', @level0type = N'SCHEMA', @level0name = N'organization', @level1type = N'TABLE', @level1name = N'UnitMembers', @level2type = N'COLUMN', @level2name = N'UserId';


GO

