CREATE TABLE [audit].[AuditEvents] (
    [Id]            BIGINT             IDENTITY (1, 1) NOT NULL,
    [OwnerId]       UNIQUEIDENTIFIER   NOT NULL,
    [ActorId]       UNIQUEIDENTIFIER   NULL,
    [TraceId]       NVARCHAR (32)      NULL,
    [OperationId]   UNIQUEIDENTIFIER   NULL,
    [IssueCode]     NVARCHAR (40)      NULL,
    [Action]        NVARCHAR (64)      NOT NULL,
    [ResourceId]    UNIQUEIDENTIFIER   NULL,
    [Result]        NVARCHAR (80)      NOT NULL,
    [DetailsJson]   NVARCHAR (MAX)     NULL,
    [At]            DATETIMEOFFSET (7) NOT NULL,
    [Category]      NVARCHAR (16)      NOT NULL,
    [FeatureId]     NVARCHAR (64)      NULL,
    [SubjectUserId] UNIQUEIDENTIFIER   NULL,
    [ClientAddress] NVARCHAR (45)      NULL,
    CONSTRAINT [PK_AuditEvents] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_AuditEvents_Category] CHECK ([Category]=N'authentication' OR [Category]=N'administration' OR [Category]=N'access' OR [Category]=N'activity')
);


GO

CREATE NONCLUSTERED INDEX [IX_AuditEvents_Action_Id]
    ON [audit].[AuditEvents]([Action] ASC, [Id] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_AuditEvents_ActorId_Id]
    ON [audit].[AuditEvents]([ActorId] ASC, [Id] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_AuditEvents_At]
    ON [audit].[AuditEvents]([At] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_AuditEvents_ResourceId_Id]
    ON [audit].[AuditEvents]([ResourceId] ASC, [Id] ASC);


GO

CREATE NONCLUSTERED INDEX [ix_AuditEvents_Category_Id]
    ON [audit].[AuditEvents]([Category] ASC, [Id] ASC);


GO

CREATE NONCLUSTERED INDEX [ix_AuditEvents_FeatureId_At]
    ON [audit].[AuditEvents]([FeatureId] ASC, [At] ASC)
    INCLUDE([OwnerId], [ActorId], [Result]) WHERE ([FeatureId] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_AuditEvents_OwnerId_Id]
    ON [audit].[AuditEvents]([OwnerId] ASC, [Id] ASC);


GO

CREATE NONCLUSTERED INDEX [ix_AuditEvents_SubjectUserId_Id]
    ON [audit].[AuditEvents]([SubjectUserId] ASC, [Id] ASC) WHERE ([SubjectUserId] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'操作與管理異動稽核；保存實際管理者及有效身分，不記錄密碼或私密內容。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核動作名稱 <module>.<object>.<verb>（ADR 0023）。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'Action';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'身分測試時實際發起操作的管理者；空值表示與 OwnerId 相同。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'ActorId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核事件發生時間。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'At';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核分類（authentication、administration、access、activity），由寫入時的動作宣告決定。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'Category';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'請求的來源 IP（IPv4 對應位址轉成 IPv4）；背景工作為空值。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'ClientAddress';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核前後狀態或操作範圍 JSON；不含密碼、hash、token 或對話內容。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'DetailsJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'通過授權的功能 ID（最具體的那一個）；功能使用量依此統計，系統動作為空值。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'FeatureId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持久作業識別，跨佇列與重試保持不變。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'OperationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核對象的業務資源識別碼。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'ResourceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結果：成功為 completed，失敗為錯誤代碼。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'Result';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料主體的 Users 主鍵：被查閱或被修改資料的那個人；與操作者相同或不屬於特定人時為空值。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'SubjectUserId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'W3C 流程追蹤識別，僅由伺服器建立。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'TraceId';


GO

