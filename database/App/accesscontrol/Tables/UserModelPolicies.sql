CREATE TABLE [accesscontrol].[UserModelPolicies] (
    [UserId]               UNIQUEIDENTIFIER NOT NULL,
    [AllowedModelsJson]    NVARCHAR (4000)  NULL,
    [DailyTokenLimitsJson] NVARCHAR (MAX)   NULL,
    CONSTRAINT [PK_UserModelPolicies] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [FK_UserModelPolicies_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者的模型白名單與各模型每日 token 覆寫政策。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'UserModelPolicies';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'UserModelPolicies', @level2type = N'COLUMN', @level2name = N'AllowedModelsJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'UserModelPolicies', @level2type = N'COLUMN', @level2name = N'DailyTokenLimitsJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯使用者的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'accesscontrol', @level1type = N'TABLE', @level1name = N'UserModelPolicies', @level2type = N'COLUMN', @level2name = N'UserId';


GO

