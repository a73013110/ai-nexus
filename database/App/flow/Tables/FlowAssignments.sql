CREATE TABLE [flow].[FlowAssignments] (
    [InstanceId] UNIQUEIDENTIFIER NOT NULL,
    [UserId]     UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [pk_FlowAssignments] PRIMARY KEY CLUSTERED ([InstanceId] ASC, [UserId] ASC),
    CONSTRAINT [fk_FlowAssignments_FlowInstances] FOREIGN KEY ([InstanceId]) REFERENCES [flow].[FlowInstances] ([Id]),
    CONSTRAINT [fk_FlowAssignments_Users] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_FlowAssignments_UserId]
    ON [flow].[FlowAssignments]([UserId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'審核中關卡的候選審核人（快取）；關卡啟動與排程重算時改寫，決定時另行即時重算。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowAssignments';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'審核中的申請。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowAssignments', @level2type = N'COLUMN', @level2name = N'InstanceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'可審核的 Users 主鍵。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowAssignments', @level2type = N'COLUMN', @level2name = N'UserId';


GO

