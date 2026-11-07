CREATE TABLE [identity].[ApiClients] (
    [Id]          UNIQUEIDENTIFIER   NOT NULL,
    [RowVersion]  ROWVERSION         NOT NULL,
    [Name]        NVARCHAR (100)     NOT NULL,
    [KeyHash]     BINARY (32)        NOT NULL,
    [KeyHint]     NVARCHAR (4)       NOT NULL,
    [KeyIssuedAt] DATETIMEOFFSET (7) NOT NULL,
    [CreatedAt]   DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [pk_ApiClients] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_ApiClients_KeyHash]
    ON [identity].[ApiClients]([KeyHash] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_ApiClients_Name]
    ON [identity].[ApiClients]([Name] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'可呼叫 AiNexus API 的外部系統：每個系統一把金鑰，只存雜湊；金鑰只在建立與重新產生時顯示一次。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'ApiClients';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'建立時間（UTC）。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'ApiClients', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼，由伺服器產生；也是外部系統呼叫的稽核對象（ResourceId）。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'ApiClients', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'金鑰的 SHA-256；金鑰是 256 位元亂數，不需要慢速雜湊。驗證時以雜湊查詢，不保存明碼。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'ApiClients', @level2type = N'COLUMN', @level2name = N'KeyHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'金鑰的末四碼，讓管理員辨認對方系統設定的是哪一把。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'ApiClients', @level2type = N'COLUMN', @level2name = N'KeyHint';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'目前這把金鑰的產生時間（UTC）；重新產生時更新，舊金鑰同時失效。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'ApiClients', @level2type = N'COLUMN', @level2name = N'KeyIssuedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'系統名稱，例如「公文系統」；不分大小寫唯一。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'ApiClients', @level2type = N'COLUMN', @level2name = N'Name';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'並行控制版本：資料列每次異動由資料庫自動更新。前端帶回讀取時的值，不符時拒絕寫入，不會覆蓋別人的修改。', @level0type = N'SCHEMA', @level0name = N'identity', @level1type = N'TABLE', @level1name = N'ApiClients', @level2type = N'COLUMN', @level2name = N'RowVersion';


GO

