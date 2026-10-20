CREATE TABLE [mail].[DeliverySettings] (
    [Id]          TINYINT            NOT NULL,
    [AutoRetry]   BIT                NOT NULL,
    [UpdatedAt]   DATETIMEOFFSET (7) NOT NULL,
    [UpdatedById] UNIQUEIDENTIFIER   NOT NULL,
    CONSTRAINT [pk_DeliverySettings] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_DeliverySettings_Id] CHECK ([Id]=(1)),
    CONSTRAINT [fk_DeliverySettings_Users] FOREIGN KEY ([UpdatedById]) REFERENCES [identity].[Users] ([Id])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'郵件寄送的執行期設定（單列，Id 固定為 1），由管理頁修改、不必重啟；沒有列時使用預設值。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliverySettings';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'暫時性失敗是否自動退避重試；關閉時第一次失敗就結束（仍可手動重寄）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliverySettings', @level2type = N'COLUMN', @level2name = N'AutoRetry';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'固定為 1，確保只有一列。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliverySettings', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後修改時間（UTC）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliverySettings', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最後修改者的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliverySettings', @level2type = N'COLUMN', @level2name = N'UpdatedById';


GO

