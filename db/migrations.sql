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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    IF SCHEMA_ID(N'attachments') IS NULL EXEC(N'CREATE SCHEMA [attachments];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    IF SCHEMA_ID(N'library') IS NULL EXEC(N'CREATE SCHEMA [library];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    ALTER TABLE [conversations].[Messages] ADD [ErrorCode] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    ALTER TABLE [conversations].[Conversations] ADD [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    ALTER TABLE [conversations].[Conversations] ADD [IsFavorite] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    ALTER TABLE [conversations].[Conversations] ADD [SystemInstruction] nvarchar(4000) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    CREATE TABLE [attachments].[Attachments] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(180) NOT NULL,
        [ContentType] nvarchar(80) NOT NULL,
        [Size] bigint NOT NULL,
        [Data] varbinary(max) NOT NULL,
        [ExtractedText] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Attachments_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    CREATE TABLE [conversations].[ConversationLabels] (
        [ConversationId] uniqueidentifier NOT NULL,
        [Name] nvarchar(24) NOT NULL,
        CONSTRAINT [PK_ConversationLabels] PRIMARY KEY ([ConversationId], [Name]),
        CONSTRAINT [FK_ConversationLabels_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    CREATE TABLE [attachments].[MessageAttachments] (
        [MessageId] uniqueidentifier NOT NULL,
        [AttachmentId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_MessageAttachments] PRIMARY KEY ([MessageId], [AttachmentId]),
        CONSTRAINT [FK_MessageAttachments_Attachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [attachments].[Attachments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessageAttachments_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    CREATE INDEX [IX_Conversations_OwnerId_IsDeleted_IsArchived_IsFavorite_UpdatedAt] ON [conversations].[Conversations] ([OwnerId], [IsDeleted], [IsArchived], [IsFavorite], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    CREATE INDEX [IX_Attachments_OwnerId_CreatedAt] ON [attachments].[Attachments] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    CREATE INDEX [IX_MessageAttachments_AttachmentId] ON [attachments].[MessageAttachments] ([AttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    CREATE INDEX [IX_PromptTemplates_OwnerId_UpdatedAt] ON [library].[PromptTemplates] ([OwnerId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003204750_WorkspaceExtensions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003204750_WorkspaceExtensions', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [AutoFollow] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [DefaultReasoningEffort] nvarchar(16) NOT NULL DEFAULT N'auto';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [Density] nvarchar(16) NOT NULL DEFAULT N'comfortable';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [EnterToSend] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [NotifyOnCompletion] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [ReadingFontSize] int NOT NULL DEFAULT 17;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [ReadingLineHeight] float NOT NULL DEFAULT 1.8E0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [ReadingWidth] nvarchar(16) NOT NULL DEFAULT N'standard';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [SaveLocalDrafts] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    ALTER TABLE [identity].[UserPreferences] ADD [SidebarWidth] int NOT NULL DEFAULT 264;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004021115_PersonalSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004021115_PersonalSettings', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
)
BEGIN
    ALTER TABLE [operations].[AuditEvents] ADD [DetailsJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
)
BEGIN
    CREATE TABLE [access].[AdministratorBootstraps] (
        [UserId] uniqueidentifier NOT NULL,
        [GrantedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AdministratorBootstraps] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_AdministratorBootstraps_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''admin'', CAST(1 AS bit), N''管理'', N''/admin'', 90)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[RoleGroups]'))
        SET IDENTITY_INSERT [access].[RoleGroups] ON;
    EXEC(N'INSERT INTO [access].[RoleGroups] ([Id], [Enabled], [Name])
    VALUES (N''administrators'', CAST(1 AS bit), N''平台管理'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[RoleGroups]'))
        SET IDENTITY_INSERT [access].[RoleGroups] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[Roles]'))
        SET IDENTITY_INSERT [access].[Roles] ON;
    EXEC(N'INSERT INTO [access].[Roles] ([Id], [Enabled], [Name])
    VALUES (N''administrator'', CAST(1 AS bit), N''平台管理員'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name') AND [object_id] = OBJECT_ID(N'[access].[Roles]'))
        SET IDENTITY_INSERT [access].[Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''admin'', N''administrators'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'GroupId', N'RoleId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupRoles]'))
        SET IDENTITY_INSERT [access].[RoleGroupRoles] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupRoles] ([GroupId], [RoleId])
    VALUES (N''administrators'', N''administrator'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'GroupId', N'RoleId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupRoles]'))
        SET IDENTITY_INSERT [access].[RoleGroupRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004023221_Administration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004023221_Administration', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    IF SCHEMA_ID(N'knowledge') IS NULL EXEC(N'CREATE SCHEMA [knowledge];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    IF SCHEMA_ID(N'collaboration') IS NULL EXEC(N'CREATE SCHEMA [collaboration];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE TABLE [inference].[ModelInvocations] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(32) NOT NULL,
        [ModelId] nvarchar(160) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [InputTokens] bigint NULL,
        [OutputTokens] bigint NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ModelInvocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ModelInvocations_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE TABLE [collaboration].[Resources] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(24) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Resources] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Resources_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE TABLE [knowledge].[Collections] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_Collections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Collections_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE TABLE [attachments].[ResourceAttachments] (
        [ResourceId] uniqueidentifier NOT NULL,
        [AttachmentId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ResourceAttachments] PRIMARY KEY ([ResourceId], [AttachmentId]),
        CONSTRAINT [FK_ResourceAttachments_Attachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [attachments].[Attachments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ResourceAttachments_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE TABLE [collaboration].[ResourceGroups] (
        [ResourceId] uniqueidentifier NOT NULL,
        [GroupId] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_ResourceGroups] PRIMARY KEY ([ResourceId], [GroupId]),
        CONSTRAINT [FK_ResourceGroups_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ResourceGroups_RoleGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [access].[RoleGroups] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE TABLE [knowledge].[ConversationCollections] (
        [ConversationId] uniqueidentifier NOT NULL,
        [CollectionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ConversationCollections] PRIMARY KEY ([ConversationId], [CollectionId]),
        CONSTRAINT [FK_ConversationCollections_Collections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [knowledge].[Collections] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ConversationCollections_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [conversations].[Conversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''knowledge'', CAST(1 AS bit), N''知識庫'', N''/knowledge'', 30),
    (N''tasks'', CAST(1 AS bit), N''背景任務'', N''/tasks'', 70)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''knowledge'', N''workspace''),
    (N''tasks'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BackgroundJobs_ActiveKey] ON [operations].[BackgroundJobs] ([ActiveKey]) WHERE [ActiveKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_BackgroundJobs_OwnerId_CreatedAt] ON [operations].[BackgroundJobs] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_BackgroundJobs_ResourceId] ON [operations].[BackgroundJobs] ([ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_BackgroundJobs_Status_LeaseUntil_CreatedAt] ON [operations].[BackgroundJobs] ([Status], [LeaseUntil], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Chunks_DocumentId_Ordinal] ON [knowledge].[Chunks] ([DocumentId], [Ordinal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_ConversationCollections_CollectionId] ON [knowledge].[ConversationCollections] ([CollectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_Documents_AttachmentId] ON [knowledge].[Documents] ([AttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_Documents_CollectionId_Status] ON [knowledge].[Documents] ([CollectionId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_MessageCitations_DocumentId] ON [knowledge].[MessageCitations] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_ModelInvocations_OwnerId_CreatedAt] ON [inference].[ModelInvocations] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_ResourceAttachments_AttachmentId] ON [attachments].[ResourceAttachments] ([AttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_ResourceGroups_GroupId] ON [collaboration].[ResourceGroups] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_ResourceMembers_UserId] ON [collaboration].[ResourceMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    CREATE INDEX [IX_Resources_OwnerId_Kind_UpdatedAt] ON [collaboration].[Resources] ([OwnerId], [Kind], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) >= 17 EXEC(N'ALTER TABLE [knowledge].[Chunks] ADD [EmbeddingVector] VECTOR(768) NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004033749_KnowledgeAndJobs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004033749_KnowledgeAndJobs', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004050511_Artifacts'
)
BEGIN
    IF SCHEMA_ID(N'content') IS NULL EXEC(N'CREATE SCHEMA [content];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004050511_Artifacts'
)
BEGIN
    CREATE TABLE [content].[Artifacts] (
        [Id] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [SourceMessageId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        CONSTRAINT [PK_Artifacts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Artifacts_Messages_SourceMessageId] FOREIGN KEY ([SourceMessageId]) REFERENCES [conversations].[Messages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Artifacts_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004050511_Artifacts'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004050511_Artifacts'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''artifacts'', CAST(1 AS bit), N''成果文件'', N''/artifacts'', 40)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004050511_Artifacts'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''artifacts'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004050511_Artifacts'
)
BEGIN
    CREATE INDEX [IX_ArtifactRevisions_AuthorId] ON [content].[ArtifactRevisions] ([AuthorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004050511_Artifacts'
)
BEGIN
    CREATE INDEX [IX_Artifacts_SourceMessageId] ON [content].[Artifacts] ([SourceMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004050511_Artifacts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004050511_Artifacts', N'10.0.12');
END;

COMMIT;
GO

