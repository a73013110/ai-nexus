using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>
/// Splits the idempotent migration script into numbered files a DBA runs one at a time. Consecutive operations of the
/// same kind (<see cref="ReleaseScriptKind"/>) on the same object share a file; operations are never reordered, because
/// EF already put them in dependency order. The SQL comes from the context's own <see cref="IMigrationsSqlGenerator"/>
/// and history repository with the migration's target model, exactly as <c>dotnet ef migrations script --idempotent</c>
/// generates it and without a database connection; only the transaction boundaries move, to one per file.
/// </summary>
/// <param name="createContext">
/// Creates a SQL Server context; it is never opened. Two are used: one generates the files, a fresh one the whole
/// migrations as EF does, and they must agree. The generator numbers its variables (<c>@description12</c>) across calls,
/// so only a generator that saw the same calls in the same order gives the same text.
/// </param>
public sealed class ReleaseScriptGenerator(Func<DbContext> createContext)
{
    public const int NumberStep = 10;

    public static readonly string HistoryTarget = "dbo." + HistoryRepository.DefaultTableName;

    private const MigrationsSqlGenerationOptions Options = MigrationsSqlGenerationOptions.Script | MigrationsSqlGenerationOptions.Idempotent;

    /// <param name="from">The production database's last applied migration (id or name), or <c>0</c> for an empty database.</param>
    /// <param name="to">The last migration to include; <see langword="null"/> for the latest.</param>
    /// <exception cref="ReleaseScriptException">The range is invalid, or a migration in it cannot be delivered file by file.</exception>
    public ReleaseScript Generate(string from, string? to = null)
    {
        using var db = createContext();
        using var reference = createContext();
        var services = new Services(db.GetService<IMigrationsSqlGenerator>(), reference.GetService<IMigrationsSqlGenerator>(),
            db.GetService<IHistoryRepository>(), db.GetService<ISqlGenerationHelper>(), db.GetService<IModelRuntimeInitializer>());
        var history = services.History;
        var sql = services.Sql;
        var assembly = db.GetService<IMigrationsAssembly>();
        var ids = assembly.Migrations.Keys.ToList();
        var fromId = from == Migration.InitialDatabase ? null : Resolve(assembly, from, "--from");
        if (to == Migration.InitialDatabase) throw new ReleaseScriptException("--to 不能是 0；拆檔只產生升級 SQL。");
        var toId = to is null ? ids.LastOrDefault() : Resolve(assembly, to, "--to");
        if (fromId is not null && toId is not null && Compare(toId, fromId) < 0)
            throw new ReleaseScriptException($"--to {toId} 早於 --from {fromId}；拆檔只產生升級 SQL，不產生降版 SQL。");
        var range = ids.Where(id => (fromId is null || Compare(id, fromId) > 0) && toId is not null && Compare(id, toId) <= 0).ToList();

        // Like EF's script, a range from an empty database starts by creating the history table every later file checks.
        var drafts = new List<Draft>();
        if (fromId is null && range.Count > 0) drafts.Add(new(ReleaseScriptKind.Schema, HistoryTarget, null, false, history.GetCreateIfNotExistsScript() + sql.BatchTerminator, []));
        foreach (var id in range) drafts.AddRange(Split(services, id, assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!)));

        var width = Math.Max(4, (drafts.Count * NumberStep).ToString(CultureInfo.InvariantCulture).Length);
        var files = drafts.Select((draft, index) =>
        {
            var number = (index + 1) * NumberStep;
            var label = number.ToString("D" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            var folder = draft.Kind == ReleaseScriptKind.Data ? "data" : "schema";
            return new ReleaseScriptFile(number, draft.Kind, draft.Target, draft.MigrationId, draft.OutsideTransaction,
                $"{folder}/{label}_{FileName(draft.Target)}.sql", draft.Sql, draft.Operations);
        }).ToList();
        return new ReleaseScript(fromId, toId, range, files);
    }

    /// <summary>Data: EF seed operations (<c>HasData</c>) and raw SQL marked by <see cref="MigrationBuilderExtensions.DataSql"/>.</summary>
    public static ReleaseScriptKind Classify(MigrationOperation operation) => operation switch
    {
        InsertDataOperation or UpdateDataOperation or DeleteDataOperation => ReleaseScriptKind.Data,
        SqlOperation raw when raw[MigrationBuilderExtensions.KindAnnotation] is MigrationBuilderExtensions.DataKind => ReleaseScriptKind.Data,
        _ => ReleaseScriptKind.Schema,
    };

    /// <summary>The object an operation changes: <c>schema.Table</c>, the schema itself, or the operation name.</summary>
    public static string TargetOf(MigrationOperation operation, string defaultSchema = "dbo") => operation switch
    {
        SqlOperation raw => raw[MigrationBuilderExtensions.ObjectAnnotation] as string ?? "sql",
        ITableMigrationOperation { Table.Length: > 0 } table => $"{table.Schema ?? defaultSchema}.{table.Table}",
        EnsureSchemaOperation schema => schema.Name,
        DropSchemaOperation schema => schema.Name,
        CreateSequenceOperation sequence => $"{sequence.Schema ?? defaultSchema}.{sequence.Name}",
        AlterSequenceOperation sequence => $"{sequence.Schema ?? defaultSchema}.{sequence.Name}",
        DropSequenceOperation sequence => $"{sequence.Schema ?? defaultSchema}.{sequence.Name}",
        RenameSequenceOperation sequence => $"{sequence.Schema ?? defaultSchema}.{sequence.Name}",
        RestartSequenceOperation sequence => $"{sequence.Schema ?? defaultSchema}.{sequence.Name}",
        _ => operation.GetType().Name.Replace("Operation", "", StringComparison.Ordinal),
    };

    /// <summary>
    /// Operations that make SQL Server rewrite a table that already holds rows, which a DBA delivering ALTERs object by
    /// object must not receive silently. Adding a column is always <c>ALTER TABLE … ADD</c> and is never listed. A
    /// clustered key or index on a table created by the same migration is built while the table is still empty.
    /// </summary>
    /// <returns>One line per offending operation; empty when the migration only alters in place.</returns>
    public static IReadOnlyList<string> FindTableRebuilds(IReadOnlyList<MigrationOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var fresh = operations.OfType<CreateTableOperation>().Select(x => TargetOf(x))
            .Concat(operations.OfType<DropTableOperation>().Select(x => TargetOf(x))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        foreach (var operation in operations)
        {
            var existing = !fresh.Contains(TargetOf(operation));
            var reason = operation switch
            {
                AddPrimaryKeyOperation key when existing && key[ClusteredAnnotation] is not false => "在既有表新增或變更叢集主鍵，SQL Server 會依新鍵重寫整張表",
                DropPrimaryKeyOperation when existing => "移除既有表的主鍵（預設為叢集），SQL Server 會把整張表改寫成 heap",
                CreateIndexOperation index when existing && index[ClusteredAnnotation] is true => "在既有表建立叢集索引，SQL Server 會重寫整張表",
                AlterTableOperation table when !Equals(table[MemoryOptimizedAnnotation], table.OldTable[MemoryOptimizedAnnotation]) => "切換記憶體最佳化資料表必須重建表",
                AlterColumnOperation column when !Equals(column[IdentityAnnotation], column.OldColumn[IdentityAnnotation]) => "SQL Server 不能以 ALTER 變更 IDENTITY，必須重建欄位或表",
                _ => null,
            };
            if (reason is not null) found.Add($"{TargetOf(operation)}（{operation.GetType().Name}）：{reason}");
        }
        return found;
    }

    // Provider annotation names (SqlServerAnnotationNames is internal to EF).
    private const string ClusteredAnnotation = "SqlServer:Clustered";
    private const string MemoryOptimizedAnnotation = "SqlServer:MemoryOptimized";
    private const string IdentityAnnotation = "SqlServer:Identity";

    private static IEnumerable<Draft> Split(Services services, string id, Migration migration)
    {
        var operations = migration.UpOperations;
        var rebuilds = FindTableRebuilds(operations);
        if (rebuilds.Count > 0)
            throw new ReleaseScriptException($"{id} 含會在 SQL Server 重建資料表的操作，不能以逐物件的 ALTER 交付；請改寫 migration 或另與 DBA 規劃停機搬移：{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", rebuilds)}");

        // Migrator.GenerateScript finalizes the target model the same way.
        var model = migration.TargetModel is { } targetModel ? services.Initializer.Initialize(targetModel, designTime: true, validationLogger: null) : null;
        var defaultSchema = model?.GetDefaultSchema() ?? "dbo";
        var groups = new List<Group>();
        foreach (var operation in operations)
        {
            var kind = Classify(operation);
            var target = TargetOf(operation, defaultSchema);
            var suppressed = operation is SqlOperation { SuppressTransaction: true };
            if (groups.Count > 0 && groups[^1].Kind == kind && groups[^1].Target == target && groups[^1].Suppressed == suppressed) groups[^1].Operations.Add(operation);
            else groups.Add(new(kind, target, suppressed, [operation]));
        }

        // The files must say exactly what `dotnet ef migrations script` says for the whole migration; a generator that
        // depended on the other operations of the migration would fail here, loudly, instead of shipping different SQL.
        var parts = groups.Select(group => (Group: group, Commands: services.Generator.Generate(group.Operations, model, Options))).ToList();
        var split = parts.SelectMany(x => x.Commands).Select(x => (x.CommandText, x.TransactionSuppressed));
        var whole = services.Reference.Generate(operations, model, Options).Select(x => (x.CommandText, x.TransactionSuppressed));
        if (!split.SequenceEqual(whole))
            throw new ReleaseScriptException($"{id} 依物件分段產生的 SQL 與整段產生的不同，無法安全拆檔；請改用套件的 migrations.sql。");

        foreach (var (group, commands) in parts.Where(x => x.Commands.Count > 0))
            yield return new(group.Kind, group.Target, id, commands.Any(x => x.TransactionSuppressed), Render(services, id, commands.Select(x => (x.CommandText, x.TransactionSuppressed))), group.Operations);
        // EF writes the history row outside a transaction too when any command of the migration is, so a failure cannot
        // leave the row committed without the work.
        var outside = parts.SelectMany(x => x.Commands).Any(x => x.TransactionSuppressed);
        var row = services.History.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion()));
        yield return new(ReleaseScriptKind.Schema, HistoryTarget, id, outside, Render(services, id, [(row, outside)]), []);
    }

    /// <summary>EF's script layout (Migrator.GenerateScript) for the commands of one file, with its own transaction.</summary>
    private static string Render(Services services, string id, IEnumerable<(string Text, bool Suppressed)> commands)
    {
        var (history, sql) = (services.History, services.Sql);
        var builder = new IndentedStringBuilder();
        var inTransaction = false;
        foreach (var (text, suppressed) in commands)
        {
            if (!inTransaction && !suppressed)
            {
                builder.AppendLine(sql.StartTransactionStatement);
                inTransaction = true;
            }
            if (inTransaction && suppressed)
            {
                builder.AppendLine(sql.CommitTransactionStatement).Append(sql.BatchTerminator);
                inTransaction = false;
            }
            builder.AppendLine(history.GetBeginIfNotExistsScript(id));
            using (builder.Indent()) builder.AppendLines(text);
            builder.Append(history.GetEndIfScript());
            // As in EF's script: commands inside a transaction share one batch; GO only where none is open.
            builder.Append(inTransaction ? Environment.NewLine : sql.BatchTerminator);
        }
        if (inTransaction) builder.AppendLine(sql.CommitTransactionStatement).Append(sql.BatchTerminator);
        return builder.ToString();
    }

    private static string Resolve(IMigrationsAssembly assembly, string nameOrId, string option)
        => assembly.FindMigrationId(nameOrId)
            ?? throw new ReleaseScriptException($"{option} 找不到 migration：{nameOrId}。請用 migration id（例如 20261009225825_InitialCreate）或名稱；空庫用 0。");

    // EF orders migration ids the same way.
    private static int Compare(string left, string right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

    private static string FileName(string target)
        => string.Concat(target.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private sealed record Services(IMigrationsSqlGenerator Generator, IMigrationsSqlGenerator Reference, IHistoryRepository History, ISqlGenerationHelper Sql, IModelRuntimeInitializer Initializer);

    private sealed record Group(ReleaseScriptKind Kind, string Target, bool Suppressed, List<MigrationOperation> Operations);

    private sealed record Draft(ReleaseScriptKind Kind, string Target, string? MigrationId, bool OutsideTransaction, string Sql, IReadOnlyList<MigrationOperation> Operations);
}
