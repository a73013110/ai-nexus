CREATE TABLE [prompts].[PromptTemplates] (
    [Id]        UNIQUEIDENTIFIER   NOT NULL,
    [OwnerId]   UNIQUEIDENTIFIER   NOT NULL,
    [Title]     NVARCHAR (80)      NOT NULL,
    [Content]   NVARCHAR (MAX)     NOT NULL,
    [UpdatedAt] DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_PromptTemplates] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PromptTemplates_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_PromptTemplates_OwnerId_UpdatedAt]
    ON [prompts].[PromptTemplates]([OwnerId] ASC, [UpdatedAt] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用者私人提示詞範本。', @level0type = N'SCHEMA', @level0name = N'prompts', @level1type = N'TABLE', @level1name = N'PromptTemplates';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'提示詞內容。', @level0type = N'SCHEMA', @level0name = N'prompts', @level1type = N'TABLE', @level1name = N'PromptTemplates', @level2type = N'COLUMN', @level2name = N'Content';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'prompts', @level1type = N'TABLE', @level1name = N'PromptTemplates', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。', @level0type = N'SCHEMA', @level0name = N'prompts', @level1type = N'TABLE', @level1name = N'PromptTemplates', @level2type = N'COLUMN', @level2name = N'OwnerId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面顯示標題。', @level0type = N'SCHEMA', @level0name = N'prompts', @level1type = N'TABLE', @level1name = N'PromptTemplates', @level2type = N'COLUMN', @level2name = N'Title';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料最後修改時間，採 UTC offset。', @level0type = N'SCHEMA', @level0name = N'prompts', @level1type = N'TABLE', @level1name = N'PromptTemplates', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

