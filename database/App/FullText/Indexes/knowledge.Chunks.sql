CREATE FULLTEXT INDEX ON [knowledge].[Chunks]
    ([HeadingPath] LANGUAGE 1028, [Text] LANGUAGE 1028)
    KEY INDEX [IX_Chunks_SearchId]
    ON [KnowledgeSearch];


GO

