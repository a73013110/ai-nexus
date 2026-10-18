using AiNexus.Data.App;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.IntegrationTests.Data;

/// <summary>
/// 每個表與欄位都有 <c>MS_Description</c>（ADR 0012）。說明由 DBA 在開發庫維護、隨匯出進版控，
/// 所以檢查 dacpac 建出的資料庫，而不是 EF 模型。
/// </summary>
public sealed class SchemaDescriptionTests
{
    [Fact]
    public async Task EveryTableAndColumnHasADescription()
    {
        await TestDatabase.WithContextAsync(async db => {
            var missing = await new DbContextSqlDatabase<AppDbContext>(db).QueryAsync<string>("""
                SELECT s.[name] + N'.' + t.[name] + COALESCE(N'.' + c.[name], N'')
                FROM sys.tables t
                JOIN sys.schemas s ON s.[schema_id] = t.[schema_id]
                CROSS APPLY (SELECT CAST(NULL AS sysname) AS [name], 0 AS [column_id] UNION ALL SELECT [name], [column_id] FROM sys.columns WHERE [object_id] = t.[object_id]) c
                WHERE t.[is_ms_shipped] = 0
                  AND NOT EXISTS (SELECT 1 FROM sys.extended_properties p
                                  WHERE p.[class] = 1 AND p.[major_id] = t.[object_id] AND p.[minor_id] = c.[column_id] AND p.[name] = N'MS_Description'
                                    AND LEN(CONVERT(nvarchar(max), p.[value])) > 0)
                ORDER BY 1
                """);
            Assert.True(missing.Count == 0, "請 DBA 在開發庫補上說明再匯出（scripts/Export-Database.ps1）：\n" + string.Join("\n", missing));
        });
    }
}
