-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-29
-- Description: 與這一版應有結構（releases.SchemaObjects）不同的物件：modified（雜湊不同）、added（快照沒有）、removed（實際沒有）。
--              ChangedAt 為 modify_date（修改）或 create_date（新增），刪除沒有時間；modify_date 在定義沒變時也可能更新，只當參考。
--              只傳回有差異的列，定義本文由 usp_GetSchemaObject 在打開差異時才讀。快照是空的（還沒有任何 update.sql 寫入）時不傳回任何列。
--              update.sql 的前置檢查與網站的監控共用這一支（ADR 0028）。
-- =============================================
CREATE   PROCEDURE [releases].[usp_ListSchemaChanges]
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM [releases].[SchemaObjects]) RETURN;

    -- 實際結構只算一次；FULL JOIN 兩邊各掃一次。
    SELECT a.[SchemaName], a.[Name], a.[Type], a.[Hash], a.[CreatedAt], a.[ModifiedAt]
    INTO #actual
    FROM [releases].[udf_SchemaObjects]() AS a;

    SELECT COALESCE(a.[SchemaName], e.[SchemaName]) AS [SchemaName],
           COALESCE(a.[Name], e.[Name]) AS [Name],
           COALESCE(a.[Type], e.[Type]) AS [Type],
           CASE WHEN e.[Name] IS NULL THEN N'added' WHEN a.[Name] IS NULL THEN N'removed' ELSE N'modified' END AS [Change],
           CASE WHEN e.[Name] IS NULL THEN a.[CreatedAt] ELSE a.[ModifiedAt] END AS [ChangedAt]
    FROM #actual AS a
    FULL JOIN [releases].[SchemaObjects] AS e ON e.[SchemaName] = a.[SchemaName] AND e.[Name] = a.[Name]
    WHERE (e.[Name] IS NULL OR a.[Name] IS NULL OR e.[Hash] <> a.[Hash])
      AND NOT EXISTS (
          SELECT 1 FROM [releases].[IgnoredObjects] AS i
          WHERE COALESCE(a.[SchemaName], e.[SchemaName]) + N'.' + COALESCE(a.[Name], e.[Name]) LIKE i.[Pattern]
      )
    ORDER BY [ChangedAt] DESC, [SchemaName], [Name];
END;

GO

