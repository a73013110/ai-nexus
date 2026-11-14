CREATE TABLE [flow].[FlowInstanceSteps] (
    [InstanceId]       UNIQUEIDENTIFIER   NOT NULL,
    [StepNumber]       INT                NOT NULL,
    [Name]             NVARCHAR (60)      NOT NULL,
    [ApproverRule]     NVARCHAR (64)      NOT NULL,
    [ApproverArgument] NVARCHAR (2000)    NULL,
    [Status]           NVARCHAR (16)      NOT NULL,
    [Note]             NVARCHAR (32)      NULL,
    [Fallback]         BIT                CONSTRAINT [df_FlowInstanceSteps_Fallback] DEFAULT ((0)) NOT NULL,
    [ActivatedAt]      DATETIMEOFFSET (7) NULL,
    [DecidedAt]        DATETIMEOFFSET (7) NULL,
    [DeciderId]        UNIQUEIDENTIFIER   NULL,
    [OnBehalf]         BIT                CONSTRAINT [df_FlowInstanceSteps_OnBehalf] DEFAULT ((0)) NOT NULL,
    [Comment]          NVARCHAR (500)     NULL,
    CONSTRAINT [pk_FlowInstanceSteps] PRIMARY KEY CLUSTERED ([InstanceId] ASC, [StepNumber] ASC),
    CONSTRAINT [ck_FlowInstanceSteps_Decision] CHECK (([Status]=N'approved' OR [Status]=N'rejected') AND [DecidedAt] IS NOT NULL AND [DeciderId] IS NOT NULL OR [Status]<>N'approved' AND [Status]<>N'rejected'),
    CONSTRAINT [ck_FlowInstanceSteps_OnBehalf] CHECK ([OnBehalf]=(0) OR [DeciderId] IS NOT NULL),
    CONSTRAINT [ck_FlowInstanceSteps_Status] CHECK ([Status]=N'waiting' OR [Status]=N'active' OR [Status]=N'approved' OR [Status]=N'rejected' OR [Status]=N'skipped' OR [Status]=N'stopped'),
    CONSTRAINT [fk_FlowInstanceSteps_FlowInstances] FOREIGN KEY ([InstanceId]) REFERENCES [flow].[FlowInstances] ([Id]),
    CONSTRAINT [fk_FlowInstanceSteps_Users] FOREIGN KEY ([DeciderId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_FlowInstanceSteps_DeciderId]
    ON [flow].[FlowInstanceSteps]([DeciderId] ASC) WHERE ([DeciderId] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請送出時快照的關卡與各關的決定；改流程不影響進行中的申請。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關卡開始審核的時間；提醒與逾期從這裡起算。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'ActivatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送出時的審核人規則參數快照（JSON）。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'ApproverArgument';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送出時的審核人規則鍵快照。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'ApproverRule';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'審核意見。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'Comment';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'核准、退回或自動略過的時間。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'DecidedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'做出決定的 Users 主鍵；自動略過時是先前核准的同一人。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'DeciderId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'規則算不出審核人，改由持有 flow.approvals.manage 的人審核。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'Fallback';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'所屬申請。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'InstanceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'送出時的關卡名稱快照。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'略過的原因代碼：not-applicable 條件不成立、already-approved 同一人已核准前面的關卡。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'Note';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'由簽核管理員代審。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'OnBehalf';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'waiting 未到、active 審核中、approved 核准、rejected 退回、skipped 略過、stopped 申請結束時尚未完成。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關卡順序，與流程定義相同。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowInstanceSteps', @level2type = N'COLUMN', @level2name = N'StepNumber';


GO

