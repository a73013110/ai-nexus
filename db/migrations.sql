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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    IF SCHEMA_ID(N'projects') IS NULL EXEC(N'CREATE SCHEMA [projects];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    ALTER TABLE [collaboration].[Resources] ADD [ParentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    ALTER TABLE [conversations].[Conversations] ADD [ProjectId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''projects'', CAST(1 AS bit), N''專案'', N''/projects'', 20)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''projects'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    CREATE INDEX [IX_Resources_ParentId] ON [collaboration].[Resources] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    CREATE INDEX [IX_Conversations_ProjectId] ON [conversations].[Conversations] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    CREATE INDEX [IX_Artifacts_ProjectId] ON [content].[Artifacts] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    CREATE INDEX [IX_ProjectTemplates_ProjectId] ON [projects].[ProjectTemplates] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    ALTER TABLE [content].[Artifacts] ADD CONSTRAINT [FK_Artifacts_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    ALTER TABLE [conversations].[Conversations] ADD CONSTRAINT [FK_Conversations_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    ALTER TABLE [collaboration].[Resources] ADD CONSTRAINT [FK_Resources_Resources_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [collaboration].[Resources] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004062441_Projects'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004062441_Projects', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004065040_Sharing'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004065040_Sharing'
)
BEGIN
    CREATE TABLE [collaboration].[ShareRecipients] (
        [ShareId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ShareRecipients] PRIMARY KEY ([ShareId], [UserId]),
        CONSTRAINT [FK_ShareRecipients_ShareLinks_ShareId] FOREIGN KEY ([ShareId]) REFERENCES [collaboration].[ShareLinks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ShareRecipients_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004065040_Sharing'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''shared'', CAST(1 AS bit), N''分享'', N''/shared'', 50)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004065040_Sharing'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''shared'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004065040_Sharing'
)
BEGIN
    CREATE INDEX [IX_ShareLinks_ExpiresAt] ON [collaboration].[ShareLinks] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004065040_Sharing'
)
BEGIN
    CREATE INDEX [IX_ShareLinks_OwnerId_CreatedAt] ON [collaboration].[ShareLinks] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004065040_Sharing'
)
BEGIN
    CREATE INDEX [IX_ShareRecipients_UserId_ShareId] ON [collaboration].[ShareRecipients] ([UserId], [ShareId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004065040_Sharing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004065040_Sharing', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    IF SCHEMA_ID(N'quality') IS NULL EXEC(N'CREATE SCHEMA [quality];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''quality'', CAST(1 AS bit), N''品質評測'', N''/quality'', 60)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''quality'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    CREATE INDEX [IX_EvaluationResults_ReviewerId] ON [quality].[EvaluationResults] ([ReviewerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EvaluationRuns_JobId] ON [quality].[EvaluationRuns] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    CREATE INDEX [IX_EvaluationRuns_OwnerId] ON [quality].[EvaluationRuns] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    CREATE INDEX [IX_EvaluationRuns_SetId_CreatedAt] ON [quality].[EvaluationRuns] ([SetId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    CREATE INDEX [IX_MessageFeedback_OwnerId_UpdatedAt] ON [quality].[MessageFeedback] ([OwnerId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004071532_Quality'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004071532_Quality', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004074051_Integrations'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004074051_Integrations'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''integrations'', CAST(1 AS bit), N''系統整合'', N''/integrations'', 80)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004074051_Integrations'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''integrations'', N''administrators'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004074051_Integrations'
)
BEGIN
    CREATE INDEX [IX_SourceReferences_SourceId_ExternalId] ON [content].[SourceReferences] ([SourceId], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004074051_Integrations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004074051_Integrations', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004112414_AdministrativeInspectionAudit'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_Action_Id] ON [operations].[AuditEvents] ([Action], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004112414_AdministrativeInspectionAudit'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_ResourceId_Id] ON [operations].[AuditEvents] ([ResourceId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004112414_AdministrativeInspectionAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004112414_AdministrativeInspectionAudit', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004151226_GenerationExecutorLeases'
)
BEGIN
    ALTER TABLE [inference].[GenerationRuns] ADD [ExecutorId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004151226_GenerationExecutorLeases'
)
BEGIN
    ALTER TABLE [inference].[GenerationRuns] ADD [LeaseExpiresAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004151226_GenerationExecutorLeases'
)
BEGIN
    CREATE INDEX [IX_GenerationRuns_ActiveOwnerId_LeaseExpiresAt] ON [inference].[GenerationRuns] ([ActiveOwnerId], [LeaseExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004151226_GenerationExecutorLeases'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004151226_GenerationExecutorLeases', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) >= 17 AND COL_LENGTH('knowledge.Chunks','EmbeddingVector1024') IS NULL EXEC(N'ALTER TABLE [knowledge].[Chunks] ADD [EmbeddingVector1024] VECTOR(1024) NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    IF SCHEMA_ID(N'workspace') IS NULL EXEC(N'CREATE SCHEMA [workspace];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] ON;
    EXEC(N'INSERT INTO [access].[Features] ([Id], [Enabled], [Name], [Route], [SortOrder])
    VALUES (N''dashboard'', CAST(1 AS bit), N''總覽'', N''/dashboard'', 5),
    (N''repositories'', CAST(1 AS bit), N''程式庫'', N''/repositories'', 65)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Enabled', N'Name', N'Route', N'SortOrder') AND [object_id] = OBJECT_ID(N'[access].[Features]'))
        SET IDENTITY_INSERT [access].[Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] ON;
    EXEC(N'INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
    VALUES (N''dashboard'', N''workspace''),
    (N''repositories'', N''workspace'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'GroupId') AND [object_id] = OBJECT_ID(N'[access].[RoleGroupFeatures]'))
        SET IDENTITY_INSERT [access].[RoleGroupFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_ModelCharges_ConversationId] ON [inference].[ModelCharges] ([ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_ModelCharges_CreatedAt] ON [inference].[ModelCharges] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_ModelCharges_OwnerId_CreatedAt] ON [inference].[ModelCharges] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_ModelCharges_PriceId] ON [inference].[ModelCharges] ([PriceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_ModelPrices_CreatedBy] ON [inference].[ModelPrices] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ModelPrices_Provider_ModelId_EffectiveAt] ON [inference].[ModelPrices] ([Provider], [ModelId], [EffectiveAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_RepositoryImports_DocumentId] ON [knowledge].[RepositoryImports] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_RepositoryImports_OwnerId_CollectionId_Repository_Commit_Path] ON [knowledge].[RepositoryImports] ([OwnerId], [CollectionId], [Repository], [Commit], [Path]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_WebSearches_OwnerId_CreatedAt] ON [inference].[WebSearches] ([OwnerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WebSearches_OwnerId_IdempotencyKey] ON [inference].[WebSearches] ([OwnerId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    CREATE INDEX [IX_WebSearches_RunId] ON [inference].[WebSearches] ([RunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004235154_ModelSpendAndConnectedWorkspace'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004235154_ModelSpendAndConnectedWorkspace', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description AS sql_variant;
    SET @description = N'使用者明確啟用的網路搜尋、冪等識別、結果與費用。';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', N'inference', 'TABLE', N'WebSearches';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description1 AS sql_variant;
    SET @description1 = N'使用者身分、AD SID 綁定、可用登入方式與工作階段撤銷版本；不保存 AD 密碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description1, 'SCHEMA', N'identity', 'TABLE', N'Users';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description2 AS sql_variant;
    SET @description2 = N'使用者與角色的分派關聯。';
    EXEC sp_addextendedproperty 'MS_Description', @description2, 'SCHEMA', N'access', 'TABLE', N'UserRoles';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description3 AS sql_variant;
    SET @description3 = N'使用者個人外觀、閱讀、對話操作與通知偏好；不含服務密鑰。';
    EXEC sp_addextendedproperty 'MS_Description', @description3, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description4 AS sql_variant;
    SET @description4 = N'外部來源匯入的識別、來源版本與匯入時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description4, 'SCHEMA', N'content', 'TABLE', N'SourceReferences';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description5 AS sql_variant;
    SET @description5 = N'分享的具名收件人；與原資源 ACL 分開判定。';
    EXEC sp_addextendedproperty 'MS_Description', @description5, 'SCHEMA', N'collaboration', 'TABLE', N'ShareRecipients';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description6 AS sql_variant;
    SET @description6 = N'分享版本快照、有效期限、附件選項及撤銷狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description6, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description7 AS sql_variant;
    SET @description7 = N'生成事件的有序 SSE 重播紀錄；完整生成狀態以 GenerationRuns 為準。';
    EXEC sp_addextendedproperty 'MS_Description', @description7, 'SCHEMA', N'inference', 'TABLE', N'RunEvents';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description8 AS sql_variant;
    SET @description8 = N'可分派給使用者的角色；停用後不再提供有效授權。';
    EXEC sp_addextendedproperty 'MS_Description', @description8, 'SCHEMA', N'access', 'TABLE', N'Roles';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description9 AS sql_variant;
    SET @description9 = N'角色所加入的功能群組，集中管理功能及模型政策。';
    EXEC sp_addextendedproperty 'MS_Description', @description9, 'SCHEMA', N'access', 'TABLE', N'RoleGroups';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description10 AS sql_variant;
    SET @description10 = N'角色與功能群組的授權關聯。';
    EXEC sp_addextendedproperty 'MS_Description', @description10, 'SCHEMA', N'access', 'TABLE', N'RoleGroupRoles';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description11 AS sql_variant;
    SET @description11 = N'功能群組與功能的授權關聯；有效功能取聯集。';
    EXEC sp_addextendedproperty 'MS_Description', @description11, 'SCHEMA', N'access', 'TABLE', N'RoleGroupFeatures';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description12 AS sql_variant;
    SET @description12 = N'共用資源的擁有者、種類、階層及版本；作為資料 ACL 邊界。';
    EXEC sp_addextendedproperty 'MS_Description', @description12, 'SCHEMA', N'collaboration', 'TABLE', N'Resources';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description13 AS sql_variant;
    SET @description13 = N'資源對具名使用者授予的閱讀或編輯權限。';
    EXEC sp_addextendedproperty 'MS_Description', @description13, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceMembers';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description14 AS sql_variant;
    SET @description14 = N'資源對功能群組授予的唯讀權限。';
    EXEC sp_addextendedproperty 'MS_Description', @description14, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceGroups';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description15 AS sql_variant;
    SET @description15 = N'知識庫或專案資源與附件的引用關聯。';
    EXEC sp_addextendedproperty 'MS_Description', @description15, 'SCHEMA', N'attachments', 'TABLE', N'ResourceAttachments';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description16 AS sql_variant;
    SET @description16 = N'程式庫文件匯入所固定的主機、repository、commit 與檔案路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description16, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description17 AS sql_variant;
    SET @description17 = N'使用者個人的 Gitea 連線及 Data Protection 保護的存取 token。';
    EXEC sp_addextendedproperty 'MS_Description', @description17, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description18 AS sql_variant;
    SET @description18 = N'使用者私人提示詞範本。';
    EXEC sp_addextendedproperty 'MS_Description', @description18, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description19 AS sql_variant;
    SET @description19 = N'專案建立範本與預設指令。';
    EXEC sp_addextendedproperty 'MS_Description', @description19, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description20 AS sql_variant;
    SET @description20 = N'專案資源、共用指令及範本版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description20, 'SCHEMA', N'projects', 'TABLE', N'Projects';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description21 AS sql_variant;
    SET @description21 = N'核准模型的能力、上下文與輸出限制。';
    EXEC sp_addextendedproperty 'MS_Description', @description21, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description22 AS sql_variant;
    SET @description22 = N'依供應商、模型、幣別及成本類型保存的不可變價格版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description22, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description23 AS sql_variant;
    SET @description23 = N'文字、OCR、embedding 等模型呼叫的狀態與實際用量。';
    EXEC sp_addextendedproperty 'MS_Description', @description23, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description24 AS sql_variant;
    SET @description24 = N'各呼叫當時的價格與用量快照；未知費用保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description24, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description25 AS sql_variant;
    SET @description25 = N'對話訊息樹；提問、回答、重新生成與編輯保留各版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description25, 'SCHEMA', N'conversations', 'TABLE', N'Messages';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description26 AS sql_variant;
    SET @description26 = N'使用者對 AI 回答的私人評分與意見。';
    EXEC sp_addextendedproperty 'MS_Description', @description26, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description27 AS sql_variant;
    SET @description27 = N'回答生成當時的知識引用、文件頁碼與摘要快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description27, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description28 AS sql_variant;
    SET @description28 = N'訊息與附件的關聯及呈現順序。';
    EXEC sp_addextendedproperty 'MS_Description', @description28, 'SCHEMA', N'attachments', 'TABLE', N'MessageAttachments';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description29 AS sql_variant;
    SET @description29 = N'功能群組的模型白名單、每日生成及附件空間限制。';
    EXEC sp_addextendedproperty 'MS_Description', @description29, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description30 AS sql_variant;
    SET @description30 = N'聊天生成的持久狀態、冪等請求、執行租約、回答及用量。';
    EXEC sp_addextendedproperty 'MS_Description', @description30, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description31 AS sql_variant;
    SET @description31 = N'模組註冊的功能入口、顯示名稱、路由及啟用狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description31, 'SCHEMA', N'access', 'TABLE', N'Features';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description32 AS sql_variant;
    SET @description32 = N'評測題庫、固定測試案例與版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description32, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description33 AS sql_variant;
    SET @description33 = N'評測執行的題庫與模型設定快照、背景工作與狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description33, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description34 AS sql_variant;
    SET @description34 = N'各案例／模型組合的輸出、指標、用量與人工覆核結果。';
    EXEC sp_addextendedproperty 'MS_Description', @description34, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description35 AS sql_variant;
    SET @description35 = N'知識庫文件的原始附件、分析／索引狀態與 embedding profile。';
    EXEC sp_addextendedproperty 'MS_Description', @description35, 'SCHEMA', N'knowledge', 'TABLE', N'Documents';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description36 AS sql_variant;
    SET @description36 = N'文件逐頁擷取的文字、頁碼與 OCR 結果。';
    EXEC sp_addextendedproperty 'MS_Description', @description36, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description37 AS sql_variant;
    SET @description37 = N'使用者私人對話、目前訊息分支、收藏封存與自訂指令。';
    EXEC sp_addextendedproperty 'MS_Description', @description37, 'SCHEMA', N'conversations', 'TABLE', N'Conversations';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description38 AS sql_variant;
    SET @description38 = N'使用者對話的分類標籤。';
    EXEC sp_addextendedproperty 'MS_Description', @description38, 'SCHEMA', N'conversations', 'TABLE', N'ConversationLabels';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description39 AS sql_variant;
    SET @description39 = N'對話選定的知識庫來源關聯。';
    EXEC sp_addextendedproperty 'MS_Description', @description39, 'SCHEMA', N'knowledge', 'TABLE', N'ConversationCollections';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description40 AS sql_variant;
    SET @description40 = N'知識庫的資料資源關聯及索引資訊。';
    EXEC sp_addextendedproperty 'MS_Description', @description40, 'SCHEMA', N'knowledge', 'TABLE', N'Collections';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description41 AS sql_variant;
    SET @description41 = N'知識檢索片段、頁碼、摘要與向量；查詢先套用資料 ACL。';
    EXEC sp_addextendedproperty 'MS_Description', @description41, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description42 AS sql_variant;
    SET @description42 = N'文件索引與評測等背景工作的租約、進度、重試與取消狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description42, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description43 AS sql_variant;
    SET @description43 = N'操作與管理異動稽核；保存實際管理者及有效身分，不記錄密碼或私密內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description43, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description44 AS sql_variant;
    SET @description44 = N'使用者附件的原始二進位資料、擷取文字及保留狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description44, 'SCHEMA', N'attachments', 'TABLE', N'Attachments';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description45 AS sql_variant;
    SET @description45 = N'使用者保存的成果文件與目前版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description45, 'SCHEMA', N'content', 'TABLE', N'Artifacts';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description46 AS sql_variant;
    SET @description46 = N'成果文件不可變版本、內容與作者。';
    EXEC sp_addextendedproperty 'MS_Description', @description46, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description47 AS sql_variant;
    SET @description47 = N'管理者首次啟動授權的永久標記，防止撤銷後重新授權。';
    EXEC sp_addextendedproperty 'MS_Description', @description47, 'SCHEMA', N'access', 'TABLE', N'AdministratorBootstraps';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description48 AS sql_variant;
    SET @description48 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description48, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'Status';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description49 AS sql_variant;
    SET @description49 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description49, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'RunId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description50 AS sql_variant;
    SET @description50 = N'搜尋結果的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description50, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'ResultsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description51 AS sql_variant;
    SET @description51 = N'請求內容指紋，用於辨識冪等識別碼衝突。';
    EXEC sp_addextendedproperty 'MS_Description', @description51, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'RequestHash';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description52 AS sql_variant;
    SET @description52 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description52, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description53 AS sql_variant;
    SET @description53 = N'擁有者範圍內的冪等請求識別，避免重試重複處理。';
    EXEC sp_addextendedproperty 'MS_Description', @description53, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'IdempotencyKey';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description54 AS sql_variant;
    SET @description54 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description54, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description55 AS sql_variant;
    SET @description55 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description55, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'ConversationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description56 AS sql_variant;
    SET @description56 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description56, 'SCHEMA', N'inference', 'TABLE', N'WebSearches', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description57 AS sql_variant;
    SET @description57 = N'AD 的不可變 SID；尚未綁定 AD 的手動帳號使用 managed: 識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description57, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'Sid';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description58 AS sql_variant;
    SET @description58 = N'使用者最近登入／活動時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description58, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'LastSeenAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description59 AS sql_variant;
    SET @description59 = N'使用者或模型的介面顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description59, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'DisplayName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description60 AS sql_variant;
    SET @description60 = N'登入身分顯示帳號；AD 連結後保存目錄提供的帳號。';
    EXEC sp_addextendedproperty 'MS_Description', @description60, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'Account';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description61 AS sql_variant;
    SET @description61 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description61, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [AdAccount] nvarchar(64) NULL;
    DECLARE @description62 AS sql_variant;
    SET @description62 = N'預先配置的 AD 帳號正規化值；唯一、不含網域，驗證成功後以 SID 固定綁定。';
    EXEC sp_addextendedproperty 'MS_Description', @description62, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'AdAccount';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [AdEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
    DECLARE @description63 AS sql_variant;
    SET @description63 = N'是否允許使用 AD／Windows 整合驗證登入。';
    EXEC sp_addextendedproperty 'MS_Description', @description63, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'AdEnabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [DeletedAt] datetimeoffset NULL;
    DECLARE @description64 AS sql_variant;
    SET @description64 = N'登入身分刪除時間；保留關聯與歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description64, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'DeletedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [Enabled] bit NOT NULL DEFAULT CAST(1 AS bit);
    DECLARE @description65 AS sql_variant;
    SET @description65 = N'是否啟用；停用不刪除歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description65, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [FailedLogins] int NOT NULL DEFAULT 0;
    DECLARE @description66 AS sql_variant;
    SET @description66 = N'本地登入連續失敗次數，用於暫時鎖定。';
    EXEC sp_addextendedproperty 'MS_Description', @description66, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'FailedLogins';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [LocalAccount] nvarchar(64) NULL;
    DECLARE @description67 AS sql_variant;
    SET @description67 = N'本地登入帳號正規化值；唯一且不區分大小寫。';
    EXEC sp_addextendedproperty 'MS_Description', @description67, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'LocalAccount';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [LocalEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
    DECLARE @description68 AS sql_variant;
    SET @description68 = N'是否允許本地密碼登入；與 AD 驗證獨立。';
    EXEC sp_addextendedproperty 'MS_Description', @description68, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'LocalEnabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [LockedUntil] datetimeoffset NULL;
    DECLARE @description69 AS sql_variant;
    SET @description69 = N'本地帳號暫時鎖定的到期時間；空值表示未鎖定。';
    EXEC sp_addextendedproperty 'MS_Description', @description69, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'LockedUntil';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [PasswordHash] nvarchar(512) NULL;
    DECLARE @description70 AS sql_variant;
    SET @description70 = N'本地密碼的 Argon2id PHC 雜湊，含版本、成本、隨機 salt 與衍生值；不可還原。';
    EXEC sp_addextendedproperty 'MS_Description', @description70, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'PasswordHash';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [ProfileManaged] bit NOT NULL DEFAULT CAST(0 AS bit);
    DECLARE @description71 AS sql_variant;
    SET @description71 = N'姓名是否由管理者維護；開啟後 AD 目錄不覆寫姓名。';
    EXEC sp_addextendedproperty 'MS_Description', @description71, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'ProfileManaged';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [SecurityVersion] int NOT NULL DEFAULT 0;
    DECLARE @description72 AS sql_variant;
    SET @description72 = N'登入政策或密碼變更時遞增，立即撤銷舊工作階段。';
    EXEC sp_addextendedproperty 'MS_Description', @description72, 'SCHEMA', N'identity', 'TABLE', N'Users', 'COLUMN', N'SecurityVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description73 AS sql_variant;
    SET @description73 = N'關聯角色的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description73, 'SCHEMA', N'access', 'TABLE', N'UserRoles', 'COLUMN', N'RoleId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description74 AS sql_variant;
    SET @description74 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description74, 'SCHEMA', N'access', 'TABLE', N'UserRoles', 'COLUMN', N'UserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description75 AS sql_variant;
    SET @description75 = N'外觀偏好：system、light 或 dark。';
    EXEC sp_addextendedproperty 'MS_Description', @description75, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'Theme';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description76 AS sql_variant;
    SET @description76 = N'側欄寬度偏好。';
    EXEC sp_addextendedproperty 'MS_Description', @description76, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'SidebarWidth';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description77 AS sql_variant;
    SET @description77 = N'是否在瀏覽器按使用者保存草稿。';
    EXEC sp_addextendedproperty 'MS_Description', @description77, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'SaveLocalDrafts';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description78 AS sql_variant;
    SET @description78 = N'是否減少動畫與動態效果。';
    EXEC sp_addextendedproperty 'MS_Description', @description78, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'ReducedMotion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description79 AS sql_variant;
    SET @description79 = N'閱讀區寬度偏好。';
    EXEC sp_addextendedproperty 'MS_Description', @description79, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'ReadingWidth';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description80 AS sql_variant;
    SET @description80 = N'對話閱讀行高倍率。';
    EXEC sp_addextendedproperty 'MS_Description', @description80, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'ReadingLineHeight';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description81 AS sql_variant;
    SET @description81 = N'對話文字大小，以 CSS px 的偏好值記錄。';
    EXEC sp_addextendedproperty 'MS_Description', @description81, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'ReadingFontSize';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description82 AS sql_variant;
    SET @description82 = N'是否在背景分頁提醒回答完成。';
    EXEC sp_addextendedproperty 'MS_Description', @description82, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'NotifyOnCompletion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description83 AS sql_variant;
    SET @description83 = N'是否以 Enter 送出提問；IME 組字不送出。';
    EXEC sp_addextendedproperty 'MS_Description', @description83, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'EnterToSend';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description84 AS sql_variant;
    SET @description84 = N'介面密度偏好。';
    EXEC sp_addextendedproperty 'MS_Description', @description84, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'Density';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description85 AS sql_variant;
    SET @description85 = N'偏好的推理強度。';
    EXEC sp_addextendedproperty 'MS_Description', @description85, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'DefaultReasoningEffort';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description86 AS sql_variant;
    SET @description86 = N'偏好的核准模型識別碼；空值使用伺服器預設。';
    EXEC sp_addextendedproperty 'MS_Description', @description86, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'DefaultModelId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description87 AS sql_variant;
    SET @description87 = N'生成時是否跟隨最新回答。';
    EXEC sp_addextendedproperty 'MS_Description', @description87, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'AutoFollow';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description88 AS sql_variant;
    SET @description88 = N'個人偏好對應使用者的主鍵與外鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description88, 'SCHEMA', N'identity', 'TABLE', N'UserPreferences', 'COLUMN', N'NexusUserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description89 AS sql_variant;
    SET @description89 = N'外部資料來源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description89, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'SourceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description90 AS sql_variant;
    SET @description90 = N'外部來源或 repository 的固定版本識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description90, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'Revision';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description91 AS sql_variant;
    SET @description91 = N'此來源版本明確匯入的時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description91, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'ImportedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description92 AS sql_variant;
    SET @description92 = N'外部來源中的紀錄識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description92, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'ExternalId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description93 AS sql_variant;
    SET @description93 = N'關聯成果文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description93, 'SCHEMA', N'content', 'TABLE', N'SourceReferences', 'COLUMN', N'ArtifactId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description94 AS sql_variant;
    SET @description94 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description94, 'SCHEMA', N'collaboration', 'TABLE', N'ShareRecipients', 'COLUMN', N'UserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description95 AS sql_variant;
    SET @description95 = N'關聯分享的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description95, 'SCHEMA', N'collaboration', 'TABLE', N'ShareRecipients', 'COLUMN', N'ShareId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description96 AS sql_variant;
    SET @description96 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description96, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'Title';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description97 AS sql_variant;
    SET @description97 = N'外部資料來源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description97, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'SourceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description98 AS sql_variant;
    SET @description98 = N'分享時的固定內容快照；不隨後續編輯變動。';
    EXEC sp_addextendedproperty 'MS_Description', @description98, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'SnapshotJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description99 AS sql_variant;
    SET @description99 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description99, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description100 AS sql_variant;
    SET @description100 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description100, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'Kind';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description101 AS sql_variant;
    SET @description101 = N'分享是否已撤銷。';
    EXEC sp_addextendedproperty 'MS_Description', @description101, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'IsRevoked';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description102 AS sql_variant;
    SET @description102 = N'是否明確允許分享附件。';
    EXEC sp_addextendedproperty 'MS_Description', @description102, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'IncludeAttachments';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description103 AS sql_variant;
    SET @description103 = N'分享或授權到期時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description103, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'ExpiresAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description104 AS sql_variant;
    SET @description104 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description104, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description105 AS sql_variant;
    SET @description105 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description105, 'SCHEMA', N'collaboration', 'TABLE', N'ShareLinks', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description106 AS sql_variant;
    SET @description106 = N'事件的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description106, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'Type';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description107 AS sql_variant;
    SET @description107 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description107, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'Status';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description108 AS sql_variant;
    SET @description108 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description108, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'ErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description109 AS sql_variant;
    SET @description109 = N'生成文字增量或完整快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description109, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'Delta';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description110 AS sql_variant;
    SET @description110 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description110, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description111 AS sql_variant;
    SET @description111 = N'事件在同一生成中的遞增序號。';
    EXEC sp_addextendedproperty 'MS_Description', @description111, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'Sequence';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description112 AS sql_variant;
    SET @description112 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description112, 'SCHEMA', N'inference', 'TABLE', N'RunEvents', 'COLUMN', N'RunId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description113 AS sql_variant;
    SET @description113 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description113, 'SCHEMA', N'access', 'TABLE', N'Roles', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description114 AS sql_variant;
    SET @description114 = N'是否啟用；停用不刪除歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description114, 'SCHEMA', N'access', 'TABLE', N'Roles', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description115 AS sql_variant;
    SET @description115 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description115, 'SCHEMA', N'access', 'TABLE', N'Roles', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description116 AS sql_variant;
    SET @description116 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description116, 'SCHEMA', N'access', 'TABLE', N'RoleGroups', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description117 AS sql_variant;
    SET @description117 = N'是否啟用；停用不刪除歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description117, 'SCHEMA', N'access', 'TABLE', N'RoleGroups', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description118 AS sql_variant;
    SET @description118 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description118, 'SCHEMA', N'access', 'TABLE', N'RoleGroups', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description119 AS sql_variant;
    SET @description119 = N'關聯功能群組的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description119, 'SCHEMA', N'access', 'TABLE', N'RoleGroupRoles', 'COLUMN', N'GroupId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description120 AS sql_variant;
    SET @description120 = N'關聯角色的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description120, 'SCHEMA', N'access', 'TABLE', N'RoleGroupRoles', 'COLUMN', N'RoleId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description121 AS sql_variant;
    SET @description121 = N'關聯功能的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description121, 'SCHEMA', N'access', 'TABLE', N'RoleGroupFeatures', 'COLUMN', N'FeatureId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description122 AS sql_variant;
    SET @description122 = N'關聯功能群組的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description122, 'SCHEMA', N'access', 'TABLE', N'RoleGroupFeatures', 'COLUMN', N'GroupId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description123 AS sql_variant;
    SET @description123 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description123, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description124 AS sql_variant;
    SET @description124 = N'父訊息或父資源識別碼，用於分支或階層繼承。';
    EXEC sp_addextendedproperty 'MS_Description', @description124, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'ParentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description125 AS sql_variant;
    SET @description125 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description125, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description126 AS sql_variant;
    SET @description126 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description126, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description127 AS sql_variant;
    SET @description127 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description127, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'Kind';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description128 AS sql_variant;
    SET @description128 = N'是否邏輯刪除；不自動刪除歷史紀錄。';
    EXEC sp_addextendedproperty 'MS_Description', @description128, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'IsDeleted';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description129 AS sql_variant;
    SET @description129 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description129, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description130 AS sql_variant;
    SET @description130 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description130, 'SCHEMA', N'collaboration', 'TABLE', N'Resources', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description131 AS sql_variant;
    SET @description131 = N'訊息角色（user／assistant）或資源成員的閱讀／編輯權限。';
    EXEC sp_addextendedproperty 'MS_Description', @description131, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceMembers', 'COLUMN', N'Role';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description132 AS sql_variant;
    SET @description132 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description132, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceMembers', 'COLUMN', N'UserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description133 AS sql_variant;
    SET @description133 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description133, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceMembers', 'COLUMN', N'ResourceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description134 AS sql_variant;
    SET @description134 = N'關聯功能群組的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description134, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceGroups', 'COLUMN', N'GroupId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description135 AS sql_variant;
    SET @description135 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description135, 'SCHEMA', N'collaboration', 'TABLE', N'ResourceGroups', 'COLUMN', N'ResourceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description136 AS sql_variant;
    SET @description136 = N'引用的附件識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description136, 'SCHEMA', N'attachments', 'TABLE', N'ResourceAttachments', 'COLUMN', N'AttachmentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description137 AS sql_variant;
    SET @description137 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description137, 'SCHEMA', N'attachments', 'TABLE', N'ResourceAttachments', 'COLUMN', N'ResourceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description138 AS sql_variant;
    SET @description138 = N'Gitea repository 的 owner/name 識別。';
    EXEC sp_addextendedproperty 'MS_Description', @description138, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'Repository';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description139 AS sql_variant;
    SET @description139 = N'repository 內的檔案路徑，不是伺服器路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description139, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'Path';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description140 AS sql_variant;
    SET @description140 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description140, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description141 AS sql_variant;
    SET @description141 = N'關聯知識文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description141, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'DocumentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description142 AS sql_variant;
    SET @description142 = N'匯入當時固定的 commit SHA。';
    EXEC sp_addextendedproperty 'MS_Description', @description142, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'Commit';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description143 AS sql_variant;
    SET @description143 = N'關聯知識庫的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description143, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'CollectionId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description144 AS sql_variant;
    SET @description144 = N'Gitea 連線主機位址。';
    EXEC sp_addextendedproperty 'MS_Description', @description144, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'BaseUrl';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description145 AS sql_variant;
    SET @description145 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description145, 'SCHEMA', N'knowledge', 'TABLE', N'RepositoryImports', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description146 AS sql_variant;
    SET @description146 = N'以 ASP.NET Data Protection 保護的外部 token；不可在 API、稽核或日誌回傳。';
    EXEC sp_addextendedproperty 'MS_Description', @description146, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'ProtectedToken';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description147 AS sql_variant;
    SET @description147 = N'外部服務的使用者登入名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description147, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'Login';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description148 AS sql_variant;
    SET @description148 = N'使用者建立外部服務連線的時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description148, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'ConnectedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description149 AS sql_variant;
    SET @description149 = N'Gitea 連線主機位址。';
    EXEC sp_addextendedproperty 'MS_Description', @description149, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'BaseUrl';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description150 AS sql_variant;
    SET @description150 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description150, 'SCHEMA', N'workspace', 'TABLE', N'RepositoryConnections', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description151 AS sql_variant;
    SET @description151 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description151, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description152 AS sql_variant;
    SET @description152 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description152, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'Title';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description153 AS sql_variant;
    SET @description153 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description153, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description154 AS sql_variant;
    SET @description154 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description154, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'Content';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description155 AS sql_variant;
    SET @description155 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description155, 'SCHEMA', N'library', 'TABLE', N'PromptTemplates', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description156 AS sql_variant;
    SET @description156 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description156, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates', 'COLUMN', N'Title';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description157 AS sql_variant;
    SET @description157 = N'關聯專案的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description157, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates', 'COLUMN', N'ProjectId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description158 AS sql_variant;
    SET @description158 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description158, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates', 'COLUMN', N'Content';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description159 AS sql_variant;
    SET @description159 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description159, 'SCHEMA', N'projects', 'TABLE', N'ProjectTemplates', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description160 AS sql_variant;
    SET @description160 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description160, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'Version';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description161 AS sql_variant;
    SET @description161 = N'是否封存對話；封存後不再接受新的生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description161, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'IsArchived';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description162 AS sql_variant;
    SET @description162 = N'專案共用指令。';
    EXEC sp_addextendedproperty 'MS_Description', @description162, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'Instructions';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description163 AS sql_variant;
    SET @description163 = N'業務物件的用途說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description163, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'Description';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description164 AS sql_variant;
    SET @description164 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description164, 'SCHEMA', N'projects', 'TABLE', N'Projects', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description165 AS sql_variant;
    SET @description165 = N'模型是否會回報實際用量。';
    EXEC sp_addextendedproperty 'MS_Description', @description165, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'SupportsUsage';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description166 AS sql_variant;
    SET @description166 = N'模型是否支援串流輸出。';
    EXEC sp_addextendedproperty 'MS_Description', @description166, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'SupportsStreaming';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description167 AS sql_variant;
    SET @description167 = N'模型核准的最大輸出 tokens。';
    EXEC sp_addextendedproperty 'MS_Description', @description167, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'MaxOutputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description168 AS sql_variant;
    SET @description168 = N'使用者或模型的介面顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description168, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'DisplayName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description169 AS sql_variant;
    SET @description169 = N'模型上下文容量，以 tokens 計。';
    EXEC sp_addextendedproperty 'MS_Description', @description169, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'ContextTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description170 AS sql_variant;
    SET @description170 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description170, 'SCHEMA', N'inference', 'TABLE', N'ModelProfiles', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description171 AS sql_variant;
    SET @description171 = N'搜尋服務每次請求的費用。';
    EXEC sp_addextendedproperty 'MS_Description', @description171, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'RequestCharge';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description172 AS sql_variant;
    SET @description172 = N'模型或搜尋服務供應商識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description172, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Provider';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description173 AS sql_variant;
    SET @description173 = N'每次呼叫的固定單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description173, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'PerRequest';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description174 AS sql_variant;
    SET @description174 = N'每百萬輸出 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description174, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'OutputPerMillion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description175 AS sql_variant;
    SET @description175 = N'使用者提供的補充說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description175, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Note';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description176 AS sql_variant;
    SET @description176 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description176, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'ModelId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description177 AS sql_variant;
    SET @description177 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description177, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Kind';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description178 AS sql_variant;
    SET @description178 = N'每百萬輸入 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description178, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'InputPerMillion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description179 AS sql_variant;
    SET @description179 = N'此價格版本開始生效的時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description179, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'EffectiveAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description180 AS sql_variant;
    SET @description180 = N'費用幣別代碼；不同幣別不可直接合計。';
    EXEC sp_addextendedproperty 'MS_Description', @description180, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Currency';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description181 AS sql_variant;
    SET @description181 = N'建立紀錄的使用者識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description181, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'CreatedBy';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description182 AS sql_variant;
    SET @description182 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description182, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description183 AS sql_variant;
    SET @description183 = N'每百萬快取輸入 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description183, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'CachedInputPerMillion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description184 AS sql_variant;
    SET @description184 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description184, 'SCHEMA', N'inference', 'TABLE', N'ModelPrices', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description185 AS sql_variant;
    SET @description185 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description185, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'Status';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description186 AS sql_variant;
    SET @description186 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description186, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description187 AS sql_variant;
    SET @description187 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description187, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'OutputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description188 AS sql_variant;
    SET @description188 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description188, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'ModelId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description189 AS sql_variant;
    SET @description189 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description189, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'Kind';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description190 AS sql_variant;
    SET @description190 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description190, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'InputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description191 AS sql_variant;
    SET @description191 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description191, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description192 AS sql_variant;
    SET @description192 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description192, 'SCHEMA', N'inference', 'TABLE', N'ModelInvocations', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description193 AS sql_variant;
    SET @description193 = N'本次呼叫是否有完整且可計費的實際用量。';
    EXEC sp_addextendedproperty 'MS_Description', @description193, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'UsageComplete';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description194 AS sql_variant;
    SET @description194 = N'業務物件的生命週期狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description194, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'State';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description195 AS sql_variant;
    SET @description195 = N'工作開始執行時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description195, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'StartedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description196 AS sql_variant;
    SET @description196 = N'搜尋服務每次請求的費用。';
    EXEC sp_addextendedproperty 'MS_Description', @description196, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'RequestCharge';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description197 AS sql_variant;
    SET @description197 = N'模型回報的推理 tokens。';
    EXEC sp_addextendedproperty 'MS_Description', @description197, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'ReasoningTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description198 AS sql_variant;
    SET @description198 = N'模型或搜尋服務供應商識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description198, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Provider';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description199 AS sql_variant;
    SET @description199 = N'此呼叫採用的不可變價格版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description199, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'PriceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description200 AS sql_variant;
    SET @description200 = N'每次呼叫的固定單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description200, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'PerRequest';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description201 AS sql_variant;
    SET @description201 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description201, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description202 AS sql_variant;
    SET @description202 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description202, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'OutputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description203 AS sql_variant;
    SET @description203 = N'每百萬輸出 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description203, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'OutputPerMillion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description204 AS sql_variant;
    SET @description204 = N'搜尋或處理操作的結果分類。';
    EXEC sp_addextendedproperty 'MS_Description', @description204, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Outcome';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description205 AS sql_variant;
    SET @description205 = N'模型呼叫或背景工作的操作類型。';
    EXEC sp_addextendedproperty 'MS_Description', @description205, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Operation';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description206 AS sql_variant;
    SET @description206 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description206, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'ModelId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description207 AS sql_variant;
    SET @description207 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description207, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Kind';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description208 AS sql_variant;
    SET @description208 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description208, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'InputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description209 AS sql_variant;
    SET @description209 = N'每百萬輸入 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description209, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'InputPerMillion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description210 AS sql_variant;
    SET @description210 = N'工作結束時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description210, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'FinishedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description211 AS sql_variant;
    SET @description211 = N'費用幣別代碼；不同幣別不可直接合計。';
    EXEC sp_addextendedproperty 'MS_Description', @description211, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Currency';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description212 AS sql_variant;
    SET @description212 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description212, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description213 AS sql_variant;
    SET @description213 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description213, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'ConversationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description214 AS sql_variant;
    SET @description214 = N'模型回報的快取輸入 tokens。';
    EXEC sp_addextendedproperty 'MS_Description', @description214, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'CachedInputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description215 AS sql_variant;
    SET @description215 = N'每百萬快取輸入 tokens 的單價。';
    EXEC sp_addextendedproperty 'MS_Description', @description215, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'CachedInputPerMillion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description216 AS sql_variant;
    SET @description216 = N'此呼叫的已知費用；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description216, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Amount';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description217 AS sql_variant;
    SET @description217 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description217, 'SCHEMA', N'inference', 'TABLE', N'ModelCharges', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description218 AS sql_variant;
    SET @description218 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description218, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'Status';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description219 AS sql_variant;
    SET @description219 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description219, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'RunId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description220 AS sql_variant;
    SET @description220 = N'訊息角色（user／assistant）或資源成員的閱讀／編輯權限。';
    EXEC sp_addextendedproperty 'MS_Description', @description220, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'Role';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description221 AS sql_variant;
    SET @description221 = N'父訊息或父資源識別碼，用於分支或階層繼承。';
    EXEC sp_addextendedproperty 'MS_Description', @description221, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'ParentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description222 AS sql_variant;
    SET @description222 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description222, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'ModelId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description223 AS sql_variant;
    SET @description223 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description223, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'ErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description224 AS sql_variant;
    SET @description224 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description224, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description225 AS sql_variant;
    SET @description225 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description225, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'ConversationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description226 AS sql_variant;
    SET @description226 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description226, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'Content';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description227 AS sql_variant;
    SET @description227 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description227, 'SCHEMA', N'conversations', 'TABLE', N'Messages', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description228 AS sql_variant;
    SET @description228 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description228, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description229 AS sql_variant;
    SET @description229 = N'回饋或操作理由。';
    EXEC sp_addextendedproperty 'MS_Description', @description229, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'Reason';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description230 AS sql_variant;
    SET @description230 = N'使用者對回答的評分。';
    EXEC sp_addextendedproperty 'MS_Description', @description230, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'Rating';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description231 AS sql_variant;
    SET @description231 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description231, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description232 AS sql_variant;
    SET @description232 = N'使用者提供的補充說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description232, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'Note';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description233 AS sql_variant;
    SET @description233 = N'關聯訊息的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description233, 'SCHEMA', N'quality', 'TABLE', N'MessageFeedback', 'COLUMN', N'MessageId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description234 AS sql_variant;
    SET @description234 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description234, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'Title';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description235 AS sql_variant;
    SET @description235 = N'文件頁碼，從 1 開始。';
    EXEC sp_addextendedproperty 'MS_Description', @description235, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'PageNumber';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description236 AS sql_variant;
    SET @description236 = N'檢索或引用時保存的文字摘要。';
    EXEC sp_addextendedproperty 'MS_Description', @description236, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'Excerpt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description237 AS sql_variant;
    SET @description237 = N'關聯知識文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description237, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'DocumentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description238 AS sql_variant;
    SET @description238 = N'回答引用的順序編號。';
    EXEC sp_addextendedproperty 'MS_Description', @description238, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'Number';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description239 AS sql_variant;
    SET @description239 = N'關聯訊息的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description239, 'SCHEMA', N'knowledge', 'TABLE', N'MessageCitations', 'COLUMN', N'MessageId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description240 AS sql_variant;
    SET @description240 = N'引用的附件識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description240, 'SCHEMA', N'attachments', 'TABLE', N'MessageAttachments', 'COLUMN', N'AttachmentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description241 AS sql_variant;
    SET @description241 = N'關聯訊息的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description241, 'SCHEMA', N'attachments', 'TABLE', N'MessageAttachments', 'COLUMN', N'MessageId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description242 AS sql_variant;
    SET @description242 = N'個人附件儲存上限，以 bytes 計；群組限制取最低值。';
    EXEC sp_addextendedproperty 'MS_Description', @description242, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'StoredAttachmentLimitBytes';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description243 AS sql_variant;
    SET @description243 = N'每日生成次數上限；群組限制取最低值，UTC 午夜重設。';
    EXEC sp_addextendedproperty 'MS_Description', @description243, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'DailyRequestLimit';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description244 AS sql_variant;
    SET @description244 = N'模型白名單 JSON；空值不增加限制，空陣列禁止生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description244, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'AllowedModelsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description245 AS sql_variant;
    SET @description245 = N'關聯功能群組的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description245, 'SCHEMA', N'access', 'TABLE', N'GroupModelPolicies', 'COLUMN', N'GroupId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description246 AS sql_variant;
    SET @description246 = N'此次生成的使用者提問訊息識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description246, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'UserMessageId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description247 AS sql_variant;
    SET @description247 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description247, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'Status';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description248 AS sql_variant;
    SET @description248 = N'工作開始執行時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description248, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'StartedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description249 AS sql_variant;
    SET @description249 = N'請求內容指紋，用於辨識冪等識別碼衝突。';
    EXEC sp_addextendedproperty 'MS_Description', @description249, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'RequestHash';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description250 AS sql_variant;
    SET @description250 = N'執行參數的 JSON 快照，不含服務密鑰。';
    EXEC sp_addextendedproperty 'MS_Description', @description250, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ParametersJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description251 AS sql_variant;
    SET @description251 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description251, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description252 AS sql_variant;
    SET @description252 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description252, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'OutputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description253 AS sql_variant;
    SET @description253 = N'核准模型的內部識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description253, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ModelId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description254 AS sql_variant;
    SET @description254 = N'生成 executor 租約的到期時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description254, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'LeaseExpiresAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description255 AS sql_variant;
    SET @description255 = N'最後已持久化的生成事件序號。';
    EXEC sp_addextendedproperty 'MS_Description', @description255, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'LastSequence';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description256 AS sql_variant;
    SET @description256 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description256, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'InputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description257 AS sql_variant;
    SET @description257 = N'擁有者範圍內的冪等請求識別，避免重試重複處理。';
    EXEC sp_addextendedproperty 'MS_Description', @description257, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'IdempotencyKey';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description258 AS sql_variant;
    SET @description258 = N'工作結束時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description258, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'FinishedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description259 AS sql_variant;
    SET @description259 = N'處理此次生成的伺服器程序識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description259, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ExecutorId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description260 AS sql_variant;
    SET @description260 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description260, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description261 AS sql_variant;
    SET @description261 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description261, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description262 AS sql_variant;
    SET @description262 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description262, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ConversationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description263 AS sql_variant;
    SET @description263 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description263, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'Content';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description264 AS sql_variant;
    SET @description264 = N'此次生成的 AI 回答訊息識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description264, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'AssistantMessageId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description265 AS sql_variant;
    SET @description265 = N'仍在執行的擁有者；filtered unique index 限制每人一個生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description265, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'ActiveOwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description266 AS sql_variant;
    SET @description266 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description266, 'SCHEMA', N'inference', 'TABLE', N'GenerationRuns', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description267 AS sql_variant;
    SET @description267 = N'介面顯示順序；數值越小越前。';
    EXEC sp_addextendedproperty 'MS_Description', @description267, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'SortOrder';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description268 AS sql_variant;
    SET @description268 = N'功能入口的本站路由；API 授權仍由後端政策判定。';
    EXEC sp_addextendedproperty 'MS_Description', @description268, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'Route';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description269 AS sql_variant;
    SET @description269 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description269, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description270 AS sql_variant;
    SET @description270 = N'是否啟用；停用不刪除歷史資料。';
    EXEC sp_addextendedproperty 'MS_Description', @description270, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'Enabled';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description271 AS sql_variant;
    SET @description271 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description271, 'SCHEMA', N'access', 'TABLE', N'Features', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description272 AS sql_variant;
    SET @description272 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description272, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets', 'COLUMN', N'Version';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description273 AS sql_variant;
    SET @description273 = N'業務物件的用途說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description273, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets', 'COLUMN', N'Description';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description274 AS sql_variant;
    SET @description274 = N'固定評測案例的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description274, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets', 'COLUMN', N'CasesJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description275 AS sql_variant;
    SET @description275 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description275, 'SCHEMA', N'quality', 'TABLE', N'EvaluationSets', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description276 AS sql_variant;
    SET @description276 = N'評測模型／參數組合的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description276, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'VariantsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description277 AS sql_variant;
    SET @description277 = N'評測執行當時的題庫版本。';
    EXEC sp_addextendedproperty 'MS_Description', @description277, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'SetVersion';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description278 AS sql_variant;
    SET @description278 = N'評測執行當時的題庫名稱快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description278, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'SetTitle';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description279 AS sql_variant;
    SET @description279 = N'評測題庫識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description279, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'SetId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description280 AS sql_variant;
    SET @description280 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description280, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description281 AS sql_variant;
    SET @description281 = N'關聯背景工作識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description281, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'JobId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description282 AS sql_variant;
    SET @description282 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description282, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description283 AS sql_variant;
    SET @description283 = N'固定評測案例的 JSON 快照。';
    EXEC sp_addextendedproperty 'MS_Description', @description283, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'CasesJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description284 AS sql_variant;
    SET @description284 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description284, 'SCHEMA', N'quality', 'TABLE', N'EvaluationRuns', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description285 AS sql_variant;
    SET @description285 = N'評測輸出是否因上限截斷。';
    EXEC sp_addextendedproperty 'MS_Description', @description285, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'Truncated';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description286 AS sql_variant;
    SET @description286 = N'人工覆核者的使用者識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description286, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ReviewerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description287 AS sql_variant;
    SET @description287 = N'人工覆核分數；未覆核保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description287, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ReviewScore';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description288 AS sql_variant;
    SET @description288 = N'人工覆核意見。';
    EXEC sp_addextendedproperty 'MS_Description', @description288, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ReviewNote';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description289 AS sql_variant;
    SET @description289 = N'必要條件總數。';
    EXEC sp_addextendedproperty 'MS_Description', @description289, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'RequiredTotal';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description290 AS sql_variant;
    SET @description290 = N'命中的必要條件數。';
    EXEC sp_addextendedproperty 'MS_Description', @description290, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'RequiredMatches';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description291 AS sql_variant;
    SET @description291 = N'模型回報的輸出 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description291, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'OutputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description292 AS sql_variant;
    SET @description292 = N'評測模型的實際回答。';
    EXEC sp_addextendedproperty 'MS_Description', @description292, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'Output';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description293 AS sql_variant;
    SET @description293 = N'模型回報的輸入 tokens；未知保持空值。';
    EXEC sp_addextendedproperty 'MS_Description', @description293, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'InputTokens';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description294 AS sql_variant;
    SET @description294 = N'命中的禁止條件數。';
    EXEC sp_addextendedproperty 'MS_Description', @description294, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ForbiddenMatches';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description295 AS sql_variant;
    SET @description295 = N'執行耗時，以毫秒計。';
    EXEC sp_addextendedproperty 'MS_Description', @description295, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'ElapsedMs';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description296 AS sql_variant;
    SET @description296 = N'評測模型／參數組合的從零開始索引。';
    EXEC sp_addextendedproperty 'MS_Description', @description296, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'VariantIndex';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description297 AS sql_variant;
    SET @description297 = N'評測案例的從零開始索引。';
    EXEC sp_addextendedproperty 'MS_Description', @description297, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'CaseIndex';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description298 AS sql_variant;
    SET @description298 = N'關聯生成或評測執行的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description298, 'SCHEMA', N'quality', 'TABLE', N'EvaluationResults', 'COLUMN', N'RunId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description299 AS sql_variant;
    SET @description299 = N'處理過程中的非致命提示。';
    EXEC sp_addextendedproperty 'MS_Description', @description299, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'Warning';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description300 AS sql_variant;
    SET @description300 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description300, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'Status';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description301 AS sql_variant;
    SET @description301 = N'文件總頁數。';
    EXEC sp_addextendedproperty 'MS_Description', @description301, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'PageCount';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description302 AS sql_variant;
    SET @description302 = N'關聯背景工作識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description302, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'JobId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description303 AS sql_variant;
    SET @description303 = N'是否邏輯刪除；不自動刪除歷史紀錄。';
    EXEC sp_addextendedproperty 'MS_Description', @description303, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'IsDeleted';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description304 AS sql_variant;
    SET @description304 = N'原始附件檔名，不作為伺服器儲存路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description304, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'FileName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description305 AS sql_variant;
    SET @description305 = N'向量模型、維度與前處理的版本指紋；不混用不同 profile。';
    EXEC sp_addextendedproperty 'MS_Description', @description305, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'EmbeddingProfile';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description306 AS sql_variant;
    SET @description306 = N'核准的 MIME 型別。';
    EXEC sp_addextendedproperty 'MS_Description', @description306, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'ContentType';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description307 AS sql_variant;
    SET @description307 = N'關聯知識庫的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description307, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'CollectionId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description308 AS sql_variant;
    SET @description308 = N'文件已建立的檢索片段數。';
    EXEC sp_addextendedproperty 'MS_Description', @description308, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'ChunkCount';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description309 AS sql_variant;
    SET @description309 = N'引用的附件識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description309, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'AttachmentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description310 AS sql_variant;
    SET @description310 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description310, 'SCHEMA', N'knowledge', 'TABLE', N'Documents', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description311 AS sql_variant;
    SET @description311 = N'文件頁面／片段的擷取文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description311, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'Text';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description312 AS sql_variant;
    SET @description312 = N'此評測結果是否需要人工覆核。';
    EXEC sp_addextendedproperty 'MS_Description', @description312, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'NeedsReview';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description313 AS sql_variant;
    SET @description313 = N'附件文字擷取方法或結果。';
    EXEC sp_addextendedproperty 'MS_Description', @description313, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'Extraction';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description314 AS sql_variant;
    SET @description314 = N'文件頁碼，從 1 開始。';
    EXEC sp_addextendedproperty 'MS_Description', @description314, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'PageNumber';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description315 AS sql_variant;
    SET @description315 = N'關聯知識文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description315, 'SCHEMA', N'knowledge', 'TABLE', N'DocumentPages', 'COLUMN', N'DocumentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description316 AS sql_variant;
    SET @description316 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description316, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description317 AS sql_variant;
    SET @description317 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description317, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'Title';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description318 AS sql_variant;
    SET @description318 = N'對話專用的回答指令。';
    EXEC sp_addextendedproperty 'MS_Description', @description318, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'SystemInstruction';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description319 AS sql_variant;
    SET @description319 = N'關聯專案的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description319, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'ProjectId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description320 AS sql_variant;
    SET @description320 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description320, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description321 AS sql_variant;
    SET @description321 = N'是否標記收藏。';
    EXEC sp_addextendedproperty 'MS_Description', @description321, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'IsFavorite';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description322 AS sql_variant;
    SET @description322 = N'是否邏輯刪除；不自動刪除歷史紀錄。';
    EXEC sp_addextendedproperty 'MS_Description', @description322, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'IsDeleted';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description323 AS sql_variant;
    SET @description323 = N'是否封存對話；封存後不再接受新的生成。';
    EXEC sp_addextendedproperty 'MS_Description', @description323, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'IsArchived';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description324 AS sql_variant;
    SET @description324 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description324, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description325 AS sql_variant;
    SET @description325 = N'對話目前顯示分支的最後訊息識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description325, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'ActiveLeafId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description326 AS sql_variant;
    SET @description326 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description326, 'SCHEMA', N'conversations', 'TABLE', N'Conversations', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description327 AS sql_variant;
    SET @description327 = N'業務物件的顯示名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description327, 'SCHEMA', N'conversations', 'TABLE', N'ConversationLabels', 'COLUMN', N'Name';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description328 AS sql_variant;
    SET @description328 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description328, 'SCHEMA', N'conversations', 'TABLE', N'ConversationLabels', 'COLUMN', N'ConversationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description329 AS sql_variant;
    SET @description329 = N'關聯知識庫的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description329, 'SCHEMA', N'knowledge', 'TABLE', N'ConversationCollections', 'COLUMN', N'CollectionId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description330 AS sql_variant;
    SET @description330 = N'關聯對話的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description330, 'SCHEMA', N'knowledge', 'TABLE', N'ConversationCollections', 'COLUMN', N'ConversationId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description331 AS sql_variant;
    SET @description331 = N'業務物件的用途說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description331, 'SCHEMA', N'knowledge', 'TABLE', N'Collections', 'COLUMN', N'Description';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description332 AS sql_variant;
    SET @description332 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description332, 'SCHEMA', N'knowledge', 'TABLE', N'Collections', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description333 AS sql_variant;
    SET @description333 = N'文件頁面／片段的擷取文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description333, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'Text';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description334 AS sql_variant;
    SET @description334 = N'文件頁碼，從 1 開始。';
    EXEC sp_addextendedproperty 'MS_Description', @description334, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'PageNumber';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description335 AS sql_variant;
    SET @description335 = N'同一父物件內的呈現順序。';
    EXEC sp_addextendedproperty 'MS_Description', @description335, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'Ordinal';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description336 AS sql_variant;
    SET @description336 = N'向量模型、維度與前處理的版本指紋；不混用不同 profile。';
    EXEC sp_addextendedproperty 'MS_Description', @description336, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'EmbeddingProfile';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description337 AS sql_variant;
    SET @description337 = N'正規化向量的 JSON 表示；與 embedding profile 一起判斷相容性。';
    EXEC sp_addextendedproperty 'MS_Description', @description337, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'EmbeddingJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description338 AS sql_variant;
    SET @description338 = N'關聯知識文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description338, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'DocumentId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description339 AS sql_variant;
    SET @description339 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description339, 'SCHEMA', N'knowledge', 'TABLE', N'Chunks', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description340 AS sql_variant;
    SET @description340 = N'資料最後修改時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description340, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'UpdatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description341 AS sql_variant;
    SET @description341 = N'已知的總工作單位數；未知不表示百分比。';
    EXEC sp_addextendedproperty 'MS_Description', @description341, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'TotalUnits';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description342 AS sql_variant;
    SET @description342 = N'操作所關聯的業務對象識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description342, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'SubjectId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description343 AS sql_variant;
    SET @description343 = N'業務執行狀態。';
    EXEC sp_addextendedproperty 'MS_Description', @description343, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Status';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description344 AS sql_variant;
    SET @description344 = N'背景工作目前階段。';
    EXEC sp_addextendedproperty 'MS_Description', @description344, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Stage';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description345 AS sql_variant;
    SET @description345 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description345, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ResourceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description346 AS sql_variant;
    SET @description346 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description346, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description347 AS sql_variant;
    SET @description347 = N'背景工作租約的到期時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description347, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'LeaseUntil';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description348 AS sql_variant;
    SET @description348 = N'背景工作租約的 fencing token，防止過期 worker 提交。';
    EXEC sp_addextendedproperty 'MS_Description', @description348, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'LeaseToken';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description349 AS sql_variant;
    SET @description349 = N'分類標籤或階段的顯示文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description349, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Label';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description350 AS sql_variant;
    SET @description350 = N'業務操作、資源或成本的種類。';
    EXEC sp_addextendedproperty 'MS_Description', @description350, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Kind';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description351 AS sql_variant;
    SET @description351 = N'經限制的錯誤說明。';
    EXEC sp_addextendedproperty 'MS_Description', @description351, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ErrorMessage';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description352 AS sql_variant;
    SET @description352 = N'對外安全的錯誤代碼，不含密碼或完整例外。';
    EXEC sp_addextendedproperty 'MS_Description', @description352, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ErrorCode';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description353 AS sql_variant;
    SET @description353 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description353, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description354 AS sql_variant;
    SET @description354 = N'已完成的真實工作單位數。';
    EXEC sp_addextendedproperty 'MS_Description', @description354, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'CompletedUnits';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description355 AS sql_variant;
    SET @description355 = N'是否收到取消要求；不表示工作已停止。';
    EXEC sp_addextendedproperty 'MS_Description', @description355, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'CancelRequested';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description356 AS sql_variant;
    SET @description356 = N'背景工作執行／重試次數。';
    EXEC sp_addextendedproperty 'MS_Description', @description356, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Attempt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description357 AS sql_variant;
    SET @description357 = N'仍在執行工作的唯一鍵，避免同一業務重複排程。';
    EXEC sp_addextendedproperty 'MS_Description', @description357, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'ActiveKey';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description358 AS sql_variant;
    SET @description358 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description358, 'SCHEMA', N'operations', 'TABLE', N'BackgroundJobs', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description359 AS sql_variant;
    SET @description359 = N'稽核操作結果或失敗代碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description359, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'Result';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description360 AS sql_variant;
    SET @description360 = N'關聯或稽核對象的業務資源識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description360, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'ResourceId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description361 AS sql_variant;
    SET @description361 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description361, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description362 AS sql_variant;
    SET @description362 = N'稽核前後狀態或操作範圍 JSON；不含密碼、hash、token 或對話內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description362, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'DetailsJson';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description363 AS sql_variant;
    SET @description363 = N'稽核事件發生時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description363, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'At';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description364 AS sql_variant;
    SET @description364 = N'稽核操作名稱。';
    EXEC sp_addextendedproperty 'MS_Description', @description364, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'Action';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description365 AS sql_variant;
    SET @description365 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description365, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    ALTER TABLE [operations].[AuditEvents] ADD [ActorId] uniqueidentifier NULL;
    DECLARE @description366 AS sql_variant;
    SET @description366 = N'身分測試時實際發起操作的管理者；空值表示與 OwnerId 相同。';
    EXEC sp_addextendedproperty 'MS_Description', @description366, 'SCHEMA', N'operations', 'TABLE', N'AuditEvents', 'COLUMN', N'ActorId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description367 AS sql_variant;
    SET @description367 = N'原始附件大小，以 bytes 計。';
    EXEC sp_addextendedproperty 'MS_Description', @description367, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'Size';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description368 AS sql_variant;
    SET @description368 = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。';
    EXEC sp_addextendedproperty 'MS_Description', @description368, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'OwnerId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description369 AS sql_variant;
    SET @description369 = N'原始附件檔名，不作為伺服器儲存路徑。';
    EXEC sp_addextendedproperty 'MS_Description', @description369, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'FileName';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description370 AS sql_variant;
    SET @description370 = N'附件分析後的文字。';
    EXEC sp_addextendedproperty 'MS_Description', @description370, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'ExtractedText';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description371 AS sql_variant;
    SET @description371 = N'原始附件二進位內容；不在 wwwroot 公開。';
    EXEC sp_addextendedproperty 'MS_Description', @description371, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'Data';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description372 AS sql_variant;
    SET @description372 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description372, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description373 AS sql_variant;
    SET @description373 = N'核准的 MIME 型別。';
    EXEC sp_addextendedproperty 'MS_Description', @description373, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'ContentType';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description374 AS sql_variant;
    SET @description374 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description374, 'SCHEMA', N'attachments', 'TABLE', N'Attachments', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description375 AS sql_variant;
    SET @description375 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description375, 'SCHEMA', N'content', 'TABLE', N'Artifacts', 'COLUMN', N'Version';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description376 AS sql_variant;
    SET @description376 = N'此成果版本所引用的來源訊息識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description376, 'SCHEMA', N'content', 'TABLE', N'Artifacts', 'COLUMN', N'SourceMessageId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description377 AS sql_variant;
    SET @description377 = N'關聯專案的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description377, 'SCHEMA', N'content', 'TABLE', N'Artifacts', 'COLUMN', N'ProjectId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description378 AS sql_variant;
    SET @description378 = N'資料的主鍵識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description378, 'SCHEMA', N'content', 'TABLE', N'Artifacts', 'COLUMN', N'Id';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description379 AS sql_variant;
    SET @description379 = N'介面顯示標題。';
    EXEC sp_addextendedproperty 'MS_Description', @description379, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'Title';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description380 AS sql_variant;
    SET @description380 = N'資料建立時間，採 UTC offset。';
    EXEC sp_addextendedproperty 'MS_Description', @description380, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'CreatedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description381 AS sql_variant;
    SET @description381 = N'訊息、版本或生成的文字內容。';
    EXEC sp_addextendedproperty 'MS_Description', @description381, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'Content';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description382 AS sql_variant;
    SET @description382 = N'建立此版本的使用者識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description382, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'AuthorId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description383 AS sql_variant;
    SET @description383 = N'業務版本號，用於歷史或樂觀並行控制。';
    EXEC sp_addextendedproperty 'MS_Description', @description383, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'Version';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description384 AS sql_variant;
    SET @description384 = N'關聯成果文件的識別碼。';
    EXEC sp_addextendedproperty 'MS_Description', @description384, 'SCHEMA', N'content', 'TABLE', N'ArtifactRevisions', 'COLUMN', N'ArtifactId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description385 AS sql_variant;
    SET @description385 = N'角色或資源授權建立時間。';
    EXEC sp_addextendedproperty 'MS_Description', @description385, 'SCHEMA', N'access', 'TABLE', N'AdministratorBootstraps', 'COLUMN', N'GrantedAt';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    DECLARE @description386 AS sql_variant;
    SET @description386 = N'關聯使用者的 Users 主鍵。';
    EXEC sp_addextendedproperty 'MS_Description', @description386, 'SCHEMA', N'access', 'TABLE', N'AdministratorBootstraps', 'COLUMN', N'UserId';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    UPDATE [access].[RoleGroups] SET [Name] = N'基本工作區' WHERE [Id] = N'workspace' AND [Name] = N'基本工作台'
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_AdAccount] ON [identity].[Users] ([AdAccount]) WHERE [AdAccount] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_LocalAccount] ON [identity].[Users] ([LocalAccount]) WHERE [LocalAccount] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_ActorId_Id] ON [operations].[AuditEvents] ([ActorId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
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
     (N'attachments', N'原始附件與引用關聯。'),
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
    WHERE [MigrationId] = N'20261005082244_ManagedIdentitiesAndDescriptions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005082244_ManagedIdentitiesAndDescriptions', N'10.0.12');
END;

COMMIT;
GO

