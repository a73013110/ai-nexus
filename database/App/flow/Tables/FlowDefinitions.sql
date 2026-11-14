CREATE TABLE [flow].[FlowDefinitions] (
    [Kind]       NVARCHAR (64)      NOT NULL,
    [UpdatedAt]  DATETIMEOFFSET (7) NOT NULL,
    [RowVersion] ROWVERSION         NOT NULL,
    CONSTRAINT [pk_FlowDefinitions] PRIMARY KEY CLUSTERED ([Kind] ASC)
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每種申請（kind）的簽核流程；一種一列，關卡在 FlowDefinitionSteps。種類由程式註冊，預設流程在 Seed。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitions';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'申請種類鍵，由業務模組在程式註冊，例如 repository-access。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitions', @level2type = N'COLUMN', @level2name = N'Kind';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'並行控制版本；修改流程時比對，防止覆蓋別人的修改。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitions', @level2type = N'COLUMN', @level2name = N'RowVersion';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'流程最後修改的時間。', @level0type = N'SCHEMA', @level0name = N'flow', @level1type = N'TABLE', @level1name = N'FlowDefinitions', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

