-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 單一對話的花費：對話內的費用紀錄依幣別與計費類型彙總，附各模型明細；不限期間。
--              結果集依序為：摘要、合計、各模型。對話不存在、已刪除或不屬於 @OwnerId 時只回傳沒有資料列的摘要，由程式回 conversation_not_found。
--              模型只依 Provider、ModelId 分組，顯示名稱與隱藏由程式決定；Amount 為 NULL 是未知費用，待結算（pending）不算未知。
-- =============================================
CREATE   PROCEDURE [billing].[usp_GetConversationSpend]
    @OwnerId        UNIQUEIDENTIFIER,
    @ConversationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.Provider, c.ModelId, c.Currency, c.Kind,
           SUM(ISNULL(c.Amount, 0)) AS Amount,
           COUNT(*) AS Requests,
           SUM(IIF(c.Amount IS NOT NULL, 1, 0)) AS KnownCalls,
           SUM(IIF(c.Amount IS NULL AND c.State <> N'pending', 1, 0)) AS UnknownCalls,
           SUM(IIF(c.State = N'pending', 1, 0)) AS PendingCalls,
           SUM(ISNULL(c.InputTokens, 0)) AS InputTokens,
           SUM(ISNULL(c.OutputTokens, 0)) AS OutputTokens
    INTO #models
    FROM [billing].[ModelCharges] AS c
    WHERE c.OwnerId = @OwnerId AND c.ConversationId = @ConversationId
    GROUP BY c.Provider, c.ModelId, c.Currency, c.Kind;

    -- 舊版執行紀錄沒有費用紀錄，只計入呼叫次數，費用保持未知。
    DECLARE @legacyCalls INT = (SELECT COUNT(*)
                                FROM [inference].[GenerationRuns] AS r
                                WHERE r.ConversationId = @ConversationId
                                  AND NOT EXISTS (SELECT 1 FROM [billing].[ModelCharges] AS c WHERE c.Id = r.Id));

    -- 摘要列只在對話屬於 @OwnerId 且未刪除時輸出（同 EF 的 SoftDelete 篩選）；沒有這一列就不再輸出其他結果集。
    SELECT ISNULL(t.Requests, 0) + @legacyCalls AS Requests,
           ISNULL(t.PendingCalls, 0) AS PendingCalls,
           @legacyCalls AS LegacyCalls
    FROM [conversations].[Conversations] AS v
    CROSS JOIN (SELECT SUM(Requests) AS Requests, SUM(PendingCalls) AS PendingCalls FROM #models) AS t
    WHERE v.Id = @ConversationId AND v.OwnerId = @OwnerId AND v.IsDeleted = 0;

    IF @@ROWCOUNT = 0 RETURN;

    SELECT Currency, Kind, SUM(Amount) AS Amount, SUM(KnownCalls) AS KnownCalls, SUM(UnknownCalls) AS UnknownCalls
    FROM #models
    GROUP BY Currency, Kind
    ORDER BY Currency, Kind;

    SELECT Provider, ModelId, Currency, Kind, Amount, Requests, UnknownCalls, InputTokens, OutputTokens
    FROM #models
    ORDER BY Requests DESC, Provider, ModelId, Currency, Kind;
END;

GO

