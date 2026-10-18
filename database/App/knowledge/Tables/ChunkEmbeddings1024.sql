CREATE TABLE [knowledge].[ChunkEmbeddings1024] (
    [Id]          INT              IDENTITY (1, 1) NOT NULL,
    [ChunkId]     UNIQUEIDENTIFIER NOT NULL,
    [ProfileId]   INT              NOT NULL,
    [ContentHash] BINARY (32)      NOT NULL,
    [Vector]      VECTOR(1024)     NOT NULL,
    CONSTRAINT [PK_ChunkEmbeddings1024] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChunkEmbeddings1024_Chunks_ChunkId] FOREIGN KEY ([ChunkId]) REFERENCES [knowledge].[Chunks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChunkEmbeddings1024_EmbeddingProfiles_ProfileId] FOREIGN KEY ([ProfileId]) REFERENCES [knowledge].[EmbeddingProfiles] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_ChunkEmbeddings1024_ChunkId]
    ON [knowledge].[ChunkEmbeddings1024]([ChunkId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_ChunkEmbeddings1024_ProfileId_ContentHash]
    ON [knowledge].[ChunkEmbeddings1024]([ProfileId] ASC, [ContentHash] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_ChunkEmbeddings1024_ProfileId_ChunkId]
    ON [knowledge].[ChunkEmbeddings1024]([ProfileId] ASC, [ChunkId] ASC);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'1024 維原生向量、片段關聯與 profile 內容快取。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ChunkEmbeddings1024';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'向量對應的結構化片段 Guid 識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ChunkEmbeddings1024', @level2type = N'COLUMN', @level2name = N'ChunkId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ChunkEmbeddings1024', @level2type = N'COLUMN', @level2name = N'ContentHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ChunkEmbeddings1024', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'向量空間及切段版本的 EmbeddingProfiles 外鍵。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ChunkEmbeddings1024', @level2type = N'COLUMN', @level2name = N'ProfileId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'L2 正規化的 float32 向量；SQL Server 使用 VECTOR 型別。', @level0type = N'SCHEMA', @level0name = N'knowledge', @level1type = N'TABLE', @level1name = N'ChunkEmbeddings1024', @level2type = N'COLUMN', @level2name = N'Vector';


GO

