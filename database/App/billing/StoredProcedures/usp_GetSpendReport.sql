-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 消費報表（個人、單一擁有者或全平台）：期間內的費用紀錄依幣別與計費類型彙總，附每日、各模型與各使用者明細。
--              結果集依序為：摘要、合計、每日、各模型、各使用者（@IncludeUsers = 1 才有資料列）。
--              期間與 @Offset 由程式傳入（SpendPeriod），不用資料庫時鐘；模型只依 Provider、ModelId 分組，顯示名稱與隱藏由程式決定。
--              Amount 為 NULL 是未知費用，但待結算（pending）的呼叫不算未知；舊版執行紀錄沒有費用紀錄，只計入呼叫次數。
-- =============================================
CREATE   PROCEDURE [billing].[usp_GetSpendReport]
    @OwnerId      UNIQUEIDENTIFIER = NULL,
    @From         DATETIMEOFFSET (7),
    @Until        DATETIMEOFFSET (7),
    @Offset       INT,
    @IncludeUsers BIT
AS
BEGIN
    SET NOCOUNT ON;

    -- 費用紀錄只讀一次，先彙總到各結果集共用的最細粒度，之後的結果集都由這裡加總，彼此一致。
    -- 每日依檢視者時區切分：CreatedAt 加 @Offset 分鐘後取日期，同 EF 對 C# CreatedAt.AddMinutes(offset).Date 的翻譯。
    -- CAST 取的是 CreatedAt 自身位移下的日期，不先轉 UTC；程式寫入的 CreatedAt 都是 UTC，所以結果等於 UTC＋@Offset 的日期。
    -- @OwnerId 選填：RECOMPILE 讓指定擁有者時走 OwnerId＋CreatedAt 索引，全平台時走 CreatedAt 索引。
    SELECT d.[Day], c.OwnerId, c.Provider, c.ModelId, c.Currency, c.Kind,
           SUM(ISNULL(c.Amount, 0)) AS Amount,
           COUNT(*) AS Requests,
           SUM(IIF(c.Amount IS NOT NULL, 1, 0)) AS KnownCalls,
           SUM(IIF(c.Amount IS NULL AND c.State <> N'pending', 1, 0)) AS UnknownCalls,
           SUM(IIF(c.State = N'pending', 1, 0)) AS PendingCalls,
           SUM(ISNULL(c.InputTokens, 0)) AS InputTokens,
           SUM(ISNULL(c.OutputTokens, 0)) AS OutputTokens
    INTO #charges
    FROM [billing].[ModelCharges] AS c
    CROSS APPLY (SELECT CAST(DATEADD(MINUTE, @Offset, c.CreatedAt) AS DATE) AS [Day]) AS d
    WHERE c.CreatedAt >= @From AND c.CreatedAt < @Until
      AND (@OwnerId IS NULL OR c.OwnerId = @OwnerId)
    GROUP BY d.[Day], c.OwnerId, c.Provider, c.ModelId, c.Currency, c.Kind
    OPTION (RECOMPILE);

    DECLARE @legacyCalls INT;
    SELECT @legacyCalls =
        (SELECT COUNT(*)
         FROM [inference].[GenerationRuns] AS r
         WHERE r.CreatedAt >= @From AND r.CreatedAt < @Until
           AND (@OwnerId IS NULL OR r.OwnerId = @OwnerId)
           AND NOT EXISTS (SELECT 1 FROM [billing].[ModelCharges] AS c WHERE c.Id = r.Id))
      + (SELECT COUNT(*)
         FROM [inference].[ModelInvocations] AS m
         WHERE m.CreatedAt >= @From AND m.CreatedAt < @Until
           AND (@OwnerId IS NULL OR m.OwnerId = @OwnerId)
           AND NOT EXISTS (SELECT 1 FROM [billing].[ModelCharges] AS c WHERE c.Id = m.Id))
    OPTION (RECOMPILE);

    SELECT ISNULL(SUM(Requests), 0) + @legacyCalls AS Requests,
           ISNULL(SUM(PendingCalls), 0) AS PendingCalls,
           @legacyCalls AS LegacyCalls,
           ISNULL(SUM(InputTokens), 0) AS InputTokens,
           ISNULL(SUM(OutputTokens), 0) AS OutputTokens
    FROM #charges;

    SELECT Currency, Kind, SUM(Amount) AS Amount, SUM(KnownCalls) AS KnownCalls, SUM(UnknownCalls) AS UnknownCalls
    FROM #charges
    GROUP BY Currency, Kind
    ORDER BY Currency, Kind;

    -- 樣式 23 是 yyyy-mm-dd，直接作為每日的標籤。
    SELECT CONVERT(CHAR (10), [Day], 23) AS Label, Currency, Kind,
           SUM(Amount) AS Amount, SUM(Requests) AS Requests, SUM(UnknownCalls) AS UnknownCalls,
           SUM(InputTokens) AS InputTokens, SUM(OutputTokens) AS OutputTokens
    FROM #charges
    GROUP BY [Day], Currency, Kind
    ORDER BY [Day], Currency, Kind;

    SELECT Provider, ModelId, Currency, Kind,
           SUM(Amount) AS Amount, SUM(Requests) AS Requests, SUM(UnknownCalls) AS UnknownCalls,
           SUM(InputTokens) AS InputTokens, SUM(OutputTokens) AS OutputTokens
    FROM #charges
    GROUP BY Provider, ModelId, Currency, Kind
    ORDER BY SUM(Requests) DESC, Provider, ModelId, Currency, Kind;

    -- 匯出 CSV 也用這份清單，所以不分頁；包含已刪除的使用者，費用仍算在他們名下。
    SELECT u.Id AS OwnerId, u.DisplayName, u.Account, c.Currency, c.Kind,
           SUM(c.Amount) AS Amount, SUM(c.Requests) AS Requests, SUM(c.UnknownCalls) AS UnknownCalls
    FROM #charges AS c
    JOIN [identity].[Users] AS u ON u.Id = c.OwnerId
    WHERE @IncludeUsers = 1
    GROUP BY u.Id, u.DisplayName, u.Account, c.Currency, c.Kind
    ORDER BY SUM(c.Requests) DESC, u.Account, u.Id, c.Currency, c.Kind;
END;

GO

