using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>
/// 把可重跑（idempotent）的 migration 腳本拆成編號檔案，讓 DBA 一次執行一個。同一物件上、同種類且連續的操作共用一個檔案。
/// 操作順序絕不重排，因為 EF 已依相依性排好。SQL 內容來自 <see cref="IMigrationsSqlGenerator"/> 與 history repository，
/// 搭配 migration 的目標模型產生，與 <c>dotnet ef migrations script --idempotent</c> 的結果一致，且不需要資料庫連線。
/// 唯一的差異是交易邊界：改為每個檔案各自一個交易。
/// </summary>
/// <param name="createContext">
/// 建立 SQL Server context，但不會真的開啟連線。會用到兩個：一個產生檔案，另一個依 EF 的方式整段產生，兩者結果必須一致。
/// 產生器會跨呼叫為變數編號（如 <c>@description12</c>），所以只有以相同順序呼叫的產生器，才會得到相同的文字。
/// </param>
public sealed class ReleaseScriptGenerator(Func<DbContext> createContext)
{
    public const int NumberStep = 10;

    public static readonly string HistoryTarget = "dbo." + HistoryRepository.DefaultTableName;

    private const MigrationsSqlGenerationOptions Options = MigrationsSqlGenerationOptions.Script | MigrationsSqlGenerationOptions.Idempotent;

    /// <param name="from">正式資料庫最後套用的 migration（id 或名稱）；空庫用 <c>0</c>。</param>
    /// <param name="to">要包含的最後一個 migration；<see langword="null"/> 表示最新。</param>
    /// <exception cref="ReleaseScriptException">範圍無效，或範圍內有 migration 無法逐檔交付。</exception>
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

        // 與 EF 的腳本一致：空庫起跑時，先建立之後每個檔案都會檢查的 history 表。
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

    /// <summary>資料檔：EF 的 seed 操作（<c>HasData</c>）與 <see cref="MigrationBuilderExtensions.DataSql"/> 標記的原生 SQL。其餘為結構。</summary>
    public static ReleaseScriptKind Classify(MigrationOperation operation) => operation switch
    {
        InsertDataOperation or UpdateDataOperation or DeleteDataOperation => ReleaseScriptKind.Data,
        SqlOperation raw when raw[MigrationBuilderExtensions.KindAnnotation] is MigrationBuilderExtensions.DataKind => ReleaseScriptKind.Data,
        _ => ReleaseScriptKind.Schema,
    };

    /// <summary>操作所變動的物件：<c>schema.Table</c>、schema 本身，或操作名稱。</summary>
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
    /// 找出會讓 SQL Server 重寫整張已有資料的表的操作。逐物件交付 ALTER 的 DBA 不能在不知情的情況下收到這類操作。
    /// 新增欄位永遠是 <c>ALTER TABLE … ADD</c>，不列入。同一個 migration 內新建的表，其叢集鍵或索引是在空表上建立，也不列入。
    /// </summary>
    /// <returns>每個有問題的操作一行；migration 只做原地 ALTER 時為空。</returns>
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

    // SQL Server provider 的 annotation 名稱。EF 的 SqlServerAnnotationNames 是 internal，只能在這裡自行定義。
    private const string ClusteredAnnotation = "SqlServer:Clustered";
    private const string MemoryOptimizedAnnotation = "SqlServer:MemoryOptimized";
    private const string IdentityAnnotation = "SqlServer:Identity";

    private static IEnumerable<Draft> Split(Services services, string id, Migration migration)
    {
        var operations = migration.UpOperations;
        var rebuilds = FindTableRebuilds(operations);
        if (rebuilds.Count > 0)
            throw new ReleaseScriptException($"{id} 含會在 SQL Server 重建資料表的操作，不能以逐物件的 ALTER 交付；請改寫 migration 或另與 DBA 規劃停機搬移：{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", rebuilds)}");

        // 與 Migrator.GenerateScript 一樣先定案目標模型，否則產生的 SQL 會與 dotnet ef 的結果不同。
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

        // 檔案內容必須與 `dotnet ef migrations script` 對整段 migration 的結果完全相同。
        // 若產生器依賴同一 migration 中的其他操作，這裡會直接失敗，而不是悄悄交付不同的 SQL。
        var parts = groups.Select(group => (Group: group, Commands: services.Generator.Generate(group.Operations, model, Options))).ToList();
        var split = parts.SelectMany(x => x.Commands).Select(x => (x.CommandText, x.TransactionSuppressed));
        var whole = services.Reference.Generate(operations, model, Options).Select(x => (x.CommandText, x.TransactionSuppressed));
        if (!split.SequenceEqual(whole))
            throw new ReleaseScriptException($"{id} 依物件分段產生的 SQL 與整段產生的不同，無法安全拆檔；請改用套件的 migrations.sql。");

        foreach (var (group, commands) in parts.Where(x => x.Commands.Count > 0))
            yield return new(group.Kind, group.Target, id, commands.Any(x => x.TransactionSuppressed), Render(services, id, commands.Select(x => (x.CommandText, x.TransactionSuppressed))), group.Operations);
        // 只要 migration 中有任何指令在交易外執行，EF 寫入 history 列時也會在交易外進行；這樣失敗時才不會出現「列已提交、工作卻沒完成」的情況。
        var outside = parts.SelectMany(x => x.Commands).Any(x => x.TransactionSuppressed);
        var row = services.History.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion()));
        yield return new(ReleaseScriptKind.Schema, HistoryTarget, id, outside, Render(services, id, [(row, outside)]), []);
    }

    /// <summary>依 EF 的腳本格式（Migrator.GenerateScript）輸出一個檔案的指令，每個檔案有自己的交易。</summary>
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
            // 與 EF 的腳本一致：交易內的指令共用同一批次；只有在沒有開啟交易時才加 GO。
            builder.Append(inTransaction ? Environment.NewLine : sql.BatchTerminator);
        }
        if (inTransaction) builder.AppendLine(sql.CommitTransactionStatement).Append(sql.BatchTerminator);
        return builder.ToString();
    }

    internal static string Resolve(IMigrationsAssembly assembly, string nameOrId, string option)
        => assembly.FindMigrationId(nameOrId)
            ?? throw new ReleaseScriptException($"{option} 找不到 migration：{nameOrId}。請用 migration id（例如 20261009225825_InitialCreate）或名稱；空庫用 0。");

    // 與 EF 排序 migration id 的方式相同（不分大小寫）。
    private static int Compare(string left, string right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

    private static string FileName(string target)
        => string.Concat(target.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private sealed record Services(IMigrationsSqlGenerator Generator, IMigrationsSqlGenerator Reference, IHistoryRepository History, ISqlGenerationHelper Sql, IModelRuntimeInitializer Initializer);

    private sealed record Group(ReleaseScriptKind Kind, string Target, bool Suppressed, List<MigrationOperation> Operations);

    private sealed record Draft(ReleaseScriptKind Kind, string Target, string? MigrationId, bool OutsideTransaction, string Sql, IReadOnlyList<MigrationOperation> Operations);
}
