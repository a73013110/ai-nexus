IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'access') IS NULL EXEC(N'CREATE SCHEMA [access];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'content') IS NULL EXEC(N'CREATE SCHEMA [content];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'attachments') IS NULL EXEC(N'CREATE SCHEMA [attachments];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'operations') IS NULL EXEC(N'CREATE SCHEMA [operations];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'knowledge') IS NULL EXEC(N'CREATE SCHEMA [knowledge];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'conversations') IS NULL EXEC(N'CREATE SCHEMA [conversations];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'quality') IS NULL EXEC(N'CREATE SCHEMA [quality];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'inference') IS NULL EXEC(N'CREATE SCHEMA [inference];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'projects') IS NULL EXEC(N'CREATE SCHEMA [projects];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'library') IS NULL EXEC(N'CREATE SCHEMA [library];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'workspace') IS NULL EXEC(N'CREATE SCHEMA [workspace];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'collaboration') IS NULL EXEC(N'CREATE SCHEMA [collaboration];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [operations].[AuditEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [OwnerId] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [Action] nvarchar(64) NOT NULL,
        [ResourceId] uniqueidentifier NULL,
        [Result] nvarchar(80) NULL,
        [DetailsJson] nvarchar(max) NULL,
        [At] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
    DECLARE @description AS sql_variant;
    SET @description = N'操作與管理異動稽核；保存實際管理者及有效身分，不記錄密碼或私密內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents';
    SET @description = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'Id';
    SET @description = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'OwnerId';
    SET @description = N'身分測試時實際發起操作的管理者；空值表示與 OwnerId 相同。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'ActorId';
    SET @description = N'稽核操作名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'Action';
    SET @description = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'ResourceId';
    SET @description = N'稽核操作結果或失敗代碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'Result';
    SET @description = N'稽核前後狀態或操作範圍 JSON；不含密碼、hash、token 或對話內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'DetailsJson';
    SET @description = N'稽核事件發生時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'At';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [access].[Features] (
        [Id] nvarchar(64) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Route] nvarchar(160) NOT NULL,
        [SortOrder] int NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_Features] PRIMARY KEY ([Id])
    );
    DECLARE @description1 AS sql_variant;
    SET @description1 = N'模組註冊的功能入口、顯示名稱、路由及啟用狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', N'access', 'TABLE', N'Features';
    SET @description1 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'Id';
    SET @description1 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'Name';
    SET @description1 = N'功能入口的本站路由；API 授權仍由後端政策判定。';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'Route';
    SET @description1 = N'介面顯示順序；數值越小越前。';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'SortOrder';
    SET @description1 = N'是否啟用；停用不刪除歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [inference].[ModelProfiles] (
        [Id] nvarchar(160) NOT NULL,
        [Provider] nvarchar(32) NOT NULL,
        [ProviderModelId] nvarchar(150) NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL,
        [ContextTokens] int NOT NULL,
        [MaxOutputTokens] int NOT NULL,
        [SupportsStreaming] bit NOT NULL,
        [SupportsUsage] bit NOT NULL,
        CONSTRAINT [PK_ModelProfiles] PRIMARY KEY ([Id])
    );
    DECLARE @description2 AS sql_variant;
    SET @description2 = N'核准模型的能力、上下文與輸出限制。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles';
    SET @description2 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'Id';
    SET @description2 = N'模型或搜尋服務供應商識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'Provider';
    SET @description2 = N'送往指定供應商的原生模型識別碼，與核准路由識別碼分開保存。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'ProviderModelId';
    SET @description2 = N'使用者或模型的介面顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'DisplayName';
    SET @description2 = N'模型上下文容量，以 tokens 計。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'ContextTokens';
    SET @description2 = N'模型核准的最大輸出 tokens。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'MaxOutputTokens';
    SET @description2 = N'模型是否支援串流輸出。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'SupportsStreaming';
    SET @description2 = N'模型是否會回報實際用量。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'SupportsUsage';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [access].[RoleGroups] (
        [Id] nvarchar(64) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_RoleGroups] PRIMARY KEY ([Id])
    );
    DECLARE @description3 AS sql_variant;
    SET @description3 = N'角色所加入的功能群組，集中管理功能及模型政策。';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', N'access', 'TABLE', N'RoleGroups';
    SET @description3 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', N'access', 'TABLE', N'RoleGroups', 'COLUMN', N'Id';
    SET @description3 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', N'access', 'TABLE', N'RoleGroups', 'COLUMN', N'Name';
    SET @description3 = N'是否啟用；停用不刪除歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', N'access', 'TABLE', N'RoleGroups', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [access].[Roles] (
        [Id] nvarchar(64) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
    DECLARE @description4 AS sql_variant;
    SET @description4 = N'可分派給使用者的角色；停用後不再提供有效授權。';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', N'access', 'TABLE', N'Roles';
    SET @description4 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', N'access', 'TABLE', N'Roles', 'COLUMN', N'Id';
    SET @description4 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', N'access', 'TABLE', N'Roles', 'COLUMN', N'Name';
    SET @description4 = N'是否啟用；停用不刪除歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', N'access', 'TABLE', N'Roles', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [identity].[Users] (
        [Id] uniqueidentifier NOT NULL,
        [Sid] nvarchar(184) NOT NULL,
        [Account] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(256) NOT NULL,
        [Enabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [AttachmentLimitBytes] bigint NULL,
        [DeletedAt] datetimeoffset NULL,
        [AdEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [LocalEnabled] bit NOT NULL,
        [AdAccount] nvarchar(64) NULL,
        [LocalAccount] nvarchar(64) NULL,
        [PasswordHash] nvarchar(512) NULL,
        [SecurityVersion] int NOT NULL,
        [ProfileManaged] bit NOT NULL,
        [FailedLogins] int NOT NULL,
        [LockedUntil] datetimeoffset NULL,
        [LastSeenAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_AttachmentLimitBytes] CHECK ([AttachmentLimitBytes] IS NULL OR [AttachmentLimitBytes] BETWEEN 0 AND 1000000000000000)
    );
    DECLARE @description5 AS sql_variant;
    SET @description5 = N'使用者身分、AD SID 綁定、可用登入方式與工作階段撤銷版本；不保存 AD 密碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users';
    SET @description5 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'Id';
    SET @description5 = N'AD 的不可變 SID；尚未綁定 AD 的手動帳號使用 managed: 識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'Sid';
    SET @description5 = N'登入身分顯示帳號；AD 連結後保存目錄提供的帳號。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'Account';
    SET @description5 = N'使用者或模型的介面顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'DisplayName';
    SET @description5 = N'是否啟用；停用不刪除歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'Enabled';
    SET @description5 = N'管理者設定的個人容量上限 bytes；優先於群組，空值使用群組或預設 5 GB。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'AttachmentLimitBytes';
    SET @description5 = N'登入身分刪除時間；保留關聯與歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'DeletedAt';
    SET @description5 = N'是否允許使用 AD／Windows 整合驗證登入。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'AdEnabled';
    SET @description5 = N'是否允許本地密碼登入；與 AD 驗證獨立。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'LocalEnabled';
    SET @description5 = N'預先配置的 AD 帳號正規化值；唯一、不含網域，驗證成功後以 SID 固定綁定。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'AdAccount';
    SET @description5 = N'本地登入帳號正規化值；唯一且不區分大小寫。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'LocalAccount';
    SET @description5 = N'本地密碼的 Argon2id PHC 雜湊，含版本、成本、隨機 salt 與衍生值；不可還原。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'PasswordHash';
    SET @description5 = N'登入政策或密碼變更時遞增，立即撤銷舊工作階段。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'SecurityVersion';
    SET @description5 = N'姓名是否由管理者維護；開啟後 AD 目錄不覆寫姓名。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'ProfileManaged';
    SET @description5 = N'本地登入連續失敗次數，用於暫時鎖定。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'FailedLogins';
    SET @description5 = N'本地帳號暫時鎖定的到期時間；空值表示未鎖定。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'LockedUntil';
    SET @description5 = N'使用者最近登入／活動時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'LastSeenAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [access].[GroupModelPolicies] (
        [GroupId] nvarchar(64) NOT NULL,
        [AllowedModelsJson] nvarchar(4000) NULL,
        [DailyRequestLimit] int NULL,
        [StoredAttachmentLimitBytes] bigint NULL,
        CONSTRAINT [PK_GroupModelPolicies] PRIMARY KEY ([GroupId]),
        CONSTRAINT [FK_GroupModelPolicies_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [access].[RoleGroups] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description6 AS sql_variant;
    SET @description6 = N'功能群組的模型白名單、每日生成及附件空間限制。';
    EXEC sp_addextendedproperty 'MS_Description', @description6, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies';
    SET @description6 = N'關聯功能群組的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description6, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'GroupId';
    SET @description6 = N'模型白名單 JSON；空值不增加限制，空陣列禁止生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description6, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'AllowedModelsJson';
    SET @description6 = N'每日生成次數上限；群組限制取最低值，UTC 午夜重設。';
    EXEC sp_addextendedproperty 'MS_Description', @description6, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'DailyRequestLimit';
    SET @description6 = N'個人附件儲存上限，以 bytes 計；群組限制取最低值。';
    EXEC sp_addextendedproperty 'MS_Description', @description6, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'StoredAttachmentLimitBytes';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [access].[RoleGroupFeatures] (
        [GroupId] nvarchar(64) NOT NULL,
        [FeatureId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_RoleGroupFeatures] PRIMARY KEY ([GroupId], [FeatureId]),
        CONSTRAINT [FK_RoleGroupFeatures_Features_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [access].[Features] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RoleGroupFeatures_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [access].[RoleGroups] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description7 AS sql_variant;
    SET @description7 = N'功能群組與功能的授權關聯；有效功能取聯集。';
    EXEC sp_addextendedproperty 'MS_Description', @description7, 'SCHEMA', N'access', 'TABLE', N'RoleGroupFeatures';
    SET @description7 = N'關聯功能群組的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description7, 'SCHEMA', N'access', 'TABLE', N'RoleGroupFeatures', 'COLUMN', N'GroupId';
    SET @description7 = N'關聯功能的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description7, 'SCHEMA', N'access', 'TABLE', N'RoleGroupFeatures', 'COLUMN', N'FeatureId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [access].[RoleGroupRoles] (
        [RoleId] nvarchar(64) NOT NULL,
        [GroupId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_RoleGroupRoles] PRIMARY KEY ([RoleId], [GroupId]),
        CONSTRAINT [FK_RoleGroupRoles_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [access].[RoleGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RoleGroupRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [access].[Roles] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description8 AS sql_variant;
    SET @description8 = N'角色與功能群組的授權關聯。';
    EXEC sp_addextendedproperty 'MS_Description', @description8, 'SCHEMA', N'access', 'TABLE', N'RoleGroupRoles';
    SET @description8 = N'關聯角色的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description8, 'SCHEMA', N'access', 'TABLE', N'RoleGroupRoles', 'COLUMN', N'RoleId';
    SET @description8 = N'關聯功能群組的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description8, 'SCHEMA', N'access', 'TABLE', N'RoleGroupRoles', 'COLUMN', N'GroupId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [access].[AdministratorBootstraps] (
        [UserId] uniqueidentifier NOT NULL,
        [GrantedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AdministratorBootstraps] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_AdministratorBootstraps_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description9 AS sql_variant;
    SET @description9 = N'管理者首次啟動授權的永久標記，防止撤銷後重新授權。';
    EXEC sp_addextendedproperty 'MS_Description', @description9, 'SCHEMA', N'access', 'TABLE', N'AdministratorBootstraps';
    SET @description9 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description9, 'SCHEMA', N'access', 'TABLE', N'AdministratorBootstraps', 'COLUMN', N'UserId';
    SET @description9 = N'角色或資源授權建立時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description9, 'SCHEMA', N'access', 'TABLE', N'AdministratorBootstraps', 'COLUMN', N'GrantedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [attachments].[Attachments] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(180) NOT NULL,
        [ContentType] nvarchar(80) NOT NULL,
        [Size] bigint NOT NULL,
        [StorageKey] nvarchar(32) NOT NULL,
        [StorageState] nvarchar(16) NOT NULL,
        [ExtractedText] nvarchar(max) NULL,
        [InLibrary] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Attachments_Size] CHECK ([Size] > 0),
        CONSTRAINT [CK_Attachments_StorageState] CHECK ([StorageState] IN ('pending', 'ready', 'deleting')),
        CONSTRAINT [FK_Attachments_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description10 AS sql_variant;
    SET @description10 = N'站外附件原檔的 metadata、儲存識別、擷取文字及生命週期；不保存原始 bytes。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments';
    SET @description10 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'Id';
    SET @description10 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'OwnerId';
    SET @description10 = N'原始附件檔名，不作為伺服器儲存路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'FileName';
    SET @description10 = N'核准的 MIME 型別。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'ContentType';
    SET @description10 = N'原始附件大小，以 bytes 計。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'Size';
    SET @description10 = N'站外原檔的不可變隨機識別碼；不含使用者路徑或檔名。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'StorageKey';
    SET @description10 = N'原檔儲存狀態 pending／ready／deleting；刪檔成功才釋放 metadata 與容量。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'StorageState';
    SET @description10 = N'附件分析後的文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'ExtractedText';
    SET @description10 = N'是否由個人檔案庫獨立保留原檔；移除對話或知識索引不會刪除保留的檔案。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'InLibrary';
    SET @description10 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [inference].[ModelInvocations] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(32) NOT NULL,
        [ModelId] nvarchar(160) NOT NULL,
        [Provider] nvarchar(32) NOT NULL,
        [DurationMilliseconds] bigint NULL,
        [Status] nvarchar(16) NOT NULL,
        [InputTokens] bigint NULL,
        [OutputTokens] bigint NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ModelInvocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ModelInvocations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description11 AS sql_variant;
    SET @description11 = N'文字、OCR、embedding 等模型呼叫的狀態與實際用量。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations';
    SET @description11 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'Id';
    SET @description11 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'OwnerId';
    SET @description11 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'Kind';
    SET @description11 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'ModelId';
    SET @description11 = N'模型或搜尋服務供應商識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'Provider';
    SET @description11 = N'從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'DurationMilliseconds';
    SET @description11 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'Status';
    SET @description11 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'InputTokens';
    SET @description11 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'OutputTokens';
    SET @description11 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [inference].[ModelPrices] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] nvarchar(32) NOT NULL,
        [ModelId] nvarchar(160) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Kind] nvarchar(16) NOT NULL,
        [InputPerMillion] decimal(20,8) NOT NULL,
        [CachedInputPerMillion] decimal(20,8) NOT NULL,
        [OutputPerMillion] decimal(20,8) NOT NULL,
        [PerRequest] decimal(20,8) NOT NULL,
        [RequestCharge] nvarchar(16) NOT NULL,
        [Note] nvarchar(500) NOT NULL,
        [EffectiveAt] datetimeoffset NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ModelPrices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ModelPrices_Users_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description12 AS sql_variant;
    SET @description12 = N'依供應商、模型、幣別及成本類型保存的不可變價格版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices';
    SET @description12 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Id';
    SET @description12 = N'模型或搜尋服務供應商識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Provider';
    SET @description12 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'ModelId';
    SET @description12 = N'費用幣別代碼；不同幣別不可直接合計。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Currency';
    SET @description12 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Kind';
    SET @description12 = N'每百萬輸入 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'InputPerMillion';
    SET @description12 = N'每百萬快取輸入 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'CachedInputPerMillion';
    SET @description12 = N'每百萬輸出 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'OutputPerMillion';
    SET @description12 = N'每次呼叫的固定單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'PerRequest';
    SET @description12 = N'搜尋服務每次請求的費用。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'RequestCharge';
    SET @description12 = N'使用者提供的補充說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Note';
    SET @description12 = N'此價格版本開始生效的時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'EffectiveAt';
    SET @description12 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'CreatedAt';
    SET @description12 = N'建立紀錄的使用者識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'CreatedBy';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [library].[PromptTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Title] nvarchar(80) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PromptTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PromptTemplates_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description13 AS sql_variant;
    SET @description13 = N'使用者私人提示詞範本。';
    EXEC sp_addextendedproperty 'MS_Description', @description13, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates';
    SET @description13 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description13, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'Id';
    SET @description13 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description13, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'OwnerId';
    SET @description13 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description13, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'Title';
    SET @description13 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description13, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'Content';
    SET @description13 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description13, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [workspace].[RepositoryConnections] (
        [OwnerId] uniqueidentifier NOT NULL,
        [BaseUrl] nvarchar(500) NOT NULL,
        [Login] nvarchar(100) NOT NULL,
        [ProtectedToken] nvarchar(max) NOT NULL,
        [ConnectedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RepositoryConnections] PRIMARY KEY ([OwnerId]),
        CONSTRAINT [FK_RepositoryConnections_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description14 AS sql_variant;
    SET @description14 = N'使用者個人的 Gitea 連線及 Data Protection 保護的存取 token。';
    EXEC sp_addextendedproperty 'MS_Description', @description14, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections';
    SET @description14 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description14, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'OwnerId';
    SET @description14 = N'Gitea 連線主機位址。';
    EXEC sp_addextendedproperty 'MS_Description', @description14, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'BaseUrl';
    SET @description14 = N'外部服務的使用者登入名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description14, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'Login';
    SET @description14 = N'以 ASP.NET Data Protection 保護的外部 token；不可在 API、稽核或日誌回傳。';
    EXEC sp_addextendedproperty 'MS_Description', @description14, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'ProtectedToken';
    SET @description14 = N'使用者建立外部服務連線的時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description14, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'ConnectedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [collaboration].[Resources] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(24) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Resources] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Resources_Resources_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Resources_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description15 AS sql_variant;
    SET @description15 = N'共用資源的擁有者、種類、階層及版本；作為資料 ACL 邊界。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources';
    SET @description15 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'Id';
    SET @description15 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'OwnerId';
    SET @description15 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'Kind';
    SET @description15 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'Name';
    SET @description15 = N'父訊息或父資源識別碼，用於分支或階層繼承。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'ParentId';
    SET @description15 = N'是否邏輯刪除；不自動刪除歷史紀錄。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'IsDeleted';
    SET @description15 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'CreatedAt';
    SET @description15 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [identity].[UserPreferences] (
        [NexusUserId] uniqueidentifier NOT NULL,
        [Theme] nvarchar(12) NOT NULL,
        [ReducedMotion] bit NOT NULL,
        [DefaultModelId] nvarchar(160) NULL,
        [ReadingFontSize] int NOT NULL DEFAULT 17,
        [ReadingLineHeight] float NOT NULL DEFAULT 1.8E0,
        [Density] nvarchar(16) NOT NULL DEFAULT N'comfortable',
        [SidebarWidth] int NOT NULL DEFAULT 264,
        [ReadingWidth] nvarchar(16) NOT NULL DEFAULT N'standard',
        [EnterToSend] bit NOT NULL DEFAULT CAST(1 AS bit),
        [AutoFollow] bit NOT NULL DEFAULT CAST(1 AS bit),
        [SaveLocalDrafts] bit NOT NULL DEFAULT CAST(1 AS bit),
        [NotifyOnCompletion] bit NOT NULL,
        [DefaultReasoningEffort] nvarchar(16) NOT NULL DEFAULT N'auto',
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([NexusUserId]),
        CONSTRAINT [FK_UserPreferences_Users_NexusUserId] FOREIGN KEY ([NexusUserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description16 AS sql_variant;
    SET @description16 = N'使用者個人外觀、閱讀、對話操作與通知偏好；不含服務密鑰。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences';
    SET @description16 = N'個人偏好對應使用者的主鍵與外鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'NexusUserId';
    SET @description16 = N'外觀偏好：system、light 或 dark。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'Theme';
    SET @description16 = N'是否減少動畫與動態效果。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'ReducedMotion';
    SET @description16 = N'偏好的核准模型識別碼；空值使用伺服器預設。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'DefaultModelId';
    SET @description16 = N'對話文字大小，以 CSS px 的偏好值記錄。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'ReadingFontSize';
    SET @description16 = N'對話閱讀行高倍率。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'ReadingLineHeight';
    SET @description16 = N'介面密度偏好。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'Density';
    SET @description16 = N'側欄寬度偏好。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'SidebarWidth';
    SET @description16 = N'閱讀區寬度偏好。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'ReadingWidth';
    SET @description16 = N'是否以 Enter 送出提問；IME 組字不送出。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'EnterToSend';
    SET @description16 = N'生成時是否跟隨最新回答。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'AutoFollow';
    SET @description16 = N'是否在瀏覽器按使用者保存草稿。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'SaveLocalDrafts';
    SET @description16 = N'是否在背景分頁提醒回答完成。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'NotifyOnCompletion';
    SET @description16 = N'偏好的推理強度。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'DefaultReasoningEffort';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [access].[UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [access].[Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description17 AS sql_variant;
    SET @description17 = N'使用者與角色的分派關聯。';
    EXEC sp_addextendedproperty 'MS_Description', @description17, 'SCHEMA', N'access', 'TABLE', N'UserRoles';
    SET @description17 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description17, 'SCHEMA', N'access', 'TABLE', N'UserRoles', 'COLUMN', N'UserId';
    SET @description17 = N'關聯角色的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description17, 'SCHEMA', N'access', 'TABLE', N'UserRoles', 'COLUMN', N'RoleId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [inference].[WebSearches] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(80) NOT NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [ResultsJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [RunId] uniqueidentifier NULL,
        CONSTRAINT [PK_WebSearches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WebSearches_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description18 AS sql_variant;
    SET @description18 = N'使用者明確啟用的網路搜尋、冪等識別、結果與費用。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches';
    SET @description18 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'Id';
    SET @description18 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'OwnerId';
    SET @description18 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'ConversationId';
    SET @description18 = N'擁有者範圍內的冪等請求識別，避免重試重複處理。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'IdempotencyKey';
    SET @description18 = N'請求內容指紋，用於辨識冪等識別碼衝突。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'RequestHash';
    SET @description18 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'Status';
    SET @description18 = N'搜尋結果的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'ResultsJson';
    SET @description18 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'CreatedAt';
    SET @description18 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'RunId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [operations].[BackgroundJobs] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(32) NOT NULL,
        [Label] nvarchar(180) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Stage] nvarchar(120) NOT NULL,
        [ActiveKey] nvarchar(100) NULL,
        [LeaseToken] uniqueidentifier NULL,
        [LeaseUntil] datetimeoffset NULL,
        [CancelRequested] bit NOT NULL,
        [Attempt] int NOT NULL,
        [CompletedUnits] int NOT NULL,
        [TotalUnits] int NULL,
        [ErrorCode] nvarchar(80) NULL,
        [ErrorMessage] nvarchar(240) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_BackgroundJobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BackgroundJobs_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BackgroundJobs_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description19 AS sql_variant;
    SET @description19 = N'文件索引與評測等背景工作的租約、進度、重試與取消狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs';
    SET @description19 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Id';
    SET @description19 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'OwnerId';
    SET @description19 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ResourceId';
    SET @description19 = N'操作所關聯的業務對象識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'SubjectId';
    SET @description19 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Kind';
    SET @description19 = N'分類標籤或階段的顯示文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Label';
    SET @description19 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Status';
    SET @description19 = N'背景工作目前階段。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Stage';
    SET @description19 = N'仍在執行工作的唯一鍵，避免同一業務重複排程。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ActiveKey';
    SET @description19 = N'背景工作租約的 fencing token，防止過期 worker 提交。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'LeaseToken';
    SET @description19 = N'背景工作租約的到期時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'LeaseUntil';
    SET @description19 = N'是否收到取消要求；不表示工作已停止。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'CancelRequested';
    SET @description19 = N'背景工作執行／重試次數。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Attempt';
    SET @description19 = N'已完成的真實工作單位數。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'CompletedUnits';
    SET @description19 = N'已知的總工作單位數；未知不表示百分比。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'TotalUnits';
    SET @description19 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ErrorCode';
    SET @description19 = N'經限制的錯誤說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ErrorMessage';
    SET @description19 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'CreatedAt';
    SET @description19 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [knowledge].[Collections] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_Collections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Collections_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description20 AS sql_variant;
    SET @description20 = N'知識庫的資料資源關聯及索引資訊。';
    EXEC sp_addextendedproperty 'MS_Description', @description20, 'SCHEMA', N'knowledge', 'TABLE', N'Collections';
    SET @description20 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description20, 'SCHEMA', N'knowledge', 'TABLE', N'Collections', 'COLUMN', N'Id';
    SET @description20 = N'業務物件的用途說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description20, 'SCHEMA', N'knowledge', 'TABLE', N'Collections', 'COLUMN', N'Description';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [quality].[EvaluationSets] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [CasesJson] nvarchar(max) NOT NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_EvaluationSets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EvaluationSets_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description21 AS sql_variant;
    SET @description21 = N'評測題庫、固定測試案例與版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description21, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets';
    SET @description21 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description21, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets', 'COLUMN', N'Id';
    SET @description21 = N'業務物件的用途說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description21, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets', 'COLUMN', N'Description';
    SET @description21 = N'固定評測案例的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description21, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets', 'COLUMN', N'CasesJson';
    SET @description21 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description21, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets', 'COLUMN', N'Version';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [projects].[Projects] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Instructions] nvarchar(4000) NOT NULL,
        [Version] int NOT NULL,
        [IsArchived] bit NOT NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Projects_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description22 AS sql_variant;
    SET @description22 = N'專案資源、共用指令及範本版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', N'projects', 'TABLE', N'Projects';
    SET @description22 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'Id';
    SET @description22 = N'業務物件的用途說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'Description';
    SET @description22 = N'專案共用指令。';
    EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'Instructions';
    SET @description22 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'Version';
    SET @description22 = N'是否封存對話；封存後不再接受新的生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'IsArchived';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [attachments].[ResourceAttachments] (
        [ResourceId] uniqueidentifier NOT NULL,
        [AttachmentId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ResourceAttachments] PRIMARY KEY ([ResourceId], [AttachmentId]),
        CONSTRAINT [FK_ResourceAttachments_Attachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [attachments].[Attachments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ResourceAttachments_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description23 AS sql_variant;
    SET @description23 = N'知識庫或專案資源與附件的引用關聯。';
    EXEC sp_addextendedproperty 'MS_Description', @description23, 'SCHEMA', N'attachments', 'TABLE', N'ResourceAttachments';
    SET @description23 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description23, 'SCHEMA', N'attachments', 'TABLE', N'ResourceAttachments', 'COLUMN', N'ResourceId';
    SET @description23 = N'引用的附件識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description23, 'SCHEMA', N'attachments', 'TABLE', N'ResourceAttachments', 'COLUMN', N'AttachmentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [collaboration].[ResourceGroups] (
        [ResourceId] uniqueidentifier NOT NULL,
        [GroupId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_ResourceGroups] PRIMARY KEY ([ResourceId], [GroupId]),
        CONSTRAINT [FK_ResourceGroups_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ResourceGroups_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [access].[RoleGroups] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description24 AS sql_variant;
    SET @description24 = N'資源對功能群組授予的唯讀權限。';
    EXEC sp_addextendedproperty 'MS_Description', @description24, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceGroups';
    SET @description24 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description24, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceGroups', 'COLUMN', N'ResourceId';
    SET @description24 = N'關聯功能群組的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description24, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceGroups', 'COLUMN', N'GroupId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [collaboration].[ResourceMembers] (
        [ResourceId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(12) NOT NULL,
        CONSTRAINT [PK_ResourceMembers] PRIMARY KEY ([ResourceId], [UserId]),
        CONSTRAINT [FK_ResourceMembers_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ResourceMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description25 AS sql_variant;
    SET @description25 = N'資源對具名使用者授予的閱讀或編輯權限。';
    EXEC sp_addextendedproperty 'MS_Description', @description25, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceMembers';
    SET @description25 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description25, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceMembers', 'COLUMN', N'ResourceId';
    SET @description25 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description25, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceMembers', 'COLUMN', N'UserId';
    SET @description25 = N'訊息角色（user／assistant）或資源成員的閱讀／編輯權限。';
    EXEC sp_addextendedproperty 'MS_Description', @description25, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceMembers', 'COLUMN', N'Role';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [collaboration].[ShareLinks] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [SourceId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(24) NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        [IncludeAttachments] bit NOT NULL,
        [IsRevoked] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ShareLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShareLinks_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShareLinks_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description26 AS sql_variant;
    SET @description26 = N'分享版本快照、有效期限、附件選項及撤銷狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks';
    SET @description26 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'Id';
    SET @description26 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'OwnerId';
    SET @description26 = N'外部資料來源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'SourceId';
    SET @description26 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'Kind';
    SET @description26 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'Title';
    SET @description26 = N'分享時的固定內容快照；不隨後續編輯變動。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'SnapshotJson';
    SET @description26 = N'是否明確允許分享附件。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'IncludeAttachments';
    SET @description26 = N'分享是否已撤銷。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'IsRevoked';
    SET @description26 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'CreatedAt';
    SET @description26 = N'分享或授權到期時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'ExpiresAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [knowledge].[Documents] (
        [Id] uniqueidentifier NOT NULL,
        [CollectionId] uniqueidentifier NULL,
        [AttachmentId] uniqueidentifier NULL,
        [FileName] nvarchar(180) NOT NULL,
        [ContentType] nvarchar(80) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [PageCount] int NOT NULL,
        [ChunkCount] int NOT NULL,
        [EmbeddingProfile] nvarchar(200) NULL,
        [Warning] nvarchar(500) NULL,
        [IsDeleted] bit NOT NULL,
        [JobId] uniqueidentifier NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Documents_Attachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [attachments].[Attachments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Documents_Collections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [knowledge].[Collections] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Documents_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description27 AS sql_variant;
    SET @description27 = N'知識庫文件的原始附件、分析／索引狀態與 embedding profile。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents';
    SET @description27 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'Id';
    SET @description27 = N'關聯知識庫的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'CollectionId';
    SET @description27 = N'引用的附件識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'AttachmentId';
    SET @description27 = N'原始附件檔名，不作為伺服器儲存路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'FileName';
    SET @description27 = N'核准的 MIME 型別。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'ContentType';
    SET @description27 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'Status';
    SET @description27 = N'文件總頁數。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'PageCount';
    SET @description27 = N'文件已建立的檢索片段數。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'ChunkCount';
    SET @description27 = N'向量模型、維度與前處理的版本指紋；不混用不同 profile。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'EmbeddingProfile';
    SET @description27 = N'處理過程中的非致命提示。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'Warning';
    SET @description27 = N'是否邏輯刪除；不自動刪除歷史紀錄。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'IsDeleted';
    SET @description27 = N'關聯背景工作識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'JobId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [quality].[EvaluationRuns] (
        [Id] uniqueidentifier NOT NULL,
        [SetId] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [SetVersion] int NOT NULL,
        [SetTitle] nvarchar(120) NOT NULL,
        [CasesJson] nvarchar(max) NOT NULL,
        [VariantsJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EvaluationRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EvaluationRuns_BackgroundJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [operations].[BackgroundJobs] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EvaluationRuns_EvaluationSets_SetId] FOREIGN KEY ([SetId]) REFERENCES [quality].[EvaluationSets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EvaluationRuns_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description28 AS sql_variant;
    SET @description28 = N'評測執行的題庫與模型設定快照、背景工作與狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns';
    SET @description28 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'Id';
    SET @description28 = N'評測題庫識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'SetId';
    SET @description28 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'OwnerId';
    SET @description28 = N'關聯背景工作識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'JobId';
    SET @description28 = N'評測執行當時的題庫版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'SetVersion';
    SET @description28 = N'評測執行當時的題庫名稱快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'SetTitle';
    SET @description28 = N'固定評測案例的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'CasesJson';
    SET @description28 = N'評測模型／參數組合的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'VariantsJson';
    SET @description28 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [conversations].[Conversations] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [Title] nvarchar(120) NOT NULL,
        [ActiveLeafId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [IsDeleted] bit NOT NULL,
        [IsFavorite] bit NOT NULL,
        [IsArchived] bit NOT NULL,
        [SystemInstruction] nvarchar(4000) NOT NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversations_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Conversations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description29 AS sql_variant;
    SET @description29 = N'使用者私人對話、目前訊息分支、收藏封存與自訂指令。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations';
    SET @description29 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'Id';
    SET @description29 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'OwnerId';
    SET @description29 = N'關聯專案的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'ProjectId';
    SET @description29 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'Title';
    SET @description29 = N'對話目前顯示分支的最後訊息識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'ActiveLeafId';
    SET @description29 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'CreatedAt';
    SET @description29 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'UpdatedAt';
    SET @description29 = N'是否邏輯刪除；不自動刪除歷史紀錄。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'IsDeleted';
    SET @description29 = N'是否標記收藏。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'IsFavorite';
    SET @description29 = N'是否封存對話；封存後不再接受新的生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'IsArchived';
    SET @description29 = N'對話專用的回答指令。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'SystemInstruction';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [projects].[ProjectTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Title] nvarchar(80) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ProjectTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectTemplates_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description30 AS sql_variant;
    SET @description30 = N'專案建立範本與預設指令。';
    EXEC sp_addextendedproperty 'MS_Description', @description30, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates';
    SET @description30 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description30, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates', 'COLUMN', N'Id';
    SET @description30 = N'關聯專案的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description30, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates', 'COLUMN', N'ProjectId';
    SET @description30 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description30, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates', 'COLUMN', N'Title';
    SET @description30 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description30, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates', 'COLUMN', N'Content';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [collaboration].[ShareRecipients] (
        [ShareId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ShareRecipients] PRIMARY KEY ([ShareId], [UserId]),
        CONSTRAINT [FK_ShareRecipients_ShareLinks_ShareId] FOREIGN KEY ([ShareId]) REFERENCES [collaboration].[ShareLinks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ShareRecipients_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description31 AS sql_variant;
    SET @description31 = N'分享的具名收件人；與原資源 ACL 分開判定。';
    EXEC sp_addextendedproperty 'MS_Description', @description31, 'SCHEMA', N'collaboration', 'TABLE', N'ShareRecipients';
    SET @description31 = N'關聯分享的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description31, 'SCHEMA', N'collaboration', 'TABLE', N'ShareRecipients', 'COLUMN', N'ShareId';
    SET @description31 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description31, 'SCHEMA', N'collaboration', 'TABLE', N'ShareRecipients', 'COLUMN', N'UserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [knowledge].[Chunks] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [PageNumber] int NOT NULL,
        [Ordinal] int NOT NULL,
        [Text] nvarchar(2000) NOT NULL,
        [EmbeddingJson] nvarchar(max) NULL,
        [EmbeddingProfile] nvarchar(200) NULL,
        CONSTRAINT [PK_Chunks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Chunks_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [knowledge].[Documents] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description32 AS sql_variant;
    SET @description32 = N'知識檢索片段、頁碼、摘要與向量；查詢先套用資料 ACL。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks';
    SET @description32 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'Id';
    SET @description32 = N'關聯知識文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'DocumentId';
    SET @description32 = N'文件頁碼，從 1 開始。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'PageNumber';
    SET @description32 = N'同一父物件內的呈現順序。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'Ordinal';
    SET @description32 = N'文件頁面／片段的擷取文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'Text';
    SET @description32 = N'正規化向量的 JSON 表示；與 embedding profile 一起判斷相容性。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'EmbeddingJson';
    SET @description32 = N'向量模型、維度與前處理的版本指紋；不混用不同 profile。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'EmbeddingProfile';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [knowledge].[DocumentPages] (
        [DocumentId] uniqueidentifier NOT NULL,
        [PageNumber] int NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [Extraction] nvarchar(16) NOT NULL,
        [NeedsReview] bit NOT NULL,
        CONSTRAINT [PK_DocumentPages] PRIMARY KEY ([DocumentId], [PageNumber]),
        CONSTRAINT [FK_DocumentPages_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [knowledge].[Documents] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description33 AS sql_variant;
    SET @description33 = N'文件逐頁擷取的文字、頁碼與 OCR 結果。';
    EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages';
    SET @description33 = N'關聯知識文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'DocumentId';
    SET @description33 = N'文件頁碼，從 1 開始。';
    EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'PageNumber';
    SET @description33 = N'文件頁面／片段的擷取文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'Text';
    SET @description33 = N'附件文字擷取方法或結果。';
    EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'Extraction';
    SET @description33 = N'此評測結果是否需要人工覆核。';
    EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'NeedsReview';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [knowledge].[RepositoryImports] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [CollectionId] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [Repository] nvarchar(201) NOT NULL,
        [Path] nvarchar(500) NOT NULL,
        [Commit] nvarchar(64) NOT NULL,
        [BaseUrl] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_RepositoryImports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RepositoryImports_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [knowledge].[Documents] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description34 AS sql_variant;
    SET @description34 = N'程式庫文件匯入所固定的主機、repository、commit 與檔案路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports';
    SET @description34 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'Id';
    SET @description34 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'OwnerId';
    SET @description34 = N'關聯知識庫的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'CollectionId';
    SET @description34 = N'關聯知識文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'DocumentId';
    SET @description34 = N'Gitea repository 的 owner/name 識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'Repository';
    SET @description34 = N'repository 內的檔案路徑，不是伺服器路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'Path';
    SET @description34 = N'匯入當時固定的 commit SHA。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'Commit';
    SET @description34 = N'Gitea 連線主機位址。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'BaseUrl';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [quality].[EvaluationResults] (
        [RunId] uniqueidentifier NOT NULL,
        [CaseIndex] int NOT NULL,
        [VariantIndex] int NOT NULL,
        [Output] nvarchar(max) NOT NULL,
        [Truncated] bit NOT NULL,
        [RequiredMatches] int NOT NULL,
        [RequiredTotal] int NOT NULL,
        [ForbiddenMatches] int NOT NULL,
        [ElapsedMs] bigint NOT NULL,
        [InputTokens] bigint NULL,
        [OutputTokens] bigint NULL,
        [ReviewScore] int NULL,
        [ReviewNote] nvarchar(2000) NOT NULL,
        [ReviewerId] uniqueidentifier NULL,
        CONSTRAINT [PK_EvaluationResults] PRIMARY KEY ([RunId], [CaseIndex], [VariantIndex]),
        CONSTRAINT [FK_EvaluationResults_EvaluationRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [quality].[EvaluationRuns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_EvaluationResults_Users_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description35 AS sql_variant;
    SET @description35 = N'各案例／模型組合的輸出、指標、用量與人工覆核結果。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults';
    SET @description35 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'RunId';
    SET @description35 = N'評測案例的從零開始索引。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'CaseIndex';
    SET @description35 = N'評測模型／參數組合的從零開始索引。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'VariantIndex';
    SET @description35 = N'評測模型的實際回答。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'Output';
    SET @description35 = N'評測輸出是否因上限截斷。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'Truncated';
    SET @description35 = N'命中的必要條件數。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'RequiredMatches';
    SET @description35 = N'必要條件總數。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'RequiredTotal';
    SET @description35 = N'命中的禁止條件數。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ForbiddenMatches';
    SET @description35 = N'執行耗時，以毫秒計。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ElapsedMs';
    SET @description35 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'InputTokens';
    SET @description35 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'OutputTokens';
    SET @description35 = N'人工覆核分數；未覆核保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ReviewScore';
    SET @description35 = N'人工覆核意見。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ReviewNote';
    SET @description35 = N'人工覆核者的使用者識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ReviewerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [knowledge].[ConversationCollections] (
        [ConversationId] uniqueidentifier NOT NULL,
        [CollectionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ConversationCollections] PRIMARY KEY ([ConversationId], [CollectionId]),
        CONSTRAINT [FK_ConversationCollections_Collections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [knowledge].[Collections] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConversationCollections_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description36 AS sql_variant;
    SET @description36 = N'對話選定的知識庫來源關聯。';
    EXEC sp_addextendedproperty 'MS_Description', @description36, 'SCHEMA', N'knowledge', 'TABLE', N'ConversationCollections';
    SET @description36 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description36, 'SCHEMA', N'knowledge', 'TABLE', N'ConversationCollections', 'COLUMN', N'ConversationId';
    SET @description36 = N'關聯知識庫的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description36, 'SCHEMA', N'knowledge', 'TABLE', N'ConversationCollections', 'COLUMN', N'CollectionId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [conversations].[ConversationLabels] (
        [ConversationId] uniqueidentifier NOT NULL,
        [Name] nvarchar(24) NOT NULL,
        CONSTRAINT [PK_ConversationLabels] PRIMARY KEY ([ConversationId], [Name]),
        CONSTRAINT [FK_ConversationLabels_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description37 AS sql_variant;
    SET @description37 = N'使用者對話的分類標籤。';
    EXEC sp_addextendedproperty 'MS_Description', @description37, 'SCHEMA', N'conversations', 'TABLE', N'ConversationLabels';
    SET @description37 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description37, 'SCHEMA', N'conversations', 'TABLE', N'ConversationLabels', 'COLUMN', N'ConversationId';
    SET @description37 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description37, 'SCHEMA', N'conversations', 'TABLE', N'ConversationLabels', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [conversations].[Messages] (
        [Id] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Role] nvarchar(16) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [RunId] uniqueidentifier NULL,
        [ModelId] nvarchar(160) NULL,
        [ErrorCode] nvarchar(80) NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Messages_Messages_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description38 AS sql_variant;
    SET @description38 = N'對話訊息樹；提問、回答、重新生成與編輯保留各版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages';
    SET @description38 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'Id';
    SET @description38 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'ConversationId';
    SET @description38 = N'父訊息或父資源識別碼，用於分支或階層繼承。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'ParentId';
    SET @description38 = N'訊息角色（user／assistant）或資源成員的閱讀／編輯權限。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'Role';
    SET @description38 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'Content';
    SET @description38 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'Status';
    SET @description38 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'CreatedAt';
    SET @description38 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'RunId';
    SET @description38 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'ModelId';
    SET @description38 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'ErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [inference].[ModelCharges] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NULL,
        [Provider] nvarchar(32) NOT NULL,
        [ModelId] nvarchar(160) NOT NULL,
        [Operation] nvarchar(32) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [StartedAt] datetimeoffset NULL,
        [FinishedAt] datetimeoffset NULL,
        [PriceId] uniqueidentifier NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Kind] nvarchar(16) NOT NULL,
        [InputPerMillion] decimal(20,8) NOT NULL,
        [CachedInputPerMillion] decimal(20,8) NOT NULL,
        [OutputPerMillion] decimal(20,8) NOT NULL,
        [PerRequest] decimal(20,8) NOT NULL,
        [RequestCharge] nvarchar(16) NOT NULL,
        [InputTokens] bigint NULL,
        [CachedInputTokens] bigint NULL,
        [OutputTokens] bigint NULL,
        [ReasoningTokens] bigint NULL,
        [UsageComplete] bit NOT NULL,
        [Amount] decimal(20,8) NULL,
        [State] nvarchar(24) NOT NULL,
        [Outcome] nvarchar(16) NOT NULL,
        CONSTRAINT [PK_ModelCharges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ModelCharges_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ModelCharges_ModelPrices_PriceId] FOREIGN KEY ([PriceId]) REFERENCES [inference].[ModelPrices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ModelCharges_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description39 AS sql_variant;
    SET @description39 = N'各呼叫當時的價格與用量快照；未知費用保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges';
    SET @description39 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Id';
    SET @description39 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'OwnerId';
    SET @description39 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'ConversationId';
    SET @description39 = N'模型或搜尋服務供應商識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Provider';
    SET @description39 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'ModelId';
    SET @description39 = N'模型呼叫或背景工作的操作類型。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Operation';
    SET @description39 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'CreatedAt';
    SET @description39 = N'工作開始執行時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'StartedAt';
    SET @description39 = N'工作結束時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'FinishedAt';
    SET @description39 = N'此呼叫採用的不可變價格版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'PriceId';
    SET @description39 = N'費用幣別代碼；不同幣別不可直接合計。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Currency';
    SET @description39 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Kind';
    SET @description39 = N'每百萬輸入 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'InputPerMillion';
    SET @description39 = N'每百萬快取輸入 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'CachedInputPerMillion';
    SET @description39 = N'每百萬輸出 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'OutputPerMillion';
    SET @description39 = N'每次呼叫的固定單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'PerRequest';
    SET @description39 = N'搜尋服務每次請求的費用。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'RequestCharge';
    SET @description39 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'InputTokens';
    SET @description39 = N'模型回報的快取輸入 tokens。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'CachedInputTokens';
    SET @description39 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'OutputTokens';
    SET @description39 = N'模型回報的推理 tokens。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'ReasoningTokens';
    SET @description39 = N'本次呼叫是否有完整且可計費的實際用量。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'UsageComplete';
    SET @description39 = N'此呼叫的已知費用；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Amount';
    SET @description39 = N'業務物件的生命週期狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'State';
    SET @description39 = N'搜尋或處理操作的結果分類。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Outcome';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [content].[Artifacts] (
        [Id] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [SourceMessageId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        CONSTRAINT [PK_Artifacts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Artifacts_Messages_SourceMessageId] FOREIGN KEY ([SourceMessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Artifacts_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Artifacts_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description40 AS sql_variant;
    SET @description40 = N'使用者保存的成果文件與目前版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description40, 'SCHEMA', N'content', 'TABLE', N'Artifacts';
    SET @description40 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description40, 'SCHEMA', N'content', 'TABLE', N'Artifacts', 'COLUMN', N'Id';
    SET @description40 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description40, 'SCHEMA', N'content', 'TABLE', N'Artifacts', 'COLUMN', N'Version';
    SET @description40 = N'此成果版本所引用的來源訊息識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description40, 'SCHEMA', N'content', 'TABLE', N'Artifacts', 'COLUMN', N'SourceMessageId';
    SET @description40 = N'關聯專案的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description40, 'SCHEMA', N'content', 'TABLE', N'Artifacts', 'COLUMN', N'ProjectId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [inference].[GenerationRuns] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [ActiveOwnerId] uniqueidentifier NULL,
        [ExecutorId] uniqueidentifier NULL,
        [LeaseExpiresAt] datetimeoffset NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [UserMessageId] uniqueidentifier NOT NULL,
        [AssistantMessageId] uniqueidentifier NOT NULL,
        [ModelId] nvarchar(160) NOT NULL,
        [Provider] nvarchar(32) NOT NULL,
        [ProviderModelId] nvarchar(150) NOT NULL,
        [ParametersJson] nvarchar(max) NOT NULL,
        [IdempotencyKey] nvarchar(80) NOT NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [LastSequence] bigint NOT NULL,
        [ErrorCode] nvarchar(80) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [StartedAt] datetimeoffset NULL,
        [FinishedAt] datetimeoffset NULL,
        [InputTokens] bigint NULL,
        [OutputTokens] bigint NULL,
        [DurationMilliseconds] bigint NULL,
        [GenerationMilliseconds] bigint NULL,
        CONSTRAINT [PK_GenerationRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GenerationRuns_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GenerationRuns_Messages_AssistantMessageId] FOREIGN KEY ([AssistantMessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GenerationRuns_Messages_UserMessageId] FOREIGN KEY ([UserMessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GenerationRuns_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description41 AS sql_variant;
    SET @description41 = N'聊天生成的持久狀態、冪等請求、執行租約、回答及用量。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns';
    SET @description41 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'Id';
    SET @description41 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'OwnerId';
    SET @description41 = N'仍在執行的擁有者；filtered unique index 限制每人一個生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ActiveOwnerId';
    SET @description41 = N'處理此次生成的伺服器程序識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ExecutorId';
    SET @description41 = N'生成 executor 租約的到期時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'LeaseExpiresAt';
    SET @description41 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ConversationId';
    SET @description41 = N'此次生成的使用者提問訊息識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'UserMessageId';
    SET @description41 = N'此次生成的 AI 回答訊息識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'AssistantMessageId';
    SET @description41 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ModelId';
    SET @description41 = N'模型或搜尋服務供應商識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'Provider';
    SET @description41 = N'送往指定供應商的原生模型識別碼，與核准路由識別碼分開保存。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ProviderModelId';
    SET @description41 = N'執行參數的 JSON 快照，不含服務密鑰。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ParametersJson';
    SET @description41 = N'擁有者範圍內的冪等請求識別，避免重試重複處理。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'IdempotencyKey';
    SET @description41 = N'請求內容指紋，用於辨識冪等識別碼衝突。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'RequestHash';
    SET @description41 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'Status';
    SET @description41 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'Content';
    SET @description41 = N'最後已持久化的生成事件序號。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'LastSequence';
    SET @description41 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ErrorCode';
    SET @description41 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'CreatedAt';
    SET @description41 = N'工作開始執行時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'StartedAt';
    SET @description41 = N'工作結束時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'FinishedAt';
    SET @description41 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'InputTokens';
    SET @description41 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'OutputTokens';
    SET @description41 = N'從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'DurationMilliseconds';
    SET @description41 = N'從生成開始至終止的耗時毫秒；未開始的請求保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'GenerationMilliseconds';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [attachments].[MessageAttachments] (
        [MessageId] uniqueidentifier NOT NULL,
        [AttachmentId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_MessageAttachments] PRIMARY KEY ([MessageId], [AttachmentId]),
        CONSTRAINT [FK_MessageAttachments_Attachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [attachments].[Attachments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessageAttachments_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description42 AS sql_variant;
    SET @description42 = N'訊息與附件的關聯及呈現順序。';
    EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', N'attachments', 'TABLE', N'MessageAttachments';
    SET @description42 = N'關聯訊息的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', N'attachments', 'TABLE', N'MessageAttachments', 'COLUMN', N'MessageId';
    SET @description42 = N'引用的附件識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', N'attachments', 'TABLE', N'MessageAttachments', 'COLUMN', N'AttachmentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [knowledge].[MessageCitations] (
        [MessageId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [PageNumber] int NOT NULL,
        [Title] nvarchar(180) NOT NULL,
        [Excerpt] nvarchar(800) NOT NULL,
        CONSTRAINT [PK_MessageCitations] PRIMARY KEY ([MessageId], [Number]),
        CONSTRAINT [FK_MessageCitations_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [knowledge].[Documents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessageCitations_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description43 AS sql_variant;
    SET @description43 = N'回答生成當時的知識引用、文件頁碼與摘要快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations';
    SET @description43 = N'關聯訊息的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'MessageId';
    SET @description43 = N'回答引用的順序編號。';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'Number';
    SET @description43 = N'關聯知識文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'DocumentId';
    SET @description43 = N'文件頁碼，從 1 開始。';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'PageNumber';
    SET @description43 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'Title';
    SET @description43 = N'檢索或引用時保存的文字摘要。';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'Excerpt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [quality].[MessageFeedback] (
        [MessageId] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Rating] int NOT NULL,
        [Reason] nvarchar(24) NOT NULL,
        [Note] nvarchar(2000) NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MessageFeedback] PRIMARY KEY ([MessageId]),
        CONSTRAINT [FK_MessageFeedback_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessageFeedback_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description44 AS sql_variant;
    SET @description44 = N'使用者對 AI 回答的私人評分與意見。';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback';
    SET @description44 = N'關聯訊息的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'MessageId';
    SET @description44 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'OwnerId';
    SET @description44 = N'使用者對回答的評分。';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'Rating';
    SET @description44 = N'回饋或操作理由。';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'Reason';
    SET @description44 = N'使用者提供的補充說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'Note';
    SET @description44 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [content].[ArtifactRevisions] (
        [ArtifactId] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [AuthorId] uniqueidentifier NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ArtifactRevisions] PRIMARY KEY ([ArtifactId], [Version]),
        CONSTRAINT [FK_ArtifactRevisions_Artifacts_ArtifactId] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifacts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ArtifactRevisions_Users_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description45 AS sql_variant;
    SET @description45 = N'成果文件不可變版本、內容與作者。';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions';
    SET @description45 = N'關聯成果文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'ArtifactId';
    SET @description45 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'Version';
    SET @description45 = N'建立此版本的使用者識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'AuthorId';
    SET @description45 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'Title';
    SET @description45 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'Content';
    SET @description45 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [content].[SourceReferences] (
        [ArtifactId] uniqueidentifier NOT NULL,
        [SourceId] nvarchar(32) NOT NULL,
        [ExternalId] nvarchar(160) NOT NULL,
        [Revision] nvarchar(160) NOT NULL,
        [ImportedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SourceReferences] PRIMARY KEY ([ArtifactId]),
        CONSTRAINT [FK_SourceReferences_Artifacts_ArtifactId] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifacts] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description46 AS sql_variant;
    SET @description46 = N'外部來源匯入的識別、來源版本與匯入時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', N'content', 'TABLE', N'SourceReferences';
    SET @description46 = N'關聯成果文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'ArtifactId';
    SET @description46 = N'外部資料來源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'SourceId';
    SET @description46 = N'外部來源中的紀錄識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'ExternalId';
    SET @description46 = N'外部來源或 repository 的固定版本識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'Revision';
    SET @description46 = N'此來源版本明確匯入的時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'ImportedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE TABLE [inference].[RunEvents] (
        [RunId] uniqueidentifier NOT NULL,
        [Sequence] bigint NOT NULL,
        [Type] nvarchar(16) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Delta] nvarchar(max) NULL,
        [ErrorCode] nvarchar(80) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RunEvents] PRIMARY KEY ([RunId], [Sequence]),
        CONSTRAINT [FK_RunEvents_GenerationRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [inference].[GenerationRuns] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description47 AS sql_variant;
    SET @description47 = N'生成事件的有序 SSE 重播紀錄；完整生成狀態以 GenerationRuns 為準。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'inference', 'TABLE', N'RunEvents';
    SET @description47 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'RunId';
    SET @description47 = N'事件在同一生成中的遞增序號。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'Sequence';
    SET @description47 = N'事件的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'Type';
    SET @description47 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'Status';
    SET @description47 = N'生成文字增量或完整快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'Delta';
    SET @description47 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'ErrorCode';
    SET @description47 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''admin'', CAST(1 AS bit), N''管理'', N''/admin'', 90),
    (N''artifacts'', CAST(1 AS bit), N''成果文件'', N''/artifacts'', 40),
    (N''chat'', CAST(1 AS bit), N''AI 對話'', N''/chat'', 10),
    (N''dashboard'', CAST(1 AS bit), N''總覽'', N''/dashboard'', 5),
    (N''integrations'', CAST(1 AS bit), N''系統整合'', N''/integrations'', 80),
    (N''knowledge'', CAST(1 AS bit), N''知識庫'', N''/knowledge'', 30),
    (N''projects'', CAST(1 AS bit), N''專案'', N''/projects'', 20),
    (N''quality'', CAST(1 AS bit), N''品質評測'', N''/quality'', 60),
    (N''repositories'', CAST(1 AS bit), N''程式庫'', N''/repositories'', 65),
    (N''shared'', CAST(1 AS bit), N''分享'', N''/shared'', 50),
    (N''tasks'', CAST(1 AS bit), N''背景任務'', N''/tasks'', 70)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[RoleGroups]'))
        SET IDENTITY_INSERT [access].[RoleGroups] ON;
    EXEC(N'INSERT INTO [access].[RoleGroups] ([Id], [Enabled], [Name])
    VALUES (N''administrators'', CAST(1 AS bit), N''平台管理''),
    (N''workspace'', CAST(1 AS bit), N''基本工作區'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[RoleGroups]'))
        SET IDENTITY_INSERT [access].[RoleGroups] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[Roles]'))
        SET IDENTITY_INSERT [access].[Roles] ON;
    EXEC(N'INSERT INTO [access].[Roles] ([Id], [Enabled], [Name])
    VALUES (N''administrator'', CAST(1 AS bit), N''平台管理員''),
    (N''member'', CAST(1 AS bit), N''一般使用者'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[Roles]'))
        SET IDENTITY_INSERT [access].[Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''admin'', N''administrators''),
    (N''integrations'', N''administrators''),
    (N''artifacts'', N''workspace''),
    (N''chat'', N''workspace''),
    (N''dashboard'', N''workspace''),
    (N''knowledge'', N''workspace''),
    (N''projects'', N''workspace''),
    (N''quality'', N''workspace''),
    (N''repositories'', N''workspace''),
    (N''shared'', N''workspace''),
    (N''tasks'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'GroupId', N'RoleId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupRoles]'))
        SET IDENTITY_INSERT [access].[RoleGroupRoles] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupRoles] ([GroupId], [RoleId])
    VALUES (N''administrators'', N''administrator''),
    (N''workspace'', N''member'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'GroupId', N'RoleId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupRoles]'))
        SET IDENTITY_INSERT [access].[RoleGroupRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ArtifactRevisions_AuthorId] ON [content].[ArtifactRevisions] ([AuthorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Artifacts_ProjectId] ON [content].[Artifacts] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Artifacts_SourceMessageId] ON [content].[Artifacts] ([SourceMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Attachments_OwnerId_CreatedAt] ON [attachments].[Attachments] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Attachments_OwnerId_InLibrary_CreatedAt_Id] ON [attachments].[Attachments] ([OwnerId], [InLibrary], [CreatedAt], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Attachments_StorageKey] ON [attachments].[Attachments] ([StorageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Attachments_StorageState_CreatedAt] ON [attachments].[Attachments] ([StorageState], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_Action_Id] ON [operations].[AuditEvents] ([Action], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_ActorId_Id] ON [operations].[AuditEvents] ([ActorId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_At] ON [operations].[AuditEvents] ([At]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_ResourceId_Id] ON [operations].[AuditEvents] ([ResourceId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BackgroundJobs_ActiveKey] ON [operations].[BackgroundJobs] ([ActiveKey]) WHERE [ActiveKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BackgroundJobs_OwnerId_CreatedAt] ON [operations].[BackgroundJobs] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BackgroundJobs_ResourceId] ON [operations].[BackgroundJobs] ([ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BackgroundJobs_Status_LeaseUntil_CreatedAt] ON [operations].[BackgroundJobs] ([Status], [LeaseUntil], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Chunks_DocumentId_Ordinal] ON [knowledge].[Chunks] ([DocumentId], [Ordinal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ConversationCollections_CollectionId] ON [knowledge].[ConversationCollections] ([CollectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_OwnerId_IsDeleted_IsArchived_IsFavorite_UpdatedAt] ON [conversations].[Conversations] ([OwnerId], [IsDeleted], [IsArchived], [IsFavorite], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_OwnerId_IsDeleted_UpdatedAt] ON [conversations].[Conversations] ([OwnerId], [IsDeleted], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_ProjectId] ON [conversations].[Conversations] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Documents_AttachmentId] ON [knowledge].[Documents] ([AttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Documents_CollectionId_Status] ON [knowledge].[Documents] ([CollectionId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EvaluationResults_ReviewerId] ON [quality].[EvaluationResults] ([ReviewerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EvaluationRuns_JobId] ON [quality].[EvaluationRuns] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EvaluationRuns_OwnerId] ON [quality].[EvaluationRuns] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EvaluationRuns_SetId_CreatedAt] ON [quality].[EvaluationRuns] ([SetId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_GenerationRuns_ActiveOwnerId] ON [inference].[GenerationRuns] ([ActiveOwnerId]) WHERE [ActiveOwnerId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GenerationRuns_ActiveOwnerId_LeaseExpiresAt] ON [inference].[GenerationRuns] ([ActiveOwnerId], [LeaseExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GenerationRuns_AssistantMessageId] ON [inference].[GenerationRuns] ([AssistantMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GenerationRuns_ConversationId_CreatedAt] ON [inference].[GenerationRuns] ([ConversationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GenerationRuns_OwnerId_IdempotencyKey] ON [inference].[GenerationRuns] ([OwnerId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GenerationRuns_UserMessageId] ON [inference].[GenerationRuns] ([UserMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MessageAttachments_AttachmentId] ON [attachments].[MessageAttachments] ([AttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MessageCitations_DocumentId] ON [knowledge].[MessageCitations] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MessageFeedback_OwnerId_UpdatedAt] ON [quality].[MessageFeedback] ([OwnerId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_ConversationId_CreatedAt] ON [conversations].[Messages] ([ConversationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_ParentId] ON [conversations].[Messages] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ModelCharges_ConversationId] ON [inference].[ModelCharges] ([ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ModelCharges_CreatedAt] ON [inference].[ModelCharges] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ModelCharges_OwnerId_CreatedAt] ON [inference].[ModelCharges] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ModelCharges_PriceId] ON [inference].[ModelCharges] ([PriceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ModelInvocations_OwnerId_CreatedAt] ON [inference].[ModelInvocations] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ModelPrices_CreatedBy] ON [inference].[ModelPrices] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ModelPrices_Provider_ModelId_EffectiveAt] ON [inference].[ModelPrices] ([Provider], [ModelId], [EffectiveAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProjectTemplates_ProjectId] ON [projects].[ProjectTemplates] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PromptTemplates_OwnerId_UpdatedAt] ON [library].[PromptTemplates] ([OwnerId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RepositoryImports_DocumentId] ON [knowledge].[RepositoryImports] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RepositoryImports_OwnerId_CollectionId_Repository_Commit_Path] ON [knowledge].[RepositoryImports] ([OwnerId], [CollectionId], [Repository], [Commit], [Path]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ResourceAttachments_AttachmentId] ON [attachments].[ResourceAttachments] ([AttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ResourceGroups_GroupId] ON [collaboration].[ResourceGroups] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ResourceMembers_UserId] ON [collaboration].[ResourceMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Resources_OwnerId_Kind_UpdatedAt] ON [collaboration].[Resources] ([OwnerId], [Kind], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Resources_ParentId] ON [collaboration].[Resources] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RoleGroupFeatures_FeatureId] ON [access].[RoleGroupFeatures] ([FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RoleGroupRoles_GroupId] ON [access].[RoleGroupRoles] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareLinks_ExpiresAt] ON [collaboration].[ShareLinks] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareLinks_OwnerId_CreatedAt] ON [collaboration].[ShareLinks] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShareRecipients_UserId_ShareId] ON [collaboration].[ShareRecipients] ([UserId], [ShareId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SourceReferences_SourceId_ExternalId] ON [content].[SourceReferences] ([SourceId], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [access].[UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_AdAccount] ON [identity].[Users] ([AdAccount]) WHERE [AdAccount] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_LocalAccount] ON [identity].[Users] ([LocalAccount]) WHERE [LocalAccount] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Sid] ON [identity].[Users] ([Sid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WebSearches_OwnerId_CreatedAt] ON [inference].[WebSearches] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WebSearches_OwnerId_IdempotencyKey] ON [inference].[WebSearches] ([OwnerId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WebSearches_RunId] ON [inference].[WebSearches] ([RunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) >= 17 EXEC(N'ALTER TABLE [knowledge].[Chunks] ADD [EmbeddingVector] VECTOR(768) NULL, [EmbeddingVector1024] VECTOR(1024) NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    -- Re-runnable descriptions for schema, physical indexes/constraints and non-EF columns.
    -- Table/column MS_Description is maintained by the EF model and its migrations.
    SET NOCOUNT ON;
    DECLARE @nxDescschema sysname, @nxDesctable sysname, @nxDescname sysname, @nxDesckind varchar(20), @nxDescdescription nvarchar(3750);
    DECLARE schema_descriptions CURSOR LOCAL FAST_FORWARD FOR
    SELECT name, description FROM (VALUES
     (N'identity', N'使用者身分、登入政策、密碼雜湊與個人偏好。'),
     (N'access', N'角色、功能群組、功能授權及模型政策。'),
     (N'conversations', N'私人對話、訊息分支與分類。'),
     (N'inference', N'模型設定、生成、租約、重播、用量與費用。'),
     (N'operations', N'稽核、背景工作及持久進度。'),
     (N'attachments', N'站外原檔的 metadata、儲存識別、權限與引用關聯；不含原檔 bytes。'),
     (N'library', N'使用者私人提示詞範本。'),
     (N'collaboration', N'資料資源 ACL 與具名分享。'),
     (N'knowledge', N'知識文件、分頁、片段、向量與引用。'),
     (N'content', N'成果文件版本及外部來源參照。'),
     (N'projects', N'專案與共用指令範本。'),
     (N'quality', N'回答回饋、固定評測及人工覆核。'),
     (N'workspace', N'個人程式庫連線與匯入識別。'),
     (N'dbo', N'EF 資料庫版本記錄。')
    ) descriptions(name, description) WHERE SCHEMA_ID(name) IS NOT NULL;
    OPEN schema_descriptions;
    FETCH NEXT FROM schema_descriptions INTO @nxDescschema, @nxDescdescription;
    WHILE @@FETCH_STATUS = 0
    BEGIN
     IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 3 AND major_id = SCHEMA_ID(@nxDescschema) AND name = N'MS_Description')
      EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=@nxDescdescription, @level0type=N'SCHEMA', @level0name=@nxDescschema;
     ELSE EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=@nxDescdescription, @level0type=N'SCHEMA', @level0name=@nxDescschema;
     FETCH NEXT FROM schema_descriptions INTO @nxDescschema, @nxDescdescription;
    END;
    CLOSE schema_descriptions; DEALLOCATE schema_descriptions;

    DECLARE object_descriptions CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name, 'INDEX', i.name,
     LEFT(CONCAT(CASE WHEN i.is_unique = 1 THEN N'唯一索引；避免重複組合：' ELSE N'查詢索引；加速依下列欄位篩選及排序：' END,
      STRING_AGG(CONVERT(nvarchar(max), c.name + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N'' END), N'、') WITHIN GROUP (ORDER BY ic.key_ordinal),
      CASE WHEN i.has_filter = 1 THEN N'；篩選條件：' + i.filter_definition ELSE N'' END), 3750) COLLATE DATABASE_DEFAULT
    FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
    JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id AND ic.key_ordinal > 0
    JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
    WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace',N'dbo')
     AND i.is_primary_key=0 AND i.is_unique_constraint=0
    GROUP BY s.name,t.name,i.name,i.is_unique,i.has_filter,i.filter_definition
    UNION ALL
    SELECT s.name,t.name,'CONSTRAINT',k.name,CONCAT(CASE WHEN k.type='PK' THEN N'主鍵；唯一識別 ' ELSE N'唯一約束；防止重複 ' END,s.name,N'.',t.name,N' 的資料。') COLLATE DATABASE_DEFAULT
    FROM sys.key_constraints k JOIN sys.tables t ON k.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
    WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace',N'dbo')
    UNION ALL
    SELECT s.name,t.name,'CONSTRAINT',f.name,LEFT(CONCAT(N'外鍵；關聯 ',OBJECT_SCHEMA_NAME(f.referenced_object_id),N'.',OBJECT_NAME(f.referenced_object_id),N'；刪除策略：',f.delete_referential_action_desc,N'；更新策略：',f.update_referential_action_desc),3750) COLLATE DATABASE_DEFAULT
    FROM sys.foreign_keys f JOIN sys.tables t ON f.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
    WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
    UNION ALL
    SELECT s.name,t.name,'CONSTRAINT',d.name,LEFT(CONCAT(N'預設約束；欄位 ',COL_NAME(d.parent_object_id,d.parent_column_id),N' 未指定值時使用 ',d.definition),3750) COLLATE DATABASE_DEFAULT
    FROM sys.default_constraints d JOIN sys.tables t ON d.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
    WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
    UNION ALL
    SELECT s.name,t.name,'CONSTRAINT',c.name,LEFT(CONCAT(N'檢核約束；資料需符合 ',c.definition),3750) COLLATE DATABASE_DEFAULT
    FROM sys.check_constraints c JOIN sys.tables t ON c.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
    WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
    UNION ALL
    SELECT s.name,t.name,'COLUMN',c.name,
     CASE WHEN c.name LIKE N'%1024%' THEN N'原生 VECTOR(1024) embedding；SQL Server 2025 支援時建立，須先套用來源 ACL 與相容 profile。'
     ELSE N'原生 VECTOR(768) embedding；SQL Server 2025 支援時建立，須先套用來源 ACL 與相容 profile。' END COLLATE DATABASE_DEFAULT
    FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
    WHERE s.name=N'knowledge' AND t.name=N'Chunks' AND c.name LIKE N'EmbeddingVector%'
    UNION ALL
    SELECT N'dbo',N'__EFMigrationsHistory',NULL,NULL,N'EF 已套用版本記錄；不可手動刪除以重跑 migration。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
    UNION ALL
    SELECT N'dbo',N'__EFMigrationsHistory','COLUMN',N'MigrationId',N'已套用的 migration 版本識別碼。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
    UNION ALL
    SELECT N'dbo',N'__EFMigrationsHistory','COLUMN',N'ProductVersion',N'套用 migration 的 Entity Framework Core 版本。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL;
    OPEN object_descriptions;
    FETCH NEXT FROM object_descriptions INTO @nxDescschema,@nxDesctable,@nxDesckind,@nxDescname,@nxDescdescription;
    WHILE @@FETCH_STATUS = 0
    BEGIN
     IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description',N'SCHEMA',@nxDescschema,N'TABLE',@nxDesctable,@nxDesckind,@nxDescname))
      EXEC sys.sp_updateextendedproperty @name=N'MS_Description',@value=@nxDescdescription,@level0type=N'SCHEMA',@level0name=@nxDescschema,@level1type=N'TABLE',@level1name=@nxDesctable,@level2type=@nxDesckind,@level2name=@nxDescname;
     ELSE EXEC sys.sp_addextendedproperty @name=N'MS_Description',@value=@nxDescdescription,@level0type=N'SCHEMA',@level0name=@nxDescschema,@level1type=N'TABLE',@level1name=@nxDesctable,@level2type=@nxDesckind,@level2name=@nxDescname;
     FETCH NEXT FROM object_descriptions INTO @nxDescschema,@nxDesctable,@nxDesckind,@nxDescname,@nxDescdescription;
    END;
    CLOSE object_descriptions; DEALLOCATE object_descriptions;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005171400_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005171400_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    DECLARE @var48 nvarchar(max);
    SELECT @var48 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[access].[GroupModelPolicies]') AND [c].[name] = N'DailyRequestLimit');
    IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [access].[GroupModelPolicies] DROP CONSTRAINT ' + @var48 + ';');
    ALTER TABLE [access].[GroupModelPolicies] DROP COLUMN [DailyRequestLimit];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    DECLARE @description49 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies';
    SET @description49 = N'功能群組的模型白名單、各模型每日 token 及附件空間限制。';
    EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    ALTER TABLE [inference].[ModelInvocations] ADD [ReservedTokens] bigint NOT NULL DEFAULT CAST(0 AS bigint);
    DECLARE @description50 AS sql_variant;
    SET @description50 = N'生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'ReservedTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    ALTER TABLE [access].[GroupModelPolicies] ADD [DailyTokenLimitsJson] nvarchar(max) NULL;
    DECLARE @description51 AS sql_variant;
    SET @description51 = N'各模型每日輸入加輸出 token 上限 JSON；個人覆寫優先，群組取最低值，UTC 午夜重設。';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'DailyTokenLimitsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    ALTER TABLE [inference].[GenerationRuns] ADD [ReservedTokens] bigint NOT NULL DEFAULT CAST(0 AS bigint);
    DECLARE @description52 AS sql_variant;
    SET @description52 = N'生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ReservedTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    CREATE TABLE [access].[UserModelPolicies] (
        [UserId] uniqueidentifier NOT NULL,
        [AllowedModelsJson] nvarchar(4000) NULL,
        [DailyTokenLimitsJson] nvarchar(max) NULL,
        CONSTRAINT [PK_UserModelPolicies] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_UserModelPolicies_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description53 AS sql_variant;
    SET @description53 = N'使用者的模型白名單與各模型每日 token 覆寫政策。';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', N'access', 'TABLE', N'UserModelPolicies';
    SET @description53 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', N'access', 'TABLE', N'UserModelPolicies', 'COLUMN', N'UserId';
    SET @description53 = N'模型白名單 JSON；空值不增加限制，空陣列禁止生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', N'access', 'TABLE', N'UserModelPolicies', 'COLUMN', N'AllowedModelsJson';
    SET @description53 = N'各模型每日輸入加輸出 token 上限 JSON；個人覆寫優先，群組取最低值，UTC 午夜重設。';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', N'access', 'TABLE', N'UserModelPolicies', 'COLUMN', N'DailyTokenLimitsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'成果' WHERE [Id] = N'artifacts' AND [Name] = N'成果文件';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'對話' WHERE [Id] = N'chat' AND [Name] = N'AI 對話';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'來源' WHERE [Id] = N'integrations' AND [Name] = N'系統整合';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'知識' WHERE [Id] = N'knowledge' AND [Name] = N'知識庫';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'評測' WHERE [Id] = N'quality' AND [Name] = N'品質評測';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'任務' WHERE [Id] = N'tasks' AND [Name] = N'背景任務';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''files'', CAST(1 AS bit), N''檔案庫'', N''/files'', 15)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''files'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    SELECT DISTINCT N'files', grants.[GroupId] FROM [access].[RoleGroupFeatures] grants
    WHERE grants.[FeatureId] IN (N'chat', N'knowledge', N'projects')
      AND NOT EXISTS (SELECT 1 FROM [access].[RoleGroupFeatures] existing WHERE existing.[GroupId] = grants.[GroupId] AND existing.[FeatureId] = N'files');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006005809_PerModelTokenBudgets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006005809_PerModelTokenBudgets', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006025742_AdditiveModelGrants'
)
BEGIN
    DECLARE @description54 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', N'access', 'TABLE', N'UserModelPolicies', 'COLUMN', N'DailyTokenLimitsJson';
    SET @description54 = N'各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', N'access', 'TABLE', N'UserModelPolicies', 'COLUMN', N'DailyTokenLimitsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006025742_AdditiveModelGrants'
)
BEGIN
    DECLARE @description55 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', N'access', 'TABLE', N'UserModelPolicies', 'COLUMN', N'AllowedModelsJson';
    SET @description55 = N'模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', N'access', 'TABLE', N'UserModelPolicies', 'COLUMN', N'AllowedModelsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006025742_AdditiveModelGrants'
)
BEGIN
    DECLARE @description56 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'DailyTokenLimitsJson';
    SET @description56 = N'各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。';
    EXEC sp_addextendedproperty 'MS_Description', @description56, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'DailyTokenLimitsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006025742_AdditiveModelGrants'
)
BEGIN
    DECLARE @description57 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'AllowedModelsJson';
    SET @description57 = N'模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。';
    EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'AllowedModelsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006025742_AdditiveModelGrants'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006025742_AdditiveModelGrants', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006035611_UnifiedWorkspaceTerminology'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'平台管理' WHERE [Id] = N'admin' AND [Name] = N'管理';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006035611_UnifiedWorkspaceTerminology'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'成果文件' WHERE [Id] = N'artifacts' AND [Name] = N'成果';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006035611_UnifiedWorkspaceTerminology'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'資料來源' WHERE [Id] = N'integrations' AND [Name] = N'來源';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006035611_UnifiedWorkspaceTerminology'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'知識庫' WHERE [Id] = N'knowledge' AND [Name] = N'知識';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006035611_UnifiedWorkspaceTerminology'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'品質評測' WHERE [Id] = N'quality' AND [Name] = N'評測';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006035611_UnifiedWorkspaceTerminology'
)
BEGIN
    UPDATE [access].[Features] SET [Name] = N'背景任務' WHERE [Id] = N'tasks' AND [Name] = N'任務';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006035611_UnifiedWorkspaceTerminology'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006035611_UnifiedWorkspaceTerminology', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    ALTER TABLE [knowledge].[Documents] ADD [TextContent] nvarchar(max) NULL;
    DECLARE @description58 AS sql_variant;
    SET @description58 = N'純文字來源的可編輯內容；一般上傳原檔保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description58, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'TextContent';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    ALTER TABLE [knowledge].[Documents] ADD [TextVersion] int NOT NULL DEFAULT 0;
    DECLARE @description59 AS sql_variant;
    SET @description59 = N'純文字內容的樂觀並行版本號。';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'TextVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    CREATE TABLE [operations].[Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [EventKey] nvarchar(160) NOT NULL,
        [Type] nvarchar(80) NOT NULL,
        [Severity] nvarchar(16) NOT NULL,
        [Title] nvarchar(180) NOT NULL,
        [Body] nvarchar(600) NOT NULL,
        [TargetKind] nvarchar(32) NOT NULL,
        [TargetId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ReadAt] datetimeoffset NULL,
        [DismissedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description60 AS sql_variant;
    SET @description60 = N'使用者個人通知、版本化導向、閱讀狀態與事件去重鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications';
    SET @description60 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'Id';
    SET @description60 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'OwnerId';
    SET @description60 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'Version';
    SET @description60 = N'通知來源事件的冪等識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'EventKey';
    SET @description60 = N'事件的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'Type';
    SET @description60 = N'通知呈現層級：info、success 或 error。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'Severity';
    SET @description60 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'Title';
    SET @description60 = N'通知摘要，不包含完整私密原文。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'Body';
    SET @description60 = N'已核准的功能導向類型。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'TargetKind';
    SET @description60 = N'通知所指向的業務識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'TargetId';
    SET @description60 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'CreatedAt';
    SET @description60 = N'通知已閱讀的時間；空值代表未讀。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'ReadAt';
    SET @description60 = N'通知移除的時間；空值代表仍可查看。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'DismissedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    CREATE TABLE [workspace].[RepositoryReviews] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [BaseUrl] nvarchar(500) NOT NULL,
        [Repository] nvarchar(201) NOT NULL,
        [Commit] nvarchar(64) NOT NULL,
        [BaseCommit] nvarchar(64) NULL,
        [ModelId] nvarchar(160) NOT NULL,
        [ConfigurationFingerprint] nvarchar(64) NOT NULL,
        [Note] nvarchar(2000) NOT NULL,
        [IdempotencyKey] nvarchar(80) NOT NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RepositoryReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RepositoryReviews_BackgroundJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [operations].[BackgroundJobs] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RepositoryReviews_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description61 AS sql_variant;
    SET @description61 = N'固定 commit 或區間 diff 的私人 review、模型設定指紋與背景任務。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews';
    SET @description61 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'Id';
    SET @description61 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'OwnerId';
    SET @description61 = N'關聯背景工作識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'JobId';
    SET @description61 = N'Gitea 連線主機位址。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'BaseUrl';
    SET @description61 = N'Gitea repository 的 owner/name 識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'Repository';
    SET @description61 = N'匯入當時固定的 commit SHA。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'Commit';
    SET @description61 = N'區間 review 的起點 commit SHA；空值表示單一 commit。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'BaseCommit';
    SET @description61 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'ModelId';
    SET @description61 = N'固定模型與生成設定的 SHA-256 指紋。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'ConfigurationFingerprint';
    SET @description61 = N'使用者提供的補充說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'Note';
    SET @description61 = N'擁有者範圍內的冪等請求識別，避免重試重複處理。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'IdempotencyKey';
    SET @description61 = N'請求內容指紋，用於辨識冪等識別碼衝突。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'RequestHash';
    SET @description61 = N'分享時的固定內容快照；不隨後續編輯變動。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'SnapshotJson';
    SET @description61 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviews', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    CREATE TABLE [workspace].[RepositoryReviewResults] (
        [ReviewId] uniqueidentifier NOT NULL,
        [Ordinal] int NOT NULL,
        [Output] nvarchar(max) NOT NULL,
        [Truncated] bit NOT NULL,
        [InputTokens] bigint NULL,
        [OutputTokens] bigint NULL,
        [ElapsedMs] bigint NOT NULL,
        CONSTRAINT [PK_RepositoryReviewResults] PRIMARY KEY ([ReviewId], [Ordinal]),
        CONSTRAINT [FK_RepositoryReviewResults_RepositoryReviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [workspace].[RepositoryReviews] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description62 AS sql_variant;
    SET @description62 = N'Review 各區段的持久結果及用量；重試沿用已完成區段。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviewResults';
    SET @description62 = N'關聯私人程式碼 review 的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviewResults', 'COLUMN', N'ReviewId';
    SET @description62 = N'同一父物件內的呈現順序。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviewResults', 'COLUMN', N'Ordinal';
    SET @description62 = N'評測模型的實際回答。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviewResults', 'COLUMN', N'Output';
    SET @description62 = N'評測輸出是否因上限截斷。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviewResults', 'COLUMN', N'Truncated';
    SET @description62 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviewResults', 'COLUMN', N'InputTokens';
    SET @description62 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviewResults', 'COLUMN', N'OutputTokens';
    SET @description62 = N'執行耗時，以毫秒計。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryReviewResults', 'COLUMN', N'ElapsedMs';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    CREATE INDEX [IX_Notifications_OwnerId_DismissedAt_ReadAt_CreatedAt] ON [operations].[Notifications] ([OwnerId], [DismissedAt], [ReadAt], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Notifications_OwnerId_EventKey] ON [operations].[Notifications] ([OwnerId], [EventKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    CREATE INDEX [IX_RepositoryReviews_JobId] ON [workspace].[RepositoryReviews] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    CREATE INDEX [IX_RepositoryReviews_OwnerId_CreatedAt] ON [workspace].[RepositoryReviews] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RepositoryReviews_OwnerId_IdempotencyKey] ON [workspace].[RepositoryReviews] ([OwnerId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006061603_WorkspaceExperience'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006061603_WorkspaceExperience', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006105804_ReadingDefaults'
)
BEGIN
    DECLARE @var63 nvarchar(max);
    SELECT @var63 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[identity].[UserPreferences]') AND [c].[name] = N'SidebarWidth');
    IF @var63 IS NOT NULL EXEC(N'ALTER TABLE [identity].[UserPreferences] DROP CONSTRAINT ' + @var63 + ';');
    ALTER TABLE [identity].[UserPreferences] ADD DEFAULT 240 FOR [SidebarWidth];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006105804_ReadingDefaults'
)
BEGIN
    DECLARE @var64 nvarchar(max);
    SELECT @var64 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[identity].[UserPreferences]') AND [c].[name] = N'ReadingLineHeight');
    IF @var64 IS NOT NULL EXEC(N'ALTER TABLE [identity].[UserPreferences] DROP CONSTRAINT ' + @var64 + ';');
    ALTER TABLE [identity].[UserPreferences] ADD DEFAULT 1.2E0 FOR [ReadingLineHeight];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006105804_ReadingDefaults'
)
BEGIN
    DECLARE @var65 nvarchar(max);
    SELECT @var65 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[identity].[UserPreferences]') AND [c].[name] = N'ReadingFontSize');
    IF @var65 IS NOT NULL EXEC(N'ALTER TABLE [identity].[UserPreferences] DROP CONSTRAINT ' + @var65 + ';');
    ALTER TABLE [identity].[UserPreferences] ADD DEFAULT 15 FOR [ReadingFontSize];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006105804_ReadingDefaults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006105804_ReadingDefaults', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    DELETE FROM [knowledge].[Chunks];
    UPDATE [knowledge].[Documents] SET [Status] = 'reindex', [ChunkCount] = 0 WHERE [CollectionId] IS NOT NULL AND [IsDeleted] = 0;
    IF COL_LENGTH('knowledge.Chunks', 'EmbeddingVector') IS NOT NULL ALTER TABLE [knowledge].[Chunks] DROP COLUMN [EmbeddingVector];
    IF COL_LENGTH('knowledge.Chunks', 'EmbeddingVector1024') IS NOT NULL ALTER TABLE [knowledge].[Chunks] DROP COLUMN [EmbeddingVector1024];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    DROP INDEX [IX_Documents_CollectionId_Status] ON [knowledge].[Documents];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    DECLARE @var66 nvarchar(max);
    SELECT @var66 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[knowledge].[Documents]') AND [c].[name] = N'EmbeddingProfile');
    IF @var66 IS NOT NULL EXEC(N'ALTER TABLE [knowledge].[Documents] DROP CONSTRAINT ' + @var66 + ';');
    ALTER TABLE [knowledge].[Documents] DROP COLUMN [EmbeddingProfile];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    DECLARE @var67 nvarchar(max);
    SELECT @var67 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[knowledge].[Chunks]') AND [c].[name] = N'EmbeddingJson');
    IF @var67 IS NOT NULL EXEC(N'ALTER TABLE [knowledge].[Chunks] DROP CONSTRAINT ' + @var67 + ';');
    ALTER TABLE [knowledge].[Chunks] DROP COLUMN [EmbeddingJson];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    DECLARE @var68 nvarchar(max);
    SELECT @var68 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[knowledge].[Chunks]') AND [c].[name] = N'EmbeddingProfile');
    IF @var68 IS NOT NULL EXEC(N'ALTER TABLE [knowledge].[Chunks] DROP CONSTRAINT ' + @var68 + ';');
    ALTER TABLE [knowledge].[Chunks] DROP COLUMN [EmbeddingProfile];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    DECLARE @var69 nvarchar(max);
    SELECT @var69 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[knowledge].[Chunks]') AND [c].[name] = N'PageNumber');
    IF @var69 IS NOT NULL EXEC(N'ALTER TABLE [knowledge].[Chunks] DROP CONSTRAINT ' + @var69 + ';');
    ALTER TABLE [knowledge].[Chunks] DROP COLUMN [PageNumber];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    DECLARE @description70 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', N'knowledge', 'TABLE', N'Chunks';
    SET @description70 = N'結構化檢索片段與 profile 專屬切段版本；查詢先套用資料 ACL。';
    EXEC sp_addextendedproperty 'MS_Description', @description70, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    DECLARE @var71 nvarchar(max);
    SELECT @var71 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[knowledge].[Chunks]') AND [c].[name] = N'Text');
    IF @var71 IS NOT NULL EXEC(N'ALTER TABLE [knowledge].[Chunks] DROP CONSTRAINT ' + @var71 + ';');
    ALTER TABLE [knowledge].[Chunks] ALTER COLUMN [Text] nvarchar(4000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] ADD [ContentHash] binary(32) NOT NULL DEFAULT 0x;
    DECLARE @description72 AS sql_variant;
    SET @description72 = N'實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。';
    EXEC sp_addextendedproperty 'MS_Description', @description72, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'ContentHash';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] ADD [EndPage] int NOT NULL DEFAULT 0;
    DECLARE @description73 AS sql_variant;
    SET @description73 = N'片段結束的原始文件頁碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description73, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'EndPage';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] ADD [HeadingPath] nvarchar(400) NOT NULL DEFAULT N'';
    DECLARE @description74 AS sql_variant;
    SET @description74 = N'由標題階層組成的結構路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description74, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'HeadingPath';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] ADD [ProfileId] int NOT NULL DEFAULT 0;
    DECLARE @description75 AS sql_variant;
    SET @description75 = N'向量空間及切段版本的 EmbeddingProfiles 外鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description75, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'ProfileId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] ADD [SearchId] int NOT NULL IDENTITY;
    DECLARE @description76 AS sql_variant;
    SET @description76 = N'全文索引使用的整數唯一鍵；保留未來 ANN 映射。';
    EXEC sp_addextendedproperty 'MS_Description', @description76, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'SearchId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] ADD [StartPage] int NOT NULL DEFAULT 0;
    DECLARE @description77 AS sql_variant;
    SET @description77 = N'片段開始的原始文件頁碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description77, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'StartPage';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] ADD [TokenEstimate] int NOT NULL DEFAULT 0;
    DECLARE @description78 AS sql_variant;
    SET @description78 = N'依 CJK 與其他字元比例估算的片段 token 數。';
    EXEC sp_addextendedproperty 'MS_Description', @description78, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'TokenEstimate';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE TABLE [knowledge].[EmbeddingProfiles] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(200) NOT NULL,
        [Provider] nvarchar(32) NOT NULL,
        [Model] nvarchar(160) NOT NULL,
        [Dimensions] int NOT NULL,
        [InputFormat] nvarchar(32) NOT NULL,
        [QueryInstruction] nvarchar(500) NOT NULL,
        [Revision] nvarchar(64) NOT NULL,
        [ChunkerConfiguration] nvarchar(500) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ActivatedAt] datetimeoffset NULL,
        [RetiredAt] datetimeoffset NULL,
        CONSTRAINT [PK_EmbeddingProfiles] PRIMARY KEY ([Id])
    );
    DECLARE @description79 AS sql_variant;
    SET @description79 = N'向量空間及切段規則的不可變快照；同時最多一個 active。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles';
    SET @description79 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'Id';
    SET @description79 = N'供應商、模型、維度及輸入／切段規則的唯一指紋。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'Key';
    SET @description79 = N'模型或搜尋服務供應商識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'Provider';
    SET @description79 = N'建立此向量空間時的模型識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'Model';
    SET @description79 = N'向量維度，限已建立資料表的 allowlist。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'Dimensions';
    SET @description79 = N'查詢輸入格式 plain 或 qwen-query。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'InputFormat';
    SET @description79 = N'qwen-query 的檢索任務指令快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'QueryInstruction';
    SET @description79 = N'外部來源或 repository 的固定版本識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'Revision';
    SET @description79 = N'切段器版本及 token／重疊參數快照；重建期間保留舊版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'ChunkerConfiguration';
    SET @description79 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'Status';
    SET @description79 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'CreatedAt';
    SET @description79 = N'此 profile 完整覆蓋並切換為 active 的 UTC 時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'ActivatedAt';
    SET @description79 = N'此 profile 退役的 UTC 時間；作為保留期清理依據。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'knowledge', 'TABLE', N'EmbeddingProfiles', 'COLUMN', N'RetiredAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE TABLE [knowledge].[ChunkEmbeddings1024] (
        [Id] int NOT NULL IDENTITY,
        [ChunkId] uniqueidentifier NOT NULL,
        [ProfileId] int NOT NULL,
        [ContentHash] binary(32) NOT NULL,
        [Vector] vector(1024) NOT NULL,
        CONSTRAINT [PK_ChunkEmbeddings1024] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ChunkEmbeddings1024_Chunks_ChunkId] FOREIGN KEY ([ChunkId]) REFERENCES [knowledge].[Chunks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChunkEmbeddings1024_EmbeddingProfiles_ProfileId] FOREIGN KEY ([ProfileId]) REFERENCES [knowledge].[EmbeddingProfiles] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description80 AS sql_variant;
    SET @description80 = N'1024 維原生向量、片段關聯與 profile 內容快取。';
    EXEC sp_addextendedproperty 'MS_Description', @description80, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings1024';
    SET @description80 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description80, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings1024', 'COLUMN', N'Id';
    SET @description80 = N'向量對應的結構化片段 Guid 識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description80, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings1024', 'COLUMN', N'ChunkId';
    SET @description80 = N'向量空間及切段版本的 EmbeddingProfiles 外鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description80, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings1024', 'COLUMN', N'ProfileId';
    SET @description80 = N'實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。';
    EXEC sp_addextendedproperty 'MS_Description', @description80, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings1024', 'COLUMN', N'ContentHash';
    SET @description80 = N'L2 正規化的 float32 向量；SQL Server 使用 VECTOR 型別。';
    EXEC sp_addextendedproperty 'MS_Description', @description80, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings1024', 'COLUMN', N'Vector';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE TABLE [knowledge].[ChunkEmbeddings768] (
        [Id] int NOT NULL IDENTITY,
        [ChunkId] uniqueidentifier NOT NULL,
        [ProfileId] int NOT NULL,
        [ContentHash] binary(32) NOT NULL,
        [Vector] vector(768) NOT NULL,
        CONSTRAINT [PK_ChunkEmbeddings768] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ChunkEmbeddings768_Chunks_ChunkId] FOREIGN KEY ([ChunkId]) REFERENCES [knowledge].[Chunks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChunkEmbeddings768_EmbeddingProfiles_ProfileId] FOREIGN KEY ([ProfileId]) REFERENCES [knowledge].[EmbeddingProfiles] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description81 AS sql_variant;
    SET @description81 = N'768 維原生向量、片段關聯與 profile 內容快取。';
    EXEC sp_addextendedproperty 'MS_Description', @description81, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings768';
    SET @description81 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description81, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings768', 'COLUMN', N'Id';
    SET @description81 = N'向量對應的結構化片段 Guid 識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description81, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings768', 'COLUMN', N'ChunkId';
    SET @description81 = N'向量空間及切段版本的 EmbeddingProfiles 外鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description81, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings768', 'COLUMN', N'ProfileId';
    SET @description81 = N'實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。';
    EXEC sp_addextendedproperty 'MS_Description', @description81, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings768', 'COLUMN', N'ContentHash';
    SET @description81 = N'L2 正規化的 float32 向量；SQL Server 使用 VECTOR 型別。';
    EXEC sp_addextendedproperty 'MS_Description', @description81, 'SCHEMA', N'knowledge', 'TABLE', N'ChunkEmbeddings768', 'COLUMN', N'Vector';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE INDEX [IX_Documents_CollectionId_Status_IsDeleted] ON [knowledge].[Documents] ([CollectionId], [Status], [IsDeleted]) INCLUDE ([Id], [FileName], [ChunkCount]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE INDEX [IX_Chunks_ProfileId_DocumentId] ON [knowledge].[Chunks] ([ProfileId], [DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Chunks_SearchId] ON [knowledge].[Chunks] ([SearchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE INDEX [IX_ChunkEmbeddings1024_ChunkId] ON [knowledge].[ChunkEmbeddings1024] ([ChunkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChunkEmbeddings1024_ProfileId_ChunkId] ON [knowledge].[ChunkEmbeddings1024] ([ProfileId], [ChunkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE INDEX [IX_ChunkEmbeddings1024_ProfileId_ContentHash] ON [knowledge].[ChunkEmbeddings1024] ([ProfileId], [ContentHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE INDEX [IX_ChunkEmbeddings768_ChunkId] ON [knowledge].[ChunkEmbeddings768] ([ChunkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChunkEmbeddings768_ProfileId_ChunkId] ON [knowledge].[ChunkEmbeddings768] ([ProfileId], [ChunkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE INDEX [IX_ChunkEmbeddings768_ProfileId_ContentHash] ON [knowledge].[ChunkEmbeddings768] ([ProfileId], [ContentHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmbeddingProfiles_Key] ON [knowledge].[EmbeddingProfiles] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EmbeddingProfiles_Status] ON [knowledge].[EmbeddingProfiles] ([Status]) WHERE [Status] = ''active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] ADD CONSTRAINT [FK_Chunks_EmbeddingProfiles_ProfileId] FOREIGN KEY ([ProfileId]) REFERENCES [knowledge].[EmbeddingProfiles] ([Id]) ON DELETE NO ACTION;
END;

COMMIT;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    IF CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')) = 1
        AND EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028)
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'KnowledgeSearch')
            EXEC('CREATE FULLTEXT CATALOG [KnowledgeSearch]');
        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks'))
            EXEC('CREATE FULLTEXT INDEX ON [knowledge].[Chunks] ([Text] LANGUAGE 1028, [HeadingPath] LANGUAGE 1028) KEY INDEX [IX_Chunks_SearchId] ON [KnowledgeSearch] WITH CHANGE_TRACKING AUTO');
    END
    ELSE RAISERROR(N'知識全文索引未建立：未安裝全文元件或繁體中文 1028 斷詞器；執行期會明確回報 vector 模式。', 10, 1) WITH NOWAIT;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006144804_VectorRetrievalProfiles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006144804_VectorRetrievalProfiles', N'10.0.12');
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006145833_SingleChunkLayout'
)
BEGIN
    ALTER TABLE [knowledge].[Chunks] DROP CONSTRAINT [FK_Chunks_EmbeddingProfiles_ProfileId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006145833_SingleChunkLayout'
)
BEGIN
    DROP INDEX [IX_Chunks_ProfileId_DocumentId] ON [knowledge].[Chunks];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006145833_SingleChunkLayout'
)
BEGIN
    DECLARE @var82 nvarchar(max);
    SELECT @var82 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[knowledge].[Chunks]') AND [c].[name] = N'ProfileId');
    IF @var82 IS NOT NULL EXEC(N'ALTER TABLE [knowledge].[Chunks] DROP CONSTRAINT ' + @var82 + ';');
    ALTER TABLE [knowledge].[Chunks] DROP COLUMN [ProfileId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006145833_SingleChunkLayout'
)
BEGIN
    DECLARE @description83 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', N'knowledge', 'TABLE', N'Chunks';
    SET @description83 = N'結構化檢索片段、頁碼與內容指紋；查詢先套用資料 ACL。';
    EXEC sp_addextendedproperty 'MS_Description', @description83, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006145833_SingleChunkLayout'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006145833_SingleChunkLayout', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006150830_CitationPageRanges'
)
BEGIN
    ALTER TABLE [knowledge].[MessageCitations] ADD [EndPage] int NOT NULL DEFAULT 0;
    DECLARE @description84 AS sql_variant;
    SET @description84 = N'片段結束的原始文件頁碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description84, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'EndPage';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006150830_CitationPageRanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006150830_CitationPageRanges', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006155137_RetrievalEvaluationReports'
)
BEGIN
    CREATE TABLE [quality].[RetrievalEvaluations] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [CollectionsJson] nvarchar(max) NOT NULL,
        [CasesJson] nvarchar(max) NOT NULL,
        [ConfigurationFingerprint] nvarchar(64) NOT NULL,
        [ProfileKey] nvarchar(200) NOT NULL,
        [TopK] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RetrievalEvaluations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RetrievalEvaluations_BackgroundJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [operations].[BackgroundJobs] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RetrievalEvaluations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
    DECLARE @description85 AS sql_variant;
    SET @description85 = N'檢索驗收集、授權知識庫、索引與設定指紋及可續跑背景工作；不保存檢索來源原文。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations';
    SET @description85 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'Id';
    SET @description85 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'OwnerId';
    SET @description85 = N'關聯背景工作識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'JobId';
    SET @description85 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'Title';
    SET @description85 = N'評測所使用的知識庫識別碼陣列；執行與讀取時重新檢查授權。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'CollectionsJson';
    SET @description85 = N'固定評測案例的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'CasesJson';
    SET @description85 = N'固定模型與生成設定的 SHA-256 指紋。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'ConfigurationFingerprint';
    SET @description85 = N'評測所固定的向量空間及切段規則識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'ProfileKey';
    SET @description85 = N'評測所固定的最大檢索結果數。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'TopK';
    SET @description85 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluations', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006155137_RetrievalEvaluationReports'
)
BEGIN
    CREATE TABLE [quality].[RetrievalEvaluationResults] (
        [RunId] uniqueidentifier NOT NULL,
        [CaseIndex] int NOT NULL,
        [Mode] nvarchar(24) NOT NULL,
        [ActualMode] nvarchar(120) NOT NULL,
        [Unavailable] nvarchar(80) NULL,
        [Recall] float NULL,
        [ReciprocalRank] float NULL,
        [Ndcg] float NULL,
        [Refused] bit NULL,
        [RewriteMs] bigint NOT NULL,
        [EmbedMs] bigint NOT NULL,
        [SearchMs] bigint NOT NULL,
        [RerankMs] bigint NOT NULL,
        [ElapsedMs] bigint NOT NULL,
        CONSTRAINT [PK_RetrievalEvaluationResults] PRIMARY KEY ([RunId], [CaseIndex], [Mode]),
        CONSTRAINT [FK_RetrievalEvaluationResults_RetrievalEvaluations_RunId] FOREIGN KEY ([RunId]) REFERENCES [quality].[RetrievalEvaluations] ([Id]) ON DELETE CASCADE
    );
    DECLARE @description86 AS sql_variant;
    SET @description86 = N'四種檢索模式的相關性、無來源拒答與延遲指標；不保存查詢或檢索來源原文。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults';
    SET @description86 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'RunId';
    SET @description86 = N'評測案例的從零開始索引。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'CaseIndex';
    SET @description86 = N'請求的檢索比較模式。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'Mode';
    SET @description86 = N'實際檢索模式，包含略過或降級標記。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'ActualMode';
    SET @description86 = N'此模式無法評測的錯誤代碼；空值表示已完成。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'Unavailable';
    SET @description86 = N'前 K 筆命中的相關文件比例；無答案題保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'Recall';
    SET @description86 = N'第一筆相關命中的排名倒數；無答案題保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'ReciprocalRank';
    SET @description86 = N'前 K 筆依相關性分級計算的正規化折損累積增益。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'Ndcg';
    SET @description86 = N'無答案題是否未提供來源；有答案題保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'Refused';
    SET @description86 = N'查詢改寫耗時，單位毫秒。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'RewriteMs';
    SET @description86 = N'查詢向量化含快取耗時，單位毫秒。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'EmbedMs';
    SET @description86 = N'授權候選召回耗時，單位毫秒。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'SearchMs';
    SET @description86 = N'重排耗時，單位毫秒。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'RerankMs';
    SET @description86 = N'執行耗時，以毫秒計。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'quality', 'TABLE', N'RetrievalEvaluationResults', 'COLUMN', N'ElapsedMs';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006155137_RetrievalEvaluationReports'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RetrievalEvaluations_JobId] ON [quality].[RetrievalEvaluations] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006155137_RetrievalEvaluationReports'
)
BEGIN
    CREATE INDEX [IX_RetrievalEvaluations_OwnerId_CreatedAt] ON [quality].[RetrievalEvaluations] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006155137_RetrievalEvaluationReports'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006155137_RetrievalEvaluationReports', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [inference].[RunEvents] ADD [IssueCode] nvarchar(40) NULL;
    DECLARE @description87 AS sql_variant;
    SET @description87 = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description87, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'IssueCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [operations].[Notifications] ADD [IssueCode] nvarchar(40) NULL;
    DECLARE @description88 AS sql_variant;
    SET @description88 = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description88, 'SCHEMA', N'operations', 'TABLE', N'Notifications', 'COLUMN', N'IssueCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [conversations].[Messages] ADD [IssueCode] nvarchar(40) NULL;
    DECLARE @description89 AS sql_variant;
    SET @description89 = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description89, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'IssueCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [inference].[GenerationRuns] ADD [IssueCode] nvarchar(40) NULL;
    DECLARE @description90 AS sql_variant;
    SET @description90 = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description90, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'IssueCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [inference].[GenerationRuns] ADD [OperationId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    DECLARE @description91 AS sql_variant;
    SET @description91 = N'持久作業識別，跨佇列與重試保持不變。';
    EXEC sp_addextendedproperty 'MS_Description', @description91, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'OperationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [inference].[GenerationRuns] ADD [ParentSpanId] nvarchar(16) NULL;
    DECLARE @description92 AS sql_variant;
    SET @description92 = N'排程來源的 W3C span 識別，重試沿用同一 trace。';
    EXEC sp_addextendedproperty 'MS_Description', @description92, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ParentSpanId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [inference].[GenerationRuns] ADD [TraceId] nvarchar(32) NULL;
    DECLARE @description93 AS sql_variant;
    SET @description93 = N'W3C 流程追蹤識別，僅由伺服器建立。';
    EXEC sp_addextendedproperty 'MS_Description', @description93, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'TraceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    DECLARE @description94 AS sql_variant;
    EXEC sp_dropextendedproperty 'MS_Description', 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ErrorMessage';
    SET @description94 = N'固定安全提示與查證代碼；不可保存例外自由文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description94, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ErrorMessage';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [operations].[BackgroundJobs] ADD [IssueCode] nvarchar(40) NULL;
    DECLARE @description95 AS sql_variant;
    SET @description95 = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description95, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'IssueCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [operations].[BackgroundJobs] ADD [OperationId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    DECLARE @description96 AS sql_variant;
    SET @description96 = N'持久作業識別，跨佇列與重試保持不變。';
    EXEC sp_addextendedproperty 'MS_Description', @description96, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'OperationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [operations].[BackgroundJobs] ADD [ParentSpanId] nvarchar(16) NULL;
    DECLARE @description97 AS sql_variant;
    SET @description97 = N'排程來源的 W3C span 識別，重試沿用同一 trace。';
    EXEC sp_addextendedproperty 'MS_Description', @description97, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ParentSpanId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [operations].[BackgroundJobs] ADD [TraceId] nvarchar(32) NULL;
    DECLARE @description98 AS sql_variant;
    SET @description98 = N'W3C 流程追蹤識別，僅由伺服器建立。';
    EXEC sp_addextendedproperty 'MS_Description', @description98, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'TraceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [operations].[AuditEvents] ADD [IssueCode] nvarchar(40) NULL;
    DECLARE @description99 AS sql_variant;
    SET @description99 = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description99, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'IssueCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [operations].[AuditEvents] ADD [OperationId] uniqueidentifier NULL;
    DECLARE @description100 AS sql_variant;
    SET @description100 = N'持久作業識別，跨佇列與重試保持不變。';
    EXEC sp_addextendedproperty 'MS_Description', @description100, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'OperationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    ALTER TABLE [operations].[AuditEvents] ADD [TraceId] nvarchar(32) NULL;
    DECLARE @description101 AS sql_variant;
    SET @description101 = N'W3C 流程追蹤識別，僅由伺服器建立。';
    EXEC sp_addextendedproperty 'MS_Description', @description101, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'TraceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    CREATE TABLE [operations].[DiagnosticEvents] (
        [LogId] uniqueidentifier NOT NULL,
        [At] datetimeoffset NOT NULL,
        [Level] int NOT NULL,
        [Category] nvarchar(180) NOT NULL,
        [EventId] int NOT NULL,
        [EventName] nvarchar(100) NOT NULL,
        [MessageTemplate] nvarchar(2048) NOT NULL,
        [PropertiesJson] nvarchar(max) NOT NULL,
        [Service] nvarchar(80) NOT NULL,
        [Environment] nvarchar(32) NOT NULL,
        [Version] nvarchar(80) NOT NULL,
        [Instance] nvarchar(100) NOT NULL,
        [IssueCode] nvarchar(40) NULL,
        [TraceId] nvarchar(32) NULL,
        [SpanId] nvarchar(16) NULL,
        [RequestId] nvarchar(40) NULL,
        [OperationId] uniqueidentifier NULL,
        [JobId] uniqueidentifier NULL,
        [RunId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [Attempt] int NULL,
        [Method] nvarchar(10) NULL,
        [Route] nvarchar(240) NULL,
        [StatusCode] int NULL,
        [DurationMs] float NULL,
        [ExternalService] nvarchar(32) NULL,
        [ErrorCode] nvarchar(80) NULL,
        [ExceptionType] nvarchar(180) NULL,
        [ExceptionDetail] nvarchar(max) NULL,
        [UntrustedClient] bit NOT NULL,
        CONSTRAINT [PK_DiagnosticEvents] PRIMARY KEY NONCLUSTERED ([LogId])
    );
    DECLARE @description102 AS sql_variant;
    SET @description102 = N'共用診斷日誌；只保存受控且已遮罩的事件欄位，LogId 唯一用於補送去重。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents';
    SET @description102 = N'不可重複的日誌識別，SQL 補送去重鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'LogId';
    SET @description102 = N'診斷事件發生的 UTC 時間，時間與 LogId 為排序及游標分頁鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'At';
    SET @description102 = N'Microsoft.Extensions.Logging 層級值：Trace=0 到 Critical=5。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Level';
    SET @description102 = N'診斷事件的受控 Category 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Category';
    SET @description102 = N'穩定的事件分類識別碼，跨程式版本保持意義一致。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'EventId';
    SET @description102 = N'穩定的事件名稱，供模組及流程查詢。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'EventName';
    SET @description102 = N'結構化訊息模板，禁止串接內容與秘密。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'MessageTemplate';
    SET @description102 = N'白名單純量 metadata，大小及欄位數受限。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'PropertiesJson';
    SET @description102 = N'診斷事件的受控 Service 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Service';
    SET @description102 = N'診斷事件的受控 Environment 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Environment';
    SET @description102 = N'應用程式 informational version，用於辨認發版。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Version';
    SET @description102 = N'診斷事件的受控 Instance 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Instance';
    SET @description102 = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'IssueCode';
    SET @description102 = N'W3C 流程追蹤識別，僅由伺服器建立。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'TraceId';
    SET @description102 = N'診斷事件的受控 SpanId 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'SpanId';
    SET @description102 = N'診斷事件的受控 RequestId 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'RequestId';
    SET @description102 = N'持久作業識別，跨佇列與重試保持不變。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'OperationId';
    SET @description102 = N'關聯背景工作識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'JobId';
    SET @description102 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'RunId';
    SET @description102 = N'伺服器解析的受控使用者識別碼；不接受客戶端傳入。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'UserId';
    SET @description102 = N'背景工作執行／重試次數。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Attempt';
    SET @description102 = N'診斷事件的受控 Method 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Method';
    SET @description102 = N'HTTP 路由模板，不含實際路徑值或查詢參數。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'Route';
    SET @description102 = N'診斷事件的受控 StatusCode 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'StatusCode';
    SET @description102 = N'診斷事件的受控 DurationMs 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'DurationMs';
    SET @description102 = N'診斷事件的受控 ExternalService 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'ExternalService';
    SET @description102 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'ErrorCode';
    SET @description102 = N'診斷事件的受控 ExceptionType 欄位；由集中日誌政策限制大小與遮罩。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'ExceptionType';
    SET @description102 = N'省略例外自由文字與路徑的型別、錯誤碼及堆疊。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'ExceptionDetail';
    SET @description102 = N'明確標示不可信用戶端回報。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'operations', 'TABLE', N'DiagnosticEvents', 'COLUMN', N'UntrustedClient';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''logs.detail'', CAST(1 AS bit), N''日誌診斷詳情'', N'''', 111),
    (N''logs.export'', CAST(1 AS bit), N''日誌匯出'', N'''', 112),
    (N''logs.query'', CAST(1 AS bit), N''系統日誌'', N''/admin/logs'', 110)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''logs.detail'', N''administrators''),
    (N''logs.export'', N''administrators''),
    (N''logs.query'', N''administrators'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    CREATE CLUSTERED INDEX [IX_DiagnosticEvents_At_LogId] ON [operations].[DiagnosticEvents] ([At] DESC, [LogId] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    CREATE INDEX [IX_DiagnosticEvents_Category_At_LogId] ON [operations].[DiagnosticEvents] ([Category], [At], [LogId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_DiagnosticEvents_ErrorCode_At_LogId] ON [operations].[DiagnosticEvents] ([ErrorCode], [At], [LogId]) WHERE [ErrorCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    CREATE INDEX [IX_DiagnosticEvents_EventId_At_LogId] ON [operations].[DiagnosticEvents] ([EventId], [At], [LogId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    CREATE INDEX [IX_DiagnosticEvents_EventName_At_LogId] ON [operations].[DiagnosticEvents] ([EventName], [At], [LogId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_DiagnosticEvents_Instance_At_LogId] ON [operations].[DiagnosticEvents] ([Instance], [At], [LogId]) WHERE [Instance] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_DiagnosticEvents_IssueCode_At_LogId] ON [operations].[DiagnosticEvents] ([IssueCode], [At], [LogId]) WHERE [IssueCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_DiagnosticEvents_JobId_At_LogId] ON [operations].[DiagnosticEvents] ([JobId], [At], [LogId]) WHERE [JobId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    CREATE INDEX [IX_DiagnosticEvents_Level_At_LogId] ON [operations].[DiagnosticEvents] ([Level], [At], [LogId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_DiagnosticEvents_OperationId_At_LogId] ON [operations].[DiagnosticEvents] ([OperationId], [At], [LogId]) WHERE [OperationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_DiagnosticEvents_RunId_At_LogId] ON [operations].[DiagnosticEvents] ([RunId], [At], [LogId]) WHERE [RunId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_DiagnosticEvents_TraceId_At_LogId] ON [operations].[DiagnosticEvents] ([TraceId], [At], [LogId]) WHERE [TraceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007040053_SystemDiagnostics'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007040053_SystemDiagnostics', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008072048_RuntimeMonitoring'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''monitoring'', CAST(1 AS bit), N''即時監控'', N''/admin/monitoring'', 91)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008072048_RuntimeMonitoring'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''monitoring'', N''administrators'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008072048_RuntimeMonitoring'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008072048_RuntimeMonitoring', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008103634_IndependentActivityAudit'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''audit'', CAST(1 AS bit), N''活動稽核'', N''/admin/audit'', 92)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008103634_IndependentActivityAudit'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''audit'', N''administrators'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008103634_IndependentActivityAudit'
)
BEGIN
    INSERT INTO [access].[RoleGroupFeatures] ([GroupId], [FeatureId])
    SELECT existing.[GroupId], N'audit'
    FROM [access].[RoleGroupFeatures] AS existing
    WHERE existing.[FeatureId] = N'admin'
      AND NOT EXISTS (
        SELECT 1 FROM [access].[RoleGroupFeatures] AS granted
        WHERE granted.[GroupId] = existing.[GroupId] AND granted.[FeatureId] = N'audit');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008103634_IndependentActivityAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008103634_IndependentActivityAudit', N'10.0.12');
END;

COMMIT;
GO

