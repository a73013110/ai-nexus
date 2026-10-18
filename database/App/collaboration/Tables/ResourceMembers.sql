CREATE TABLE [collaboration].[ResourceMembers] (
    [ResourceId] UNIQUEIDENTIFIER NOT NULL,
    [UserId]     UNIQUEIDENTIFIER NOT NULL,
    [Role]       NVARCHAR (12)    NOT NULL,
    CONSTRAINT [PK_ResourceMembers] PRIMARY KEY CLUSTERED ([ResourceId] ASC, [UserId] ASC),
    CONSTRAINT [FK_ResourceMembers_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ResourceMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ResourceMembers_UserId]
    ON [collaboration].[ResourceMembers]([UserId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資源對具名使用者授予的閱讀或編輯權限。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'ResourceMembers';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯 Resources 的識別碼。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'ResourceMembers', @level2type = N'COLUMN', @level2name = N'ResourceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'具名成員的權限：viewer 或 editor。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'ResourceMembers', @level2type = N'COLUMN', @level2name = N'Role';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯使用者的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'collaboration', @level1type = N'TABLE', @level1name = N'ResourceMembers', @level2type = N'COLUMN', @level2name = N'UserId';


GO

