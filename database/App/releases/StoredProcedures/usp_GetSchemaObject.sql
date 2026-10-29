-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-29
-- Description: 一個物件應有與實際的定義本文，供版本頁並排比對；不存在的一邊為 NULL，兩邊都沒有時不傳回任何列。
-- =============================================
CREATE   PROCEDURE [releases].[usp_GetSchemaObject]
    @SchemaName [sysname],
    @Name       [sysname]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COALESCE(a.[Type], e.[Type]) AS [Type], e.[Definition] AS [Expected], a.[Definition] AS [Actual]
    FROM (SELECT @SchemaName AS [SchemaName], @Name AS [Name]) AS k
    LEFT JOIN [releases].[SchemaObjects] AS e ON e.[SchemaName] = k.[SchemaName] AND e.[Name] = k.[Name]
    OUTER APPLY (
        SELECT x.[Type], x.[Definition] FROM [releases].[udf_SchemaObjects]() AS x
        WHERE x.[SchemaName] = k.[SchemaName] AND x.[Name] = k.[Name]
    ) AS a
    WHERE e.[Name] IS NOT NULL OR a.[Type] IS NOT NULL;
END;

GO

