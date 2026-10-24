
-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-22
-- Description: 每筆刷卡依 [StartsAt, EndsAt) 歸戶到持有人。
--              對不到持有期間的刷卡也會出現（AssignmentId、UserId、HolderName 為 null），供查待指派的卡號。
--              未歸戶 = IsUnattributed = 1：沒有持有期間涵蓋；Cards 沒有這個卡號（CardId 為 null）也算。
--              只有 HolderName 沒有 UserId 的列是已歸戶（持有人還沒有帳號）。
--              同一卡片的期間重疊會讓同一筆刷卡重複出現，期間不重疊由程式保證。
--              不套用 Cards.HiddenFromRecords，那只是 HR 檢視篩選，由查詢端決定。
-- Modify:      2026-10-24 Yikai 移除卡片種類（CardKind）：訪客卡改為隱藏，未歸戶只看有沒有持有期間
-- =============================================
CREATE   VIEW [attendance].[vw_AttributedPunches]
AS
SELECT
    p.[Id]             AS [PunchId],
    p.[PunchFileId],
    p.[CardNumber],
    p.[PunchedAt],
    p.[AttendanceDate],
    p.[MachineCode],
    p.[EventCode],
    c.[Id]             AS [CardId],
    a.[Id]             AS [AssignmentId],
    a.[UserId],
    a.[HolderName],
    ISNULL(CONVERT(BIT, IIF(a.[Id] IS NULL, 1, 0)), CONVERT(BIT, 0)) AS [IsUnattributed]
FROM [attendance].[Punches] AS p
LEFT JOIN [attendance].[Cards] AS c
    ON c.[CardNumber] = p.[CardNumber]
LEFT JOIN [attendance].[CardAssignments] AS a
    ON a.[CardId] = c.[Id]
   AND p.[PunchedAt] >= a.[StartsAt]
   AND (a.[EndsAt] IS NULL OR p.[PunchedAt] < a.[EndsAt]);

GO

