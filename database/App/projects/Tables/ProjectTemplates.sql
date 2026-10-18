CREATE TABLE [projects].[ProjectTemplates] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [ProjectId] UNIQUEIDENTIFIER NOT NULL,
    [Title]     NVARCHAR (80)    NOT NULL,
    [Content]   NVARCHAR (MAX)   NOT NULL,
    CONSTRAINT [PK_ProjectTemplates] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ProjectTemplates_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_ProjectTemplates_ProjectId]
    ON [projects].[ProjectTemplates]([ProjectId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'專案建立範本與預設指令。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'ProjectTemplates';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'範本的開場提示內容。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'ProjectTemplates', @level2type = N'COLUMN', @level2name = N'Content';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'ProjectTemplates', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯專案的識別碼。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'ProjectTemplates', @level2type = N'COLUMN', @level2name = N'ProjectId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'介面顯示標題。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'ProjectTemplates', @level2type = N'COLUMN', @level2name = N'Title';


GO

