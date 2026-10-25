CREATE TABLE [identity].[Users] (
    [Id]                   UNIQUEIDENTIFIER   NOT NULL,
    [Sid]                  NVARCHAR (184)     NOT NULL,
    [DisplayName]          NVARCHAR (256)     NOT NULL,
    [Enabled]              BIT                DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [AttachmentLimitBytes] BIGINT             NULL,
    [DeletedAt]            DATETIMEOFFSET (7) NULL,
    [AdEnabled]            BIT                DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [LocalEnabled]         BIT                NOT NULL,
    [AdAccount]            NVARCHAR (64)      NULL,
    [LocalAccount]         NVARCHAR (64)      NULL,
    [PasswordHash]         NVARCHAR (512)     NULL,
    [SecurityVersion]      INT                NOT NULL,
    [ProfileManaged]       BIT                NOT NULL,
    [FailedLogins]         INT                NOT NULL,
    [LockedUntil]          DATETIMEOFFSET (7) NULL,
    [LastSeenAt]           DATETIMEOFFSET (7) NOT NULL,
    [Email]                NVARCHAR (254)     NULL,
    [EmailSource]          NVARCHAR (16)      NULL,
    [EmailVerifiedAt]      DATETIMEOFFSET (7) NULL,
    [DirectoryVerifiedAt]  DATETIMEOFFSET (7) NULL,
    [PasswordChangedAt]    DATETIMEOFFSET (7) NULL,
    [SortOrder]            INT                NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CK_Users_AttachmentLimitBytes] CHECK ([AttachmentLimitBytes] IS NULL OR [AttachmentLimitBytes]>=(0) AND [AttachmentLimitBytes]<=(1000000000000000.)),
    CONSTRAINT [ck_Users_EmailSource] CHECK ([Email] IS NULL AND [EmailSource] IS NULL AND [EmailVerifiedAt] IS NULL OR [Email] IS NOT NULL AND ([EmailSource]=N'admin' OR [EmailSource]=N'user' OR [EmailSource]=N'directory')),
    CONSTRAINT [ck_Users_LoginName] CHECK ([AdAccount] IS NOT NULL OR [LocalAccount] IS NOT NULL),
    CONSTRAINT [ck_Users_SortOrder] CHECK ([SortOrder] IS NULL OR [SortOrder]>=(0) AND [SortOrder]<=(1000000))
);


GO

CREATE NONCLUSTERED INDEX [ix_Users_Email]
    ON [identity].[Users]([Email] ASC) WHERE ([Email] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_AdAccount]
    ON [identity].[Users]([AdAccount] ASC) WHERE ([AdAccount] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_LocalAccount]
    ON [identity].[Users]([LocalAccount] ASC) WHERE ([LocalAccount] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_Sid]
    ON [identity].[Users]([Sid] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者身分、AD SID 綁定、可用登入方式與工作階段撤銷版本；不保存 AD 密碼。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'AD 短帳號（sAMAccountName，保留目錄的大小寫，不含網域）；管理者預先配置或 AD 登入時補上，唯一且不分大小寫，驗證成功後以 SID 固定綁定。畫面顯示的帳號以它優先。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'AdAccount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否允許使用 AD／Windows 整合驗證登入。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'AdEnabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'管理者設定的個人容量上限 bytes；優先於群組，空值使用群組或預設 5 GB。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'AttachmentLimitBytes';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'登入身分刪除時間；保留關聯與歷史資料。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'DeletedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最近一次 AD／Windows 驗證成功的時間；本地密碼登入的目錄新鮮度規則依此判斷。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'DirectoryVerifiedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者的介面顯示名稱。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'DisplayName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'通知信箱（不是登入識別、不唯一）；寄送驗證碼與帳號安全通知用。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Email';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'信箱來源：directory（AD mail 屬性）、user（本人驗證）、admin（管理者設定）；決定本人能否自行修改。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'EmailSource';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'信箱驗證時間；directory 寫入時即設定，user 驗證成功時設定，空值表示未驗證。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'EmailVerifiedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否啟用；停用不刪除歷史資料。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Enabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'本地登入連續失敗次數，用於暫時鎖定。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'FailedLogins';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者最近登入／活動時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'LastSeenAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'本地登入帳號（保留使用者輸入的大小寫）；唯一且不分大小寫。沒有 AD 帳號時畫面以它顯示。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'LocalAccount';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否允許本地密碼登入；與 AD 驗證獨立。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'LocalEnabled';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'本地帳號暫時鎖定的到期時間；空值表示未鎖定。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'LockedUntil';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'本地密碼最近一次設定或變更的時間。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'PasswordChangedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'本地密碼的 Argon2id PHC 雜湊，含版本、成本、隨機 salt 與衍生值；不可還原。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'PasswordHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'姓名與信箱是否由管理者維護；開啟後 AD 目錄不覆寫姓名與信箱。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'ProfileManaged';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'登入政策或密碼變更時遞增，立即撤銷舊工作階段。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'SecurityVersion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'AD 的不可變 SID；尚未綁定 AD 的手動帳號使用 managed: 識別碼。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Sid';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'依人分組的列表（出勤紀錄、匯出）中的順序，小的在前；空值表示未排序，排在已排序者之後並依姓名、帳號排列。管理者在使用者頁拖曳設定，值之間保留間隔。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'SortOrder';


GO

