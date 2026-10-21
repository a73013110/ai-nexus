-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 把一批片段向量寫入 @Dimensions 對應的向量表；同一 profile 已有向量的片段略過、不覆寫，回傳實際寫入的列數。
--              @Rows 是 JSON 陣列 [{"chunkId", "contentHash"（SHA-256 的 hex）, "vector"（數字陣列）}]：整批一個參數，
--              批次文字固定、計畫可重用，列數也不受 2100 個參數上限限制。同一批不可有重複的 chunkId（撞唯一索引，整句失敗）。
--              背景索引寫入，不寫稽核：程式以 QueryProcedureAsync 呼叫，不帶稽核情境。
--              檢查與寫入在同一句 INSERT，不另開交易：由呼叫端的交易（工作檢查點）決定成敗。
-- =============================================
CREATE   PROCEDURE [knowledge].[usp_WriteChunkEmbeddings]
    @ProfileId  INT,
    @Dimensions INT,
    @Rows       NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Dimensions NOT IN (768, 1024)
        THROW 51000, N'knowledge.usp_WriteChunkEmbeddings: Dimensions must be 768 or 1024.', 1;

    DECLARE @inserted INT;

    -- VECTOR(n) 的 n 必須是常數，每個維度各寫一句，不用動態 SQL。
    -- CAST 把 JSON 陣列字串轉成向量，元素數與 n 不符時整句失敗。CONVERT 樣式 2 把不帶 0x 的 hex 轉成 binary。
    -- UPDLOCK＋HOLDLOCK：RCSI 下 NOT EXISTS 讀的是快照，要鎖住 (ProfileId, ChunkId) 的範圍，兩個 worker 才不會同時判定不存在而撞唯一索引。
    IF @Dimensions = 768
    BEGIN
        INSERT [knowledge].[ChunkEmbeddings768] (ChunkId, ProfileId, ContentHash, Vector)
        SELECT r.ChunkId, @ProfileId, CONVERT(BINARY(32), r.ContentHash, 2), CAST(r.Vector AS VECTOR(768))
        FROM OPENJSON(@Rows) WITH (
            ChunkId     UNIQUEIDENTIFIER '$.chunkId',
            ContentHash VARCHAR(64)      '$.contentHash',
            Vector      NVARCHAR(MAX)    '$.vector' AS JSON
        ) AS r
        WHERE NOT EXISTS (
            SELECT 1
            FROM [knowledge].[ChunkEmbeddings768] AS e WITH (UPDLOCK, HOLDLOCK)
            WHERE e.ProfileId = @ProfileId AND e.ChunkId = r.ChunkId
        );
        SET @inserted = @@ROWCOUNT;
    END
    ELSE
    BEGIN
        INSERT [knowledge].[ChunkEmbeddings1024] (ChunkId, ProfileId, ContentHash, Vector)
        SELECT r.ChunkId, @ProfileId, CONVERT(BINARY(32), r.ContentHash, 2), CAST(r.Vector AS VECTOR(1024))
        FROM OPENJSON(@Rows) WITH (
            ChunkId     UNIQUEIDENTIFIER '$.chunkId',
            ContentHash VARCHAR(64)      '$.contentHash',
            Vector      NVARCHAR(MAX)    '$.vector' AS JSON
        ) AS r
        WHERE NOT EXISTS (
            SELECT 1
            FROM [knowledge].[ChunkEmbeddings1024] AS e WITH (UPDLOCK, HOLDLOCK)
            WHERE e.ProfileId = @ProfileId AND e.ChunkId = r.ChunkId
        );
        SET @inserted = @@ROWCOUNT;
    END;

    SELECT @inserted AS Inserted;
END;

GO

