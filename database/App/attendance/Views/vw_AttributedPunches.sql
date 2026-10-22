
-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-22
-- Description: 每筆刷卡依 [StartsAt, EndsAt) 歸戶到持有人。
--              對不到持有期間的刷卡也會出現（AssignmentId、UserId、HolderName 為 null），供查未歸戶卡號。
--              訪客卡（CardKind = visitor）不歸戶：照樣出現，但不算未歸戶。
--              未歸戶 = IsUnattributed = 1：沒有持有期間涵蓋，且不是訪客卡；Cards 沒登錄的卡號（CardId 為 null）也算。
--              只有 HolderName 沒有 UserId 的列是已歸戶（持有人還沒有帳號）。
--              同一卡片的期間重疊會讓同一筆刷卡重複出現，期間不重疊由程式保證。
--              不套用 RosterExclusions 的排除名單，那只是 HR 檢視篩選，由查詢端決定。
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
    c.[Kind]           AS [CardKind],
    a.[Id]             AS [AssignmentId],
    a.[UserId],
    a.[HolderName],
    CONVERT(BIT, IIF(a.[Id] IS NULL AND ISNULL(c.[Kind], N'employee') <> N'visitor', 1, 0)) AS [IsUnattributed]
FROM [attendance].[Punches] AS p
LEFT JOIN [attendance].[Cards] AS c
    ON c.[CardNumber] = p.[CardNumber]
LEFT JOIN [attendance].[CardAssignments] AS a
    ON a.[CardId] = c.[Id]
   AND c.[Kind] = N'employee'
   AND p.[PunchedAt] >= a.[StartsAt]
   AND (a.[EndsAt] IS NULL OR p.[PunchedAt] < a.[EndsAt]);

GO

