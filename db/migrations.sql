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
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    IF SCHEMA_ID(N'operations') IS NULL EXEC(N'CREATE SCHEMA [operations];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    IF SCHEMA_ID(N'conversations') IS NULL EXEC(N'CREATE SCHEMA [conversations];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    IF SCHEMA_ID(N'inference') IS NULL EXEC(N'CREATE SCHEMA [inference];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE TABLE [operations].[AuditEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [OwnerId] uniqueidentifier NOT NULL,
        [Action] nvarchar(64) NOT NULL,
        [ResourceId] uniqueidentifier NULL,
        [Result] nvarchar(80) NULL,
        [At] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE TABLE [inference].[ModelProfiles] (
        [Id] nvarchar(160) NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL,
        [ContextTokens] int NOT NULL,
        [MaxOutputTokens] int NOT NULL,
        [SupportsStreaming] bit NOT NULL,
        [SupportsUsage] bit NOT NULL,
        CONSTRAINT [PK_ModelProfiles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE TABLE [identity].[Users] (
        [Id] uniqueidentifier NOT NULL,
        [Sid] nvarchar(184) NOT NULL,
        [Account] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(256) NOT NULL,
        [LastSeenAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE TABLE [conversations].[Conversations] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [ActiveLeafId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE TABLE [identity].[UserPreferences] (
        [NexusUserId] uniqueidentifier NOT NULL,
        [Theme] nvarchar(12) NOT NULL,
        [ReducedMotion] bit NOT NULL,
        [DefaultModelId] nvarchar(160) NULL,
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([NexusUserId]),
        CONSTRAINT [FK_UserPreferences_Users_NexusUserId] FOREIGN KEY ([NexusUserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
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
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Messages_Messages_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE TABLE [inference].[GenerationRuns] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [ActiveOwnerId] uniqueidentifier NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [UserMessageId] uniqueidentifier NOT NULL,
        [AssistantMessageId] uniqueidentifier NOT NULL,
        [ModelId] nvarchar(160) NOT NULL,
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
        CONSTRAINT [PK_GenerationRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GenerationRuns_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GenerationRuns_Messages_AssistantMessageId] FOREIGN KEY ([AssistantMessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GenerationRuns_Messages_UserMessageId] FOREIGN KEY ([UserMessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GenerationRuns_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_At] ON [operations].[AuditEvents] ([At]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE INDEX [IX_Conversations_OwnerId_IsDeleted_UpdatedAt] ON [conversations].[Conversations] ([OwnerId], [IsDeleted], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_GenerationRuns_ActiveOwnerId] ON [inference].[GenerationRuns] ([ActiveOwnerId]) WHERE [ActiveOwnerId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE INDEX [IX_GenerationRuns_AssistantMessageId] ON [inference].[GenerationRuns] ([AssistantMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE INDEX [IX_GenerationRuns_ConversationId_CreatedAt] ON [inference].[GenerationRuns] ([ConversationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GenerationRuns_OwnerId_IdempotencyKey] ON [inference].[GenerationRuns] ([OwnerId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE INDEX [IX_GenerationRuns_UserMessageId] ON [inference].[GenerationRuns] ([UserMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE INDEX [IX_Messages_ConversationId_CreatedAt] ON [conversations].[Messages] ([ConversationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE INDEX [IX_Messages_ParentId] ON [conversations].[Messages] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Sid] ON [identity].[Users] ([Sid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003141107_InitialNexus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003141107_InitialNexus', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    IF SCHEMA_ID(N'access') IS NULL EXEC(N'CREATE SCHEMA [access];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    CREATE TABLE [access].[RoleGroups] (
        [Id] nvarchar(64) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_RoleGroups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    CREATE TABLE [access].[Roles] (
        [Id] nvarchar(64) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    CREATE TABLE [access].[RoleGroupFeatures] (
        [GroupId] nvarchar(64) NOT NULL,
        [FeatureId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_RoleGroupFeatures] PRIMARY KEY ([GroupId], [FeatureId]),
        CONSTRAINT [FK_RoleGroupFeatures_Features_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [access].[Features] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RoleGroupFeatures_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [access].[RoleGroups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    CREATE TABLE [access].[RoleGroupRoles] (
        [RoleId] nvarchar(64) NOT NULL,
        [GroupId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_RoleGroupRoles] PRIMARY KEY ([RoleId], [GroupId]),
        CONSTRAINT [FK_RoleGroupRoles_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [access].[RoleGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RoleGroupRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [access].[Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    CREATE TABLE [access].[UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [access].[Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''chat'', CAST(1 AS bit), N''AI 對話'', N''/chat'', 10)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[RoleGroups]'))
        SET IDENTITY_INSERT [access].[RoleGroups] ON;
    EXEC(N'INSERT INTO [access].[RoleGroups] ([Id], [Enabled], [Name])
    VALUES (N''workspace'', CAST(1 AS bit), N''基本工作台'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[RoleGroups]'))
        SET IDENTITY_INSERT [access].[RoleGroups] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[Roles]'))
        SET IDENTITY_INSERT [access].[Roles] ON;
    EXEC(N'INSERT INTO [access].[Roles] ([Id], [Enabled], [Name])
    VALUES (N''member'', CAST(1 AS bit), N''一般使用者'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[Roles]'))
        SET IDENTITY_INSERT [access].[Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''chat'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'GroupId', N'RoleId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupRoles]'))
        SET IDENTITY_INSERT [access].[RoleGroupRoles] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupRoles] ([GroupId], [RoleId])
    VALUES (N''workspace'', N''member'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'GroupId', N'RoleId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupRoles]'))
        SET IDENTITY_INSERT [access].[RoleGroupRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    CREATE INDEX [IX_RoleGroupFeatures_FeatureId] ON [access].[RoleGroupFeatures] ([FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    CREATE INDEX [IX_RoleGroupRoles_GroupId] ON [access].[RoleGroupRoles] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [access].[UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    INSERT INTO [access].[UserRoles] ([UserId], [RoleId])
    SELECT [Id], N'member' FROM [identity].[Users] AS u
    WHERE NOT EXISTS (SELECT 1 FROM [access].[UserRoles] AS r WHERE r.[UserId] = u.[Id] AND r.[RoleId] = N'member');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003175044_AccessControl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003175044_AccessControl', N'10.0.12');
END;

COMMIT;
GO
