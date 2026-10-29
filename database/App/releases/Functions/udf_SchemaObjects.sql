-- =============================================
-- Author:      Yikai
-- Create date: 2026-10-29
-- Description: 目前資料庫的結構快照，每個物件一列：資料表、檢視、procedure、function、觸發程序與 synonym，不含 releases schema 本身。
--              Definition 給人看：程式物件為 sys.sql_modules.definition，資料表為欄位、索引與約束組成的固定格式（一行一項）。
--              Hash 是空白（含換行與 tab）收成一個空白後的 SHA2-256：SQL Server 把 ALTER 存成 CREATE、CREATE OR ALTER 存成 CREATE 加空白，
--              所以 SSMS（CRLF）、SqlPackage（LF）、ALTER 或 CREATE OR ALTER 建立的同一段定義雜湊相同；大小寫不同視為異動。update.sql、演練庫與網站都用這一份算法（ADR 0028）。
--              系統命名的約束（DF__x__1A2B3C）各庫名稱不同，不列名稱；說明（MS_Description）、權限、統計與全文索引不在比對範圍。
-- =============================================
CREATE   FUNCTION [releases].[udf_SchemaObjects] ()
RETURNS TABLE
AS
RETURN
    WITH columns AS (
        SELECT c.[object_id], c.[column_id],
               QUOTENAME(c.[name]) + N' '
               + CASE WHEN c.[is_computed] = 1 THEN N'AS ' + cc.[definition] + IIF(cc.[is_persisted] = 1, N' PERSISTED', N'')
                      ELSE t.[name]
                           + CASE WHEN t.[name] IN (N'nvarchar', N'nchar') THEN N'(' + IIF(c.[max_length] = -1, N'max', CONVERT(nvarchar(10), c.[max_length] / 2)) + N')'
                                  WHEN t.[name] IN (N'varchar', N'char', N'varbinary', N'binary') THEN N'(' + IIF(c.[max_length] = -1, N'max', CONVERT(nvarchar(10), c.[max_length])) + N')'
                                  WHEN t.[name] IN (N'decimal', N'numeric') THEN N'(' + CONVERT(nvarchar(3), c.[precision]) + N',' + CONVERT(nvarchar(3), c.[scale]) + N')'
                                  WHEN t.[name] IN (N'datetime2', N'datetimeoffset', N'time') THEN N'(' + CONVERT(nvarchar(3), c.[scale]) + N')'
                                  WHEN t.[name] = N'vector' THEN N'(' + CONVERT(nvarchar(10), c.[vector_dimensions]) + N')'
                                  ELSE N'' END
                           + IIF(c.[collation_name] <> CONVERT(sysname, DATABASEPROPERTYEX(DB_NAME(), 'Collation')), N' COLLATE ' + c.[collation_name], N'')
                           + IIF(c.[is_identity] = 1, N' IDENTITY (' + CONVERT(nvarchar(40), ic.[seed_value]) + N', ' + CONVERT(nvarchar(40), ic.[increment_value]) + N')', N'')
                 END
               + IIF(c.[is_nullable] = 1, N' NULL', N' NOT NULL')
               + ISNULL(N' DEFAULT ' + IIF(dc.[is_system_named] = 1, N'', QUOTENAME(dc.[name]) + N' ') + dc.[definition], N'') COLLATE DATABASE_DEFAULT AS [Line]
        FROM sys.columns AS c
        JOIN sys.types AS t ON t.[user_type_id] = c.[user_type_id]
        LEFT JOIN sys.computed_columns AS cc ON cc.[object_id] = c.[object_id] AND cc.[column_id] = c.[column_id]
        LEFT JOIN sys.identity_columns AS ic ON ic.[object_id] = c.[object_id] AND ic.[column_id] = c.[column_id]
        LEFT JOIN sys.default_constraints AS dc ON dc.[object_id] = c.[default_object_id]
    ),
    indexes AS (
        SELECT i.[object_id],
               CASE WHEN i.[is_primary_key] = 1 THEN N'1' WHEN i.[is_unique_constraint] = 1 THEN N'2' ELSE N'3' END + i.[name] AS [SortKey],
               CASE WHEN i.[is_primary_key] = 1 OR i.[is_unique_constraint] = 1
                    THEN IIF(kc.[is_system_named] = 1, N'CONSTRAINT', N'CONSTRAINT ' + QUOTENAME(i.[name])) + IIF(i.[is_primary_key] = 1, N' PRIMARY KEY ', N' UNIQUE ')
                    ELSE IIF(i.[is_unique] = 1, N'UNIQUE INDEX ', N'INDEX ') + QUOTENAME(i.[name]) + N' ' END
               + i.[type_desc]
               + ISNULL(N' (' + keys.[List] + N')', N'')
               + ISNULL(N' INCLUDE (' + included.[List] + N')', N'')
               + ISNULL(N' WHERE ' + i.[filter_definition], N'')
               + IIF(i.[is_disabled] = 1, N' DISABLED', N'') COLLATE DATABASE_DEFAULT AS [Line]
        FROM sys.indexes AS i
        LEFT JOIN sys.key_constraints AS kc ON kc.[parent_object_id] = i.[object_id] AND kc.[unique_index_id] = i.[index_id]
        OUTER APPLY (
            SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.[name]) + IIF(ixc.[is_descending_key] = 1, N' DESC', N' ASC')), N', ')
                       WITHIN GROUP (ORDER BY ixc.[key_ordinal]) AS [List]
            FROM sys.index_columns AS ixc
            JOIN sys.columns AS c ON c.[object_id] = ixc.[object_id] AND c.[column_id] = ixc.[column_id]
            WHERE ixc.[object_id] = i.[object_id] AND ixc.[index_id] = i.[index_id] AND ixc.[key_ordinal] > 0
        ) AS keys
        OUTER APPLY (
            SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.[name])), N', ') WITHIN GROUP (ORDER BY c.[name]) AS [List]
            FROM sys.index_columns AS ixc
            JOIN sys.columns AS c ON c.[object_id] = ixc.[object_id] AND c.[column_id] = ixc.[column_id]
            WHERE ixc.[object_id] = i.[object_id] AND ixc.[index_id] = i.[index_id] AND ixc.[is_included_column] = 1
        ) AS included
        WHERE i.[type] > 0 AND i.[is_hypothetical] = 0
    ),
    constraints AS (
        SELECT fk.[parent_object_id] AS [object_id], N'4' + fk.[name] AS [SortKey],
               IIF(fk.[is_system_named] = 1, N'CONSTRAINT', N'CONSTRAINT ' + QUOTENAME(fk.[name]))
               + N' FOREIGN KEY (' + cols.[Parent] + N') REFERENCES ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.[referenced_object_id])) + N'.' + QUOTENAME(OBJECT_NAME(fk.[referenced_object_id]))
               + N' (' + cols.[Referenced] + N')'
               + IIF(fk.[delete_referential_action] = 0, N'', N' ON DELETE ' + REPLACE(fk.[delete_referential_action_desc], N'_', N' '))
               + IIF(fk.[update_referential_action] = 0, N'', N' ON UPDATE ' + REPLACE(fk.[update_referential_action_desc], N'_', N' '))
               + IIF(fk.[is_disabled] = 1, N' DISABLED', N'') COLLATE DATABASE_DEFAULT AS [Line]
        FROM sys.foreign_keys AS fk
        CROSS APPLY (
            SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(pc.[name])), N', ') WITHIN GROUP (ORDER BY fkc.[constraint_column_id]) AS [Parent],
                   STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(rc.[name])), N', ') WITHIN GROUP (ORDER BY fkc.[constraint_column_id]) AS [Referenced]
            FROM sys.foreign_key_columns AS fkc
            JOIN sys.columns AS pc ON pc.[object_id] = fkc.[parent_object_id] AND pc.[column_id] = fkc.[parent_column_id]
            JOIN sys.columns AS rc ON rc.[object_id] = fkc.[referenced_object_id] AND rc.[column_id] = fkc.[referenced_column_id]
            WHERE fkc.[constraint_object_id] = fk.[object_id]
        ) AS cols
        UNION ALL
        SELECT ck.[parent_object_id], N'5' + ck.[name],
               IIF(ck.[is_system_named] = 1, N'CONSTRAINT', N'CONSTRAINT ' + QUOTENAME(ck.[name])) + N' CHECK ' + ck.[definition] + IIF(ck.[is_disabled] = 1, N' DISABLED', N'') COLLATE DATABASE_DEFAULT
        FROM sys.check_constraints AS ck
    ),
    objects AS (
        SELECT o.[schema_id], o.[name], o.[create_date], o.[modify_date],
               CASE o.[type] WHEN 'V' THEN N'view' WHEN 'P' THEN N'procedure' WHEN 'TR' THEN N'trigger' ELSE N'function' END AS [Type],
               ISNULL(m.[definition], N'-- WITH ENCRYPTION') COLLATE DATABASE_DEFAULT AS [Definition]
        FROM sys.sql_modules AS m
        JOIN sys.objects AS o ON o.[object_id] = m.[object_id]
        WHERE o.[type] IN ('P', 'V', 'FN', 'IF', 'TF', 'TR') AND o.[is_ms_shipped] = 0
        UNION ALL
        SELECT t.[schema_id], t.[name], t.[create_date], t.[modify_date], N'table',
               (SELECT STRING_AGG(CONVERT(nvarchar(max), x.[Line]), NCHAR(10)) WITHIN GROUP (ORDER BY x.[Section], x.[Position], x.[SortKey])
                FROM (
                    SELECT 0 AS [Section], c.[column_id] AS [Position], N'' AS [SortKey], c.[Line] FROM columns AS c WHERE c.[object_id] = t.[object_id]
                    UNION ALL
                    SELECT 1, 0, i.[SortKey], i.[Line] FROM indexes AS i WHERE i.[object_id] = t.[object_id]
                    UNION ALL
                    SELECT 1, 0, k.[SortKey], k.[Line] FROM constraints AS k WHERE k.[object_id] = t.[object_id]
                ) AS x)
        FROM sys.tables AS t
        WHERE t.[is_ms_shipped] = 0
        UNION ALL
        SELECT s.[schema_id], s.[name], s.[create_date], s.[modify_date], N'synonym', N'SYNONYM FOR ' + s.[base_object_name] COLLATE DATABASE_DEFAULT
        FROM sys.synonyms AS s
    )
    SELECT SCHEMA_NAME(o.[schema_id]) AS [SchemaName], o.[name] AS [Name], o.[Type], o.[Definition],
           -- sys.objects 的時間是伺服器當地時間，以目前時差轉成 datetimeoffset。
           TODATETIMEOFFSET(o.[create_date], DATEPART(TZOFFSET, SYSDATETIMEOFFSET())) AS [CreatedAt],
           TODATETIMEOFFSET(o.[modify_date], DATEPART(TZOFFSET, SYSDATETIMEOFFSET())) AS [ModifiedAt],
           CONVERT(binary(32), HASHBYTES('SHA2_256', collapsed.[Text])) AS [Hash]
    FROM objects AS o
    -- 二進位定序：控制字元在 Windows 定序沒有權重，REPLACE 會比對到不該比對的位置。
    CROSS APPLY (SELECT TRANSLATE(o.[Definition] COLLATE Latin1_General_100_BIN2, NCHAR(9) + NCHAR(10) + NCHAR(13), N'   ') AS [Text]) AS spaced
    CROSS APPLY (SELECT TRIM(REPLACE(REPLACE(REPLACE(spaced.[Text], N' ', N' ' + NCHAR(7)), NCHAR(7) + N' ', N''), NCHAR(7), N'')) AS [Text]) AS collapsed
    WHERE SCHEMA_NAME(o.[schema_id]) <> N'releases';

GO

