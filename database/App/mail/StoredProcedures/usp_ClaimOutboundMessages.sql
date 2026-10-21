-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: MailDispatcher 每一輪的認領：依優先序取最多 @Take 封可寄的信，改成 sending、次數加一並掛上租約 @Lease，回傳認領的列。
--              可寄＝queued 且 NextAttemptAt 已到，或仍是 sending 但租約已過期（worker 中止）；PreviousStatus 讓程式補記 abandoned 嘗試。
--              @Now、@LeaseUntil 由程式傳入（TimeProvider），不用資料庫時鐘。OUTPUT 不保證順序，程式自行依 Priority、NextAttemptAt 排序。
--              背景工作的狀態轉移，不寫稽核：程式以 QueryProcedureAsync 呼叫，不帶稽核情境。
-- =============================================
CREATE   PROCEDURE [mail].[usp_ClaimOutboundMessages]
    @Take       INT,
    @Now        DATETIMEOFFSET (7),
    @Lease      UNIQUEIDENTIFIER,
    @LeaseUntil DATETIMEOFFSET (7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- READPAST 只能在 READ COMMITTED／REPEATABLE READ 使用，呼叫端的連線可能還是 SERIALIZABLE（連線池重用、外層交易）。
    -- 在 procedure 內設定：procedure 結束時 SQL Server 恢復呼叫前的層級，不會留給同一條連線之後的命令。
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    -- UPDLOCK＋READPAST：並行的 worker 跳過彼此已鎖定的列，同一封不會被認領兩次。
    -- Status IN 與 ix_OutboundMessages_Pending 的篩選相同，查詢才能用這個索引；後面的 OR 才是實際條件。
    WITH due AS (
        SELECT TOP (@Take) Id, Status, Priority, Attempts, NextAttemptAt, LeaseToken, LeaseUntil, UpdatedAt
        FROM [mail].[OutboundMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE Status IN (N'queued', N'sending')
          AND ((Status = N'queued' AND NextAttemptAt <= @Now) OR (Status = N'sending' AND LeaseUntil < @Now))
        ORDER BY Priority DESC, NextAttemptAt
    )
    UPDATE due
    SET Status = N'sending', Attempts = Attempts + 1, LeaseToken = @Lease, LeaseUntil = @LeaseUntil, UpdatedAt = @Now
    OUTPUT inserted.Id, inserted.Attempts, inserted.Priority, inserted.NextAttemptAt, deleted.Status AS PreviousStatus;
END;

GO

