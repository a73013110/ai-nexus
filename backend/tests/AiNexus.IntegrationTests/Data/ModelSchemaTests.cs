using AiNexus.Data.App;
using AiNexus.Platform.Data.Sql;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.Data;

/// <summary>EF 只做對應（ADR 0011）：實體與欄位必須和 dacpac 建出的資料庫一致。</summary>
public sealed class ModelSchemaTests
{
    [Fact]
    public async Task EfModelMatchesTheDeployedSchema()
    {
        await TestDatabase.WithContextAsync(async db => {
            var differences = await ModelSchema.CompareAsync(db, new DbContextSqlDatabase<AppDbContext>(db), CancellationToken.None);
            // 資料庫可以先於程式加欄位、只經 SQL 存取的表不必有實體，所以只擋會讓程式失敗的差異。
            var blocking = differences.Where(x => x.Blocking).Select(x => x.ToString()).ToList();
            Assert.True(blocking.Count == 0, "EF 對應與 database/App 不一致；改實體或組態讓它符合資料庫：\n" + string.Join("\n", blocking));
        });
    }

    [Fact]
    public async Task ComparisonReportsEachKindOfDrift()
    {
        await TestDatabase.WithContextAsync(async db => {
            await db.Database.GetDbConnection().ExecuteAsync("""
                ALTER TABLE [notifications].[Notifications] DROP COLUMN [Title];
                ALTER TABLE [notifications].[Notifications] ALTER COLUMN [Body] nvarchar(10) NULL;
                -- 表是空的，NOT NULL 欄位不必有預設值也加得上去。
                ALTER TABLE [notifications].[Notifications] ADD [Extra] int NULL, [Required] int NOT NULL CONSTRAINT [df_Notifications_Required] DEFAULT 0, [Strict] int NOT NULL;
                CREATE TABLE [dbo].[SqlOnly] ([Id] int NOT NULL);
                """);
            var differences = (await ModelSchema.CompareAsync(db, new DbContextSqlDatabase<AppDbContext>(db), CancellationToken.None)).ToDictionary(x => x.Target);
            Assert.Equal((SchemaDifferenceKind.MissingColumn, true), Shape(differences["notifications.Notifications.Title"]));
            Assert.Equal((SchemaDifferenceKind.TypeMismatch, true), Shape(differences["notifications.Notifications.Body"]));
            Assert.Equal((SchemaDifferenceKind.UnmappedColumn, false), Shape(differences["notifications.Notifications.Extra"]));
            // 有預設值的 NOT NULL 欄位 EF 不給值也能新增；沒有預設值的會讓新增失敗。
            Assert.Equal((SchemaDifferenceKind.UnmappedColumn, false), Shape(differences["notifications.Notifications.Required"]));
            Assert.Equal((SchemaDifferenceKind.UnmappedColumn, true), Shape(differences["notifications.Notifications.Strict"]));
            Assert.Equal((SchemaDifferenceKind.UnmappedObject, false), Shape(differences["dbo.SqlOnly"]));
        });
    }

    private static (SchemaDifferenceKind, bool) Shape(SchemaDifference difference) => (difference.Kind, difference.Blocking);
}
