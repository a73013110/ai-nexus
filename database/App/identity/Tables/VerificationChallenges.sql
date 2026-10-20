CREATE TABLE [identity].[VerificationChallenges] (
    [Id]            UNIQUEIDENTIFIER   NOT NULL,
    [UserId]        UNIQUEIDENTIFIER   NOT NULL,
    [Purpose]       NVARCHAR (32)      NOT NULL,
    [Target]        NVARCHAR (254)     NULL,
    [CodeHash]      BINARY (32)        NOT NULL,
    [Attempts]      INT                NOT NULL,
    [CreatedAt]     DATETIMEOFFSET (7) NOT NULL,
    [ExpiresAt]     DATETIMEOFFSET (7) NOT NULL,
    [ConsumedAt]    DATETIMEOFFSET (7) NULL,
    [RevokedAt]     DATETIMEOFFSET (7) NULL,
    [MailMessageId] UNIQUEIDENTIFIER   NULL,
    CONSTRAINT [pk_VerificationChallenges] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_VerificationChallenges_Purpose] CHECK ([Purpose]=N'reauthentication' OR [Purpose]=N'email_change'),
    CONSTRAINT [fk_VerificationChallenges_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_VerificationChallenges_UserId_CreatedAt]
    ON [identity].[VerificationChallenges]([UserId] ASC, [CreatedAt] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_VerificationChallenges_UserId_Purpose]
    ON [identity].[VerificationChallenges]([UserId] ASC, [Purpose] ASC) WHERE ([ConsumedAt] IS NULL AND [RevokedAt] IS NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'帳號自助維護的一次性驗證碼（變更信箱、步升驗證）：只存雜湊、15 分鐘到期、最多 5 次嘗試、單次使用；每人每目的最多一筆未結束。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'已輸入錯誤的次數；達上限即撤銷，需重新索取。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'Attempts';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'驗證碼的 HMAC-SHA256（金鑰為 Id 的 16 bytes）；不保存明碼。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'CodeHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'驗證成功（已使用）的時間；空值表示尚未使用。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'ConsumedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'發出時間（UTC）；也用於每小時發送上限計數。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'到期時間；之後一律拒絕。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'ExpiresAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼，由伺服器產生；也是驗證碼雜湊的金鑰。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'寄出驗證碼的 mail.OutboundMessages 主鍵，供查詢寄送狀態；刻意不設外鍵，郵件依保存期限刪除時不受阻擋。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'MailMessageId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'用途：email_change（驗證新信箱）、reauthentication（敏感操作前的步升驗證）。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'Purpose';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'撤銷時間：被新的驗證碼取代或錯誤次數達上限；空值表示未撤銷。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'RevokedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'用途的目標值（email_change 為新信箱）；確認時使用這裡的值，不接受請求重帶。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'Target';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請者的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'VerificationChallenges', @level2type = N'COLUMN', @level2name = N'UserId';


GO

