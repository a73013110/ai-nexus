CREATE TABLE [projects].[Projects] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [Description]  NVARCHAR (2000)  NOT NULL,
    [Instructions] NVARCHAR (4000)  NOT NULL,
    [Version]      INT              NOT NULL,
    [IsArchived]   BIT              NOT NULL,
    CONSTRAINT [PK_Projects] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Projects_Resources_Id] FOREIGN KEY ([Id]) REFERENCES [collaboration].[Resources] ([Id])
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'專案資源、共用指令及範本版本。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'Projects';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務物件的用途說明。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'Projects', @level2type = N'COLUMN', @level2name = N'Description';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'Projects', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'專案共用指令。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'Projects', @level2type = N'COLUMN', @level2name = N'Instructions';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否封存對話；封存後不再接受新的生成。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'Projects', @level2type = N'COLUMN', @level2name = N'IsArchived';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'業務版本號，用於歷史或樂觀並行控制。', @level0type = N'SCHEMA', @level0name = N'projects', @level1type = N'TABLE', @level1name = N'Projects', @level2type = N'COLUMN', @level2name = N'Version';


GO

