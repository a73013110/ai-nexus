CREATE TABLE [collaboration].[Resources] (
    [Id]        UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]   UNIQUEIDENTIFIER   NOT NULL,
    [Kind]      NVARCHAR (24)      NOT NULL,
    [Name]      NVARCHAR (120)     NOT NULL,
    [ParentId]  UNIQUEIDENTIFIER   NULL,
    [IsDeleted] BIT                NOT NULL,
    [CreatedAt] DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_Resources] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Resources_Resources_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [collaboration].[Resources] ([Id]),
    CONSTRAINT [FK_Resources_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_Resources_Kind_UpdatedAt]
    ON [collaboration].[Resources]([Kind] ASC, [UpdatedAt] ASC)
    INCLUDE([OwnerId], [ParentId], [IsDeleted]);


GO

CREATE NONCLUSTERED INDEX [IX_Resources_OwnerId_Kind_UpdatedAt]
    ON [collaboration].[Resources]([OwnerId] ASC, [Kind] ASC, [UpdatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_Resources_ParentId]
    ON [collaboration].[Resources]([ParentId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'共用資源的擁有者、種類、階層及版本；作為資料 ACL 邊界。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否邏輯刪除；不自動刪除歷史紀錄。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources', @level2type = N'COLUMN', @level2name = N'IsDeleted';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資源種類。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的顯示名稱。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'父資源識別碼；子資源繼承父資源的 ACL。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources', @level2type = N'COLUMN', @level2name = N'ParentId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料最後修改時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'Resources', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

