CREATE TABLE [identity].[UserPreferences] (
    [UserId]                 UNIQUEIDENTIFIER NOT NULL,
    [Theme]                  NVARCHAR (12)    NOT NULL,
    [ReducedMotion]          BIT              NOT NULL,
    [DefaultModelId]         NVARCHAR (160)   NULL,
    [ReadingLineHeight]      FLOAT (53)       DEFAULT ((1.2000000000000000e+000)) NOT NULL,
    [Density]                NVARCHAR (16)    DEFAULT (N'comfortable') NOT NULL,
    [SidebarWidth]           INT              DEFAULT ((240)) NOT NULL,
    [ReadingWidth]           NVARCHAR (16)    DEFAULT (N'standard') NOT NULL,
    [EnterToSend]            BIT              DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [AutoFollow]             BIT              DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [SaveLocalDrafts]        BIT              DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [NotifyOnCompletion]     BIT              NOT NULL,
    [DefaultReasoningEffort] NVARCHAR (16)    DEFAULT (N'auto') NOT NULL,
    [TextScale]              INT              CONSTRAINT [df_UserPreferences_TextScale] DEFAULT ((100)) NOT NULL,
    CONSTRAINT [PK_UserPreferences] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [ck_UserPreferences_TextScale] CHECK ([TextScale]>=(100) AND [TextScale]<=(130)),
    CONSTRAINT [FK_UserPreferences_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者個人外觀、閱讀、對話操作與通知偏好；不含服務密鑰。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'生成時是否跟隨最新回答。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'AutoFollow';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'偏好的核准模型識別碼；空值使用伺服器預設。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'DefaultModelId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'偏好的推理強度。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'DefaultReasoningEffort';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面密度偏好。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'Density';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否以 Enter 送出提問；IME 組字不送出。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'EnterToSend';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否在背景分頁提醒回答完成。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'NotifyOnCompletion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對話閱讀行高倍率。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'ReadingLineHeight';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'閱讀區寬度偏好。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'ReadingWidth';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否減少動畫與動態效果。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'ReducedMotion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否在瀏覽器按使用者保存草稿。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'SaveLocalDrafts';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'側欄寬度偏好。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'SidebarWidth';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'全站文字大小百分比（100–130），套用在根元素字級，介面字級與間距一起縮放。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'TextScale';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'外觀偏好：system、light 或 dark。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'Theme';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'偏好所屬使用者的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'UserPreferences', @level2type = N'COLUMN', @level2name = N'UserId';


GO

