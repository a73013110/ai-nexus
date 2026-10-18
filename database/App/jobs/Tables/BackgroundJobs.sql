CREATE TABLE [jobs].[BackgroundJobs] (
    [Id]              UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]         UNIQUEIDENTIFIER   NOT NULL,
    [TraceId]         NVARCHAR (32)      NULL,
    [ParentSpanId]    NVARCHAR (16)      NULL,
    [OperationId]     UNIQUEIDENTIFIER   NOT NULL,
    [ResourceId]      UNIQUEIDENTIFIER   NULL,
    [SubjectId]       UNIQUEIDENTIFIER   NOT NULL,
    [Kind]            NVARCHAR (32)      NOT NULL,
    [Label]           NVARCHAR (180)     NOT NULL,
    [Status]          NVARCHAR (16)      NOT NULL,
    [Stage]           NVARCHAR (120)     NOT NULL,
    [ActiveKey]       NVARCHAR (100)     NULL,
    [LeaseToken]      UNIQUEIDENTIFIER   NULL,
    [LeaseUntil]      DATETIMEOFFSET (7) NULL,
    [CancelRequested] BIT                NOT NULL,
    [Attempt]         INT                NOT NULL,
    [CompletedUnits]  INT                NOT NULL,
    [TotalUnits]      INT                NULL,
    [IssueCode]       NVARCHAR (40)      NULL,
    [ErrorCode]       NVARCHAR (80)      NULL,
    [ErrorMessage]    NVARCHAR (240)     NULL,
    [CreatedAt]       DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]       DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_BackgroundJobs] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_BackgroundJobs_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [collaboration].[Resources] ([Id]),
    CONSTRAINT [FK_BackgroundJobs_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_BackgroundJobs_OwnerId_CreatedAt]
    ON [jobs].[BackgroundJobs]([OwnerId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_BackgroundJobs_ResourceId]
    ON [jobs].[BackgroundJobs]([ResourceId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_BackgroundJobs_Status_LeaseUntil_CreatedAt]
    ON [jobs].[BackgroundJobs]([Status] ASC, [LeaseUntil] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_BackgroundJobs_SubjectId]
    ON [jobs].[BackgroundJobs]([SubjectId] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_BackgroundJobs_ActiveKey]
    ON [jobs].[BackgroundJobs]([ActiveKey] ASC) WHERE ([ActiveKey] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'文件索引與評測等背景工作的租約、進度、重試與取消狀態。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'仍在執行工作的唯一鍵，避免同一業務重複排程。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'ActiveKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'背景工作執行／重試次數。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'Attempt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否收到取消要求；不表示工作已停止。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'CancelRequested';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'已完成的真實工作單位數。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'CompletedUnits';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料建立時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對外安全的錯誤代碼，不含密碼或完整例外。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'固定安全提示與查證代碼；不可保存例外自由文字。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'ErrorMessage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'背景工作種類。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'分類標籤或階段的顯示文字。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'Label';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'背景工作租約的 fencing token，防止過期 worker 提交。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'LeaseToken';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'背景工作租約的到期時間。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'LeaseUntil';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持久作業識別，跨佇列與重試保持不變。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'OperationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'排程來源的 W3C span 識別，重試沿用同一 trace。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'ParentSpanId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'工作處理的業務資源識別碼。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'ResourceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'背景工作目前階段。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'Stage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務執行狀態。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'操作所關聯的業務對象識別碼。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'SubjectId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'已知的總工作單位數；未知不表示百分比。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'TotalUnits';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'W3C 流程追蹤識別，僅由伺服器建立。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'TraceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料最後修改時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'jobs', @level1type = N'TABLE', @level1name = N'BackgroundJobs', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

