CREATE TABLE [accesscontrol].[GroupModelPolicies] (
    [GroupId]                    NVARCHAR (64)   NOT NULL,
    [AllowedModelsJson]          NVARCHAR (4000) NULL,
    [DailyTokenLimitsJson]       NVARCHAR (MAX)  NULL,
    [StoredAttachmentLimitBytes] BIGINT          NULL,
    CONSTRAINT [PK_GroupModelPolicies] PRIMARY KEY CLUSTERED ([GroupId] ASC),
    CONSTRAINT [FK_GroupModelPolicies_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [accesscontrol].[RoleGroups] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'功能群組的模型白名單、各模型每日 token 及附件空間限制。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'GroupModelPolicies';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'GroupModelPolicies', @level2type = N'COLUMN', @level2name = N'AllowedModelsJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'GroupModelPolicies', @level2type = N'COLUMN', @level2name = N'DailyTokenLimitsJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯功能群組的識別碼。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'GroupModelPolicies', @level2type = N'COLUMN', @level2name = N'GroupId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'個人附件儲存上限，以 bytes 計；群組限制取最低值。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'GroupModelPolicies', @level2type = N'COLUMN', @level2name = N'StoredAttachmentLimitBytes';


GO

