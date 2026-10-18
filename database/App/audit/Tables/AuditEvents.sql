CREATE TABLE [audit].[AuditEvents] (
    [Id]          BIGINT             IDENTITY (1, 1) NOT NULL,
    [OwnerId]     UNIQUEIDENTIFIER   NOT NULL,
    [ActorId]     UNIQUEIDENTIFIER   NULL,
    [TraceId]     NVARCHAR (32)      NULL,
    [OperationId] UNIQUEIDENTIFIER   NULL,
    [IssueCode]   NVARCHAR (40)      NULL,
    [Action]      NVARCHAR (64)      NOT NULL,
    [ResourceId]  UNIQUEIDENTIFIER   NULL,
    [Result]      NVARCHAR (80)      NULL,
    [DetailsJson] NVARCHAR (MAX)     NULL,
    [At]          DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_AuditEvents] PRIMARY KEY CLUSTERED ([Id] ASC)
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

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'操作與管理異動稽核；保存實際管理者及有效身分，不記錄密碼或私密內容。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核操作名稱。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'Action';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'身分測試時實際發起操作的管理者；空值表示與 OwnerId 相同。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'ActorId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核事件發生時間。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'At';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核前後狀態或操作範圍 JSON；不含密碼、hash、token 或對話內容。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'DetailsJson';


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

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'稽核操作結果或失敗代碼。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'Result';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'W3C 流程追蹤識別，僅由伺服器建立。', @level0type = N'SCHEMA', @level0name = N'audit', @level1type = N'TABLE', @level1name = N'AuditEvents', @level2type = N'COLUMN', @level2name = N'TraceId';


GO

