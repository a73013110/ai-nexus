CREATE TABLE [flow].[FlowDefinitionSteps] (
    [Kind]             NVARCHAR (64)   NOT NULL,
    [StepNumber]       INT             NOT NULL,
    [Name]             NVARCHAR (60)   NOT NULL,
    [ApproverRule]     NVARCHAR (64)   NOT NULL,
    [ApproverArgument] NVARCHAR (2000) NULL,
    [ConditionFact]    NVARCHAR (64)   NULL,
    [ConditionValue]   NVARCHAR (64)   NULL,
    CONSTRAINT [pk_FlowDefinitionSteps] PRIMARY KEY CLUSTERED ([Kind] ASC, [StepNumber] ASC),
    CONSTRAINT [ck_FlowDefinitionSteps_Condition] CHECK ([ConditionFact] IS NULL AND [ConditionValue] IS NULL OR [ConditionFact] IS NOT NULL AND [ConditionValue] IS NOT NULL),
    CONSTRAINT [ck_FlowDefinitionSteps_StepNumber] CHECK ([StepNumber]>=(1) AND [StepNumber]<=(20)),
    CONSTRAINT [fk_FlowDefinitionSteps_FlowDefinitions] FOREIGN KEY ([Kind]) REFERENCES [flow].[FlowDefinitions] ([Kind])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'流程的關卡，依 StepNumber 依序進行；修改流程時整批取代。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitionSteps';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'審核人規則的 JSON 參數；格式由規則定義與驗證，沒有參數時為 NULL。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitionSteps', @level2type = N'COLUMN', @level2name = N'ApproverArgument';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'審核人規則鍵，例如 applicant-manager、users、feature-holders，或業務模組註冊的規則。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitionSteps', @level2type = N'COLUMN', @level2name = N'ApproverRule';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關卡的適用條件：申請種類宣告的事實鍵；NULL 表示一律適用。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitionSteps', @level2type = N'COLUMN', @level2name = N'ConditionFact';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'事實等於這個值時才需要這一關。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitionSteps', @level2type = N'COLUMN', @level2name = N'ConditionValue';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'所屬流程的申請種類。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitionSteps', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關卡名稱，顯示給申請人與審核人。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitionSteps', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關卡順序，從 1 起連號。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitionSteps', @level2type = N'COLUMN', @level2name = N'StepNumber';


GO

