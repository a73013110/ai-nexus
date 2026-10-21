-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-20
-- Description: 新增一個價格版本並寫入成功稽核，回傳新增的那一列。
--              同一供應商、模型與生效時間已存在時 THROW 50000 price_exists（ADR 0011）；
--              失敗稽核由程式在回復後寫入，procedure 內寫的會跟著交易回復（ADR 0014）。
-- Modify:      2026-10-21 Yikai 改用標準檔頭，說明移入檔頭
-- =============================================
CREATE   PROCEDURE [billing].[usp_CreateModelPrice]
    @Id                    UNIQUEIDENTIFIER,
    @Provider              NVARCHAR (32),
    @ModelId               NVARCHAR (160),
    @Currency              NVARCHAR (3),
    @Kind                  NVARCHAR (16),
    @InputPerMillion       DECIMAL (20, 8),
    @CachedInputPerMillion DECIMAL (20, 8),
    @OutputPerMillion      DECIMAL (20, 8),
    @PerRequest            DECIMAL (20, 8),
    @RequestCharge         NVARCHAR (16),
    @EffectiveAt           DATETIMEOFFSET (7),
    @Note                  NVARCHAR (500),
    @CreatedBy             UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- 只在沒有外層交易時自己開；出錯時不回復外層交易，直接 THROW 交給呼叫端回復（否則 EF 的寫入也會被回復，並出現錯誤 266）。
    DECLARE @ownsTran BIT = IIF(@@TRANCOUNT = 0, 1, 0);
    BEGIN TRY
        IF @ownsTran = 1 BEGIN TRAN;

        -- RCSI 下一般讀取不等寫入，兩個請求會同時通過檢查；UPDLOCK, HOLDLOCK 鎖住索引範圍，第二個請求等第一個提交後才判斷。
        IF EXISTS (SELECT 1 FROM [billing].[ModelPrices] WITH (UPDLOCK, HOLDLOCK)
                   WHERE Provider = @Provider AND ModelId = @ModelId AND EffectiveAt = @EffectiveAt)
            THROW 50000, N'price_exists', 1;

        INSERT [billing].[ModelPrices] (Id, Provider, ModelId, Currency, Kind, InputPerMillion, CachedInputPerMillion, OutputPerMillion,
                                        PerRequest, RequestCharge, Note, EffectiveAt, CreatedAt, CreatedBy)
        SELECT @Id, @Provider, @ModelId, @Currency, @Kind, @InputPerMillion, @CachedInputPerMillion, @OutputPerMillion,
               @PerRequest, @RequestCharge, @Note, @EffectiveAt, c.At, @CreatedBy
        FROM [audit].[udf_AuditContext]() AS c;

        -- 明細只放 AuditRedactor 白名單內的 camelCase 鍵。
        DECLARE @details NVARCHAR (MAX) = (SELECT @Provider AS provider, @ModelId AS modelId, @Kind AS kind, @EffectiveAt AS effectiveAt
                                           FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
        EXEC [audit].[usp_WriteAuditEvent] @Action = N'billing.price.created', @ResourceId = @Id, @Result = N'created', @DetailsJson = @details;

        SELECT Id, Provider, ModelId, Currency, Kind, InputPerMillion, CachedInputPerMillion, OutputPerMillion,
               PerRequest, RequestCharge, Note, EffectiveAt, CreatedAt, CreatedBy
        FROM [billing].[ModelPrices]
        WHERE Id = @Id;

        IF @ownsTran = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @ownsTran = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;

GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'新增一個價格版本並寫入成功稽核；同一供應商、模型與生效時間已存在時以 price_exists 失敗。', @level0type = N'SCHEMA', @level0name = N'billing', @level1type = N'PROCEDURE', @level1name = N'usp_CreateModelPrice';


GO

