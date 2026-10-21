-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-21
-- Description: 總覽每日分桶：@At 落在 [@From, @Until) 時回傳它在檢視者時區的曆日，否則不回傳列，所以 CROSS APPLY 同時排除期間外的資料。
--              @OffsetMinutes 是檢視者的 UTC 位移（程式傳入，不是時區名稱）；先位移再取日期，與 EF 的 at.AddMinutes(offset).Date 相同。
--              所有總覽 procedure 的每日序列都用它，日界才會一致；inline 函式會展開進呼叫端的查詢，期間條件仍可用 CreatedAt 索引。
-- =============================================
CREATE FUNCTION [dbo].[udf_DashboardDay] (@At DATETIMEOFFSET (7), @From DATETIMEOFFSET (7), @Until DATETIMEOFFSET (7), @OffsetMinutes INT)
RETURNS TABLE
AS
RETURN
    SELECT CONVERT (DATE, DATEADD(MINUTE, @OffsetMinutes, @At)) AS [Day]
    WHERE @At >= @From AND @At < @Until;

GO

