CREATE TABLE [releases].[Deployments] (
    [Id]         INT                IDENTITY (1, 1) NOT NULL,
    [Component]  NVARCHAR (16)      NOT NULL,
    [Version]    NVARCHAR (64)      NOT NULL,
    [Revision]   NVARCHAR (40)      NOT NULL,
    [StartPoint] NVARCHAR (64)      NULL,
    [DeployedAt] DATETIMEOFFSET (7) NOT NULL,
    [Host]       NVARCHAR (128)     NOT NULL,
    CONSTRAINT [pk_Deployments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_Deployments_Component] CHECK ([Component]=N'app' OR [Component]=N'database')
);


GO

CREATE NONCLUSTERED INDEX [ix_Deployments_Component_Id]
    ON [releases].[Deployments]([Component] ASC, [Id] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'部署歷史：資料庫由 update.sql 在最後一步寫入，程式在新版本第一次啟動時寫入（ADR 0027、0028）。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'Deployments';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'部署的部分：app（IIS 網站）或 database（結構與種子資料）。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'Deployments', @level2type = N'COLUMN', @level2name = N'Component';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'寫入時間（UTC）；資料庫取 SYSDATETIMEOFFSET()，程式取 TimeProvider。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'Deployments', @level2type = N'COLUMN', @level2name = N'DeployedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'執行的機器：資料庫為 HOST_NAME()（執行 update.sql 的用戶端），程式為 IIS 主機名稱。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'Deployments', @level2type = N'COLUMN', @level2name = N'Host';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'Deployments', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建置的 commit（完整 SHA）。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'Deployments', @level2type = N'COLUMN', @level2name = N'Revision';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料庫更新包的比對起點（tag 或 commit）；新建的庫與程式為空值。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'Deployments', @level2type = N'COLUMN', @level2name = N'StartPoint';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'版號 YYYY.MM.N；未發版的建置為 0.0.0-dev 或 <版號>-dev.<commit 數>。', @level0type = N'SCHEMA', @level0name = N'releases', @level1type = N'TABLE', @level1name = N'Deployments', @level2type = N'COLUMN', @level2name = N'Version';


GO

