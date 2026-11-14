CREATE TABLE [flow].[FlowInstances] (
    [Id]          UNIQUEIDENTIFIER   NOT NULL,
    [Kind]        NVARCHAR (64)      NOT NULL,
    [SubjectId]   UNIQUEIDENTIFIER   NOT NULL,
    [ApplicantId] UNIQUEIDENTIFIER   NOT NULL,
    [Title]       NVARCHAR (200)     NOT NULL,
    [Details]     NVARCHAR (4000)    NOT NULL,
    [Status]      NVARCHAR (16)      NOT NULL,
    [CurrentStep] INT                NULL,
    [SubmittedAt] DATETIMEOFFSET (7) NOT NULL,
    [CompletedAt] DATETIMEOFFSET (7) NULL,
    [RowVersion]  ROWVERSION         NOT NULL,
    CONSTRAINT [pk_FlowInstances] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_FlowInstances_Completed] CHECK ([Status]=N'pending' AND [CompletedAt] IS NULL OR [Status]<>N'pending' AND [CompletedAt] IS NOT NULL AND [CompletedAt]>=[SubmittedAt]),
    CONSTRAINT [ck_FlowInstances_CurrentStep] CHECK ([Status]=N'pending' AND [CurrentStep] IS NOT NULL OR [Status]<>N'pending' AND [CurrentStep] IS NULL),
    CONSTRAINT [ck_FlowInstances_Status] CHECK ([Status]=N'pending' OR [Status]=N'approved' OR [Status]=N'rejected' OR [Status]=N'cancelled' OR [Status]=N'expired'),
    CONSTRAINT [fk_FlowInstances_FlowDefinitions] FOREIGN KEY ([Kind]) REFERENCES [flow].[FlowDefinitions] ([Kind]),
    CONSTRAINT [fk_FlowInstances_Users] FOREIGN KEY ([ApplicantId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_FlowInstances_ApplicantId_SubmittedAt]
    ON [flow].[FlowInstances]([ApplicantId] ASC, [SubmittedAt] DESC);


GO

CREATE NONCLUSTERED INDEX [ix_FlowInstances_Status_SubmittedAt]
    ON [flow].[FlowInstances]([Status] ASC, [SubmittedAt] DESC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_FlowInstances_Kind_SubjectId]
    ON [flow].[FlowInstances]([Kind] ASC, [SubjectId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'一次送出的申請（流程實例）；狀態只存在這裡，業務表只存申請內容。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請人的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'ApplicantId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結束時間；審核中為 NULL。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'CompletedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'審核中的關卡序號；結束後為 NULL。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'CurrentStep';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送出時的申請內容快照：JSON 陣列 [{label, value}]，通用畫面直接顯示。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'Details';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請種類。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'並行控制版本；兩位審核人同時決定時只有一位成功。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'RowVersion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'pending 審核中、approved 核准、rejected 退回、cancelled 申請人取消、expired 逾期未完成。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務模組的申請內容主鍵（例如 repositories.RepositoryAccessRequests.Id）；同一種類內唯一。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'SubjectId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送出時間。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'SubmittedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送出時的標題快照，顯示在待審與申請清單。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstances', @level2type = N'COLUMN', @level2name = N'Title';


GO

