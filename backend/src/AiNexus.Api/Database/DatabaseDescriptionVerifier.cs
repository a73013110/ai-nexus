using AiNexus.BuildingBlocks;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Database;

/// <summary>Read-only verification of deployed MS_Description coverage.</summary>
public static class DatabaseDescriptionVerifier
{
    public static async Task VerifyAsync(NexusDbContext db, CancellationToken ct)
    {
        var missing = await db.Database.SqlQueryRaw<MissingDescription>(
            """
            WITH owned_tables AS (
              SELECT t.object_id, t.name, s.name AS schema_name FROM sys.tables t
              JOIN sys.schemas s ON s.schema_id=t.schema_id
              WHERE s.name IN ('identity','access','conversations','inference','operations','attachments','library',
                'collaboration','knowledge','content','projects','quality','workspace')
                OR (s.name='dbo' AND t.name='__EFMigrationsHistory')
            ), objects AS (
              SELECT 1 AS class, t.object_id AS major_id, 0 AS minor_id, 'TABLE' AS kind,
                CONCAT(t.schema_name,'.',t.name) AS object_name FROM owned_tables t
              UNION ALL SELECT 1,t.object_id,c.column_id,'COLUMN',CONCAT(t.schema_name,'.',t.name,'.',c.name)
                FROM owned_tables t JOIN sys.columns c ON t.object_id=c.object_id
              UNION ALL SELECT 7,t.object_id,i.index_id,'INDEX',CONCAT(t.schema_name,'.',t.name,'.',i.name)
                FROM owned_tables t JOIN sys.indexes i ON t.object_id=i.object_id
                WHERE i.name IS NOT NULL AND i.is_primary_key=0 AND i.is_unique_constraint=0 AND i.is_hypothetical=0
              UNION ALL SELECT 1,c.object_id,0,'CONSTRAINT',CONCAT(t.schema_name,'.',t.name,'.',c.name)
                FROM owned_tables t JOIN sys.objects c ON t.object_id=c.parent_object_id WHERE c.type IN ('PK','UQ','F','D','C')
              UNION ALL SELECT 3,s.schema_id,0,'SCHEMA',s.name FROM sys.schemas s
                WHERE s.name IN (SELECT schema_name FROM owned_tables)
            )
            SELECT o.kind AS Kind, o.object_name AS Name FROM objects o
            LEFT JOIN sys.extended_properties p ON p.class=o.class AND p.major_id=o.major_id AND p.minor_id=o.minor_id AND p.name='MS_Description'
            WHERE p.value IS NULL OR LEN(CONVERT(nvarchar(3750),p.value))=0
            """).ToListAsync(ct);
        if (missing.Count > 0)
            throw new ApiException(500, "database_description_missing", "資料庫物件缺少描述：" + string.Join("、", missing.Take(12).Select(x => x.Kind + " " + x.Name)));
        Console.WriteLine("資料庫描述檢查通過：schema、資料表、欄位、索引及約束均有 MS_Description。");
    }
    private sealed class MissingDescription
    {
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
    }
}
