using System.Text.RegularExpressions;
using AiNexus.Features.Persistence;
using AiNexus.Features.Persistence.ReleaseScripts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace AiNexus.IntegrationTests.Persistence;

/// <summary>
/// 發佈 SQL 由真實 migration 以 SQL Server 的產生器產出，且不需要連線，內容必須
/// 與 <c>dotnet ef migrations script --idempotent</c> 產出的內容完全一致，並依物件與種類拆分。
/// </summary>
public sealed partial class ReleaseScriptGeneratorTests
{
    [Fact]
    public void FilesJoinedInOrderAreTheIdempotentScript()
    {
        using var db = Context();
        var script = new ReleaseScriptGenerator(Context).Generate(Migration.InitialDatabase);
        Assert.Equal(db.Database.GetMigrations(), script.Migrations);
        Assert.Equal(Statements(Reference(db, Migration.InitialDatabase, null)), Statements(WrittenFiles(script)));
    }

    [Theory]
    [InlineData(1, -2)]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    public void PartialRangeIsTheIdempotentScriptOfThatRange(int from, int? to)
    {
        using var db = Context();
        var ids = db.Database.GetMigrations().ToList();
        var fromId = ids[from < 0 ? ids.Count + from : from];
        var toId = to is null ? null : ids[ids.Count + to.Value];
        // 名稱的解析方式與 id 相同。
        var script = new ReleaseScriptGenerator(Context).Generate(fromId[(fromId.IndexOf('_', StringComparison.Ordinal) + 1)..], toId);
        Assert.Equal(ids.Where(id => string.CompareOrdinal(id, fromId) > 0 && (toId is null || string.CompareOrdinal(id, toId) <= 0)), script.Migrations);
        Assert.Equal(Statements(Reference(db, fromId, toId)), Statements(WrittenFiles(script)));
    }

    [Fact]
    public void SeedDataGoesToDataFilesAndEverythingElseToSchemaFiles()
    {
        using var db = Context();
        var script = new ReleaseScriptGenerator(Context).Generate(Migration.InitialDatabase);
        var seeds = Migrations(db).SelectMany(x => x.UpOperations).Count(IsSeed);
        Assert.True(seeds > 0, "AccessControl seeds features and navigation groups with HasData.");
        Assert.Equal(seeds, script.Files.Where(x => x.Kind == ReleaseScriptKind.Data).SelectMany(x => x.Operations).Count(IsSeed));
        Assert.All(script.Files, file =>
        {
            Assert.StartsWith(file.Kind == ReleaseScriptKind.Data ? "data/" : "schema/", file.Path, StringComparison.Ordinal);
            Assert.All(file.Operations, operation => Assert.Equal(file.Kind, ReleaseScriptGenerator.Classify(operation)));
        });
        Assert.Contains(script.Files, x => x.Kind == ReleaseScriptKind.Data && x.Target == "accesscontrol.Features");
    }

    [Fact]
    public void HistoryRowIsTheLastFileOfEachMigration()
    {
        var script = new ReleaseScriptGenerator(Context).Generate(Migration.InitialDatabase);
        Assert.Equal(ReleaseScriptGenerator.HistoryTarget, script.Files[0].Target);
        Assert.Null(script.Files[0].MigrationId);
        foreach (var id in script.Migrations)
        {
            var files = script.Files.Where(x => x.MigrationId == id).ToList();
            Assert.True(files.Count > 1);
            Assert.Equal(ReleaseScriptGenerator.HistoryTarget, files[^1].Target);
            Assert.Contains($"N'{id}'", files[^1].Sql, StringComparison.Ordinal);
            Assert.All(files, file => Assert.Equal(file == files[^1], file.Sql.Contains("INSERT INTO [__EFMigrationsHistory]", StringComparison.Ordinal)));
            // 下一個 migration 的檔案，只有在這筆歷史列寫入之後才會開始。
            Assert.Equal(files[^1].Number, script.Files.Where(x => x.MigrationId == id).Max(x => x.Number));
            Assert.True(script.Files.Where(x => x.MigrationId is not null && string.CompareOrdinal(x.MigrationId, id) > 0).All(x => x.Number > files[^1].Number));
        }
    }

    [Fact]
    public void NumbersAreContinuousAndUniqueAcrossSchemaAndData()
    {
        var script = new ReleaseScriptGenerator(Context).Generate(Migration.InitialDatabase);
        Assert.Equal(Enumerable.Range(1, script.Files.Count).Select(x => x * ReleaseScriptGenerator.NumberStep), script.Files.Select(x => x.Number));
        Assert.Equal(script.Files.Count, script.Files.Select(x => x.Label).Distinct().Count());
        Assert.Single(script.Files.Select(x => x.Label.Length).Distinct());
        Assert.All(script.Files, file => Assert.Equal(file.Number, int.Parse(file.Label, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Contains(script.Files, x => x.Path == $"schema/0010_{ReleaseScriptGenerator.HistoryTarget}.sql");
    }

    [Fact]
    public void NoMigrationRebuildsATableAndColumnsAreAddedInPlace()
    {
        using var db = Context();
        Assert.All(Migrations(db), migration => Assert.Empty(ReleaseScriptGenerator.FindTableRebuilds(migration.UpOperations)));
        var script = new ReleaseScriptGenerator(Context).Generate(Migration.InitialDatabase);
        var added = script.Files.SelectMany(file => file.Operations.OfType<AddColumnOperation>().Select(column => (file, column))).ToList();
        Assert.NotEmpty(added);
        Assert.All(added, x => Assert.Contains($"ALTER TABLE [{x.column.Schema}].[{x.column.Table}] ADD [{x.column.Name}]", x.file.Sql, StringComparison.Ordinal));
        Assert.All(script.Files, file => Assert.DoesNotContain("ef_temp", file.Sql, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OperationsThatRewriteAnExistingTableAreReported()
    {
        var created = new CreateTableOperation { Schema = "knowledge", Name = "Fresh" };
        var clusteredOnNew = new CreateIndexOperation { Schema = "knowledge", Table = "Fresh", Name = "IX_Fresh", Columns = ["At"] };
        clusteredOnNew.AddAnnotation("SqlServer:Clustered", true);
        var clusteredOnExisting = new CreateIndexOperation { Schema = "knowledge", Table = "Chunks", Name = "IX_Chunks", Columns = ["At"] };
        clusteredOnExisting.AddAnnotation("SqlServer:Clustered", true);
        var nonclusteredKey = new AddPrimaryKeyOperation { Schema = "audit", Table = "Other", Name = "PK_Other", Columns = ["Id"] };
        nonclusteredKey.AddAnnotation("SqlServer:Clustered", false);
        MigrationOperation[] operations =
        [
            created, clusteredOnNew, clusteredOnExisting, nonclusteredKey,
            new AddColumnOperation { Schema = "knowledge", Table = "Chunks", Name = "Added", ClrType = typeof(int) },
            new DropPrimaryKeyOperation { Schema = "audit", Table = "AuditEvents", Name = "PK_AuditEvents" },
            new AddPrimaryKeyOperation { Schema = "audit", Table = "AuditEvents", Name = "PK_AuditEvents", Columns = ["Id"] },
        ];
        var found = ReleaseScriptGenerator.FindTableRebuilds(operations);
        Assert.Equal(3, found.Count);
        Assert.StartsWith("knowledge.Chunks（CreateIndexOperation）", found[0], StringComparison.Ordinal);
        Assert.StartsWith("audit.AuditEvents（DropPrimaryKeyOperation）", found[1], StringComparison.Ordinal);
        Assert.StartsWith("audit.AuditEvents（AddPrimaryKeyOperation）", found[2], StringComparison.Ordinal);
    }

    [Fact]
    public void TransactionSuppressedSqlIsMarkedAndKeptOutOfTransactions()
    {
        var script = new ReleaseScriptGenerator(Context).Generate(Migration.InitialDatabase);
        var fullText = Assert.Single(script.Files, x => x.OutsideTransaction && x.Operations.Count > 0);
        Assert.Contains("CREATE FULLTEXT", fullText.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN TRANSACTION", fullText.Sql, StringComparison.Ordinal);
        Assert.Contains("交易外", fullText.Header, StringComparison.Ordinal);
        Assert.Contains($"`{fullText.Path}` | 交易外 |", script.Manifest(), StringComparison.Ordinal);
        // 與 EF 相同，含有此類陳述式的 migration，其歷史列同樣不在交易中寫入。
        var row = script.Files.Last(x => x.MigrationId == fullText.MigrationId);
        Assert.True(row.OutsideTransaction);
        Assert.DoesNotContain("BEGIN TRANSACTION", row.Sql, StringComparison.Ordinal);
        Assert.All(script.Files.Where(x => !x.OutsideTransaction && x.MigrationId is not null), file =>
        {
            Assert.StartsWith("BEGIN TRANSACTION;", file.Sql, StringComparison.Ordinal);
            Assert.EndsWith("COMMIT;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine, file.Sql, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void HandWrittenDataSqlIsFiledAsData()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        builder.DataSql("UPDATE [accesscontrol].[Features] SET [SortOrder] = 1;", "accesscontrol.Features");
        builder.Sql("ALTER TABLE [knowledge].[Chunks] REBUILD;");
        Assert.Equal(ReleaseScriptKind.Data, ReleaseScriptGenerator.Classify(builder.Operations[0]));
        Assert.Equal("accesscontrol.Features", ReleaseScriptGenerator.TargetOf(builder.Operations[0]));
        Assert.Equal(ReleaseScriptKind.Schema, ReleaseScriptGenerator.Classify(builder.Operations[1]));
        Assert.Equal("sql", ReleaseScriptGenerator.TargetOf(builder.Operations[1]));
    }

    [Fact]
    public void HandWrittenDataSqlCompilesWhenItRuns()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        builder.DataSql("UPDATE [repositories].[RepositoryReviews] SET [Purpose] = N'review';", "repositories.RepositoryReviews");
        Assert.Equal("EXEC(N'UPDATE [repositories].[RepositoryReviews] SET [Purpose] = N''review'';');", ((SqlOperation)builder.Operations[0]).Sql);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("NoSuchMigration", null)]
    [InlineData("0", "NoSuchMigration")]
    [InlineData("UsageDateIndexes", "RepositoriesWorkspace")]
    public void InvalidRangeIsRejected(string from, string? to)
    {
        var error = Assert.Throws<ReleaseScriptException>(() => new ReleaseScriptGenerator(Context).Generate(from, to));
        Assert.Matches("--from|--to", error.Message);
    }

    [Fact]
    public void ManifestListsEveryFileInOrderAndOutputMustBeEmpty()
    {
        using var db = Context();
        var ids = db.Database.GetMigrations().ToList();
        var script = new ReleaseScriptGenerator(Context).Generate(ids[^3]);
        var directory = Path.Combine(Path.GetTempPath(), "release-script-" + Guid.NewGuid().ToString("N"));
        try
        {
            script.WriteTo(directory);
            var manifest = File.ReadAllText(Path.Combine(directory, ReleaseScript.ManifestFileName));
            var rows = manifest.Split('\n').Where(x => x.StartsWith("| 0", StringComparison.Ordinal)).ToList();
            Assert.Equal(script.Files.Select(x => x.Label), rows.Select(x => x.Split('|')[1].Trim()));
            Assert.All(script.Files, file => Assert.True(File.Exists(Path.Combine(directory, file.Path)), file.Path));
            Assert.Equal(script.Files.Count, Directory.EnumerateFiles(directory, "*.sql", SearchOption.AllDirectories).Count());
            Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(Path.Combine(directory, script.Files[1].Path))[..3]);
            Assert.Contains($"`{ids[^3]}` 之後到 `{ids[^1]}`，共 2 個 migration", manifest, StringComparison.Ordinal);
            Assert.Throws<ReleaseScriptException>(() => script.WriteTo(directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static NexusDbContext Context() => new DesignTimeDbContextFactory().CreateDbContext([]);

    private static string Reference(NexusDbContext db, string from, string? to)
        => db.GetService<IMigrator>().GenerateScript(from, to, MigrationsSqlGenerationOptions.Idempotent);

    private static IEnumerable<Migration> Migrations(NexusDbContext db)
    {
        var assembly = db.GetService<IMigrationsAssembly>();
        return assembly.Migrations.Values.Select(type => assembly.CreateMigration(type, db.Database.ProviderName!));
    }

    private static bool IsSeed(MigrationOperation operation) => operation is InsertDataOperation or UpdateDataOperation or DeleteDataOperation;

    /// <summary>DBA 實際收到的檔案內容：移除檔頭註解後，依編號順序串接。</summary>
    private static string WrittenFiles(ReleaseScript script)
    {
        var directory = Path.Combine(Path.GetTempPath(), "release-script-" + Guid.NewGuid().ToString("N"));
        try
        {
            script.WriteTo(directory);
            return string.Concat(script.Files.OrderBy(x => x.Number).Select(file =>
                string.Join('\n', File.ReadAllLines(Path.Combine(directory, file.Path)).SkipWhile(line => line.StartsWith("-- ", StringComparison.Ordinal))) + "\n"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>
    /// 依序列出陳述式並壓縮空白：包含歷史表檢查，以及 EF 包在歷史條件中的每個指令。
    /// 交易陳述式與批次分隔符會被移除，因為 EF 每個 migration 只開一個交易（與一個批次），
    /// 而每個發佈檔案各自開啟自己的交易。
    /// </summary>
    private static List<string> Statements(string script)
    {
        var lines = script.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(x => x.Trim() is not ("BEGIN TRANSACTION;" or "COMMIT;" or "GO"));
        var text = Whitespace().Replace(string.Join(' ', lines), " ").Trim();
        return [.. Statement().Split(text).Select(x => x.Trim()).Where(x => x.Length > 0)];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?=IF NOT EXISTS \( SELECT \* FROM \[__EFMigrationsHistory\])")]
    private static partial Regex Statement();
}
