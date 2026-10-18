using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AiNexus.Platform.Diagnostics;
using Dapper;

namespace AiNexus.Platform.Data.Sql;

/// <summary>以 Dapper 執行 <see cref="ISqlDatabase{TDatabase}"/> 命令的基底類別。連線來源（共用的 DbContext 連線或獨立連線）由子類別決定。</summary>
/// <param name="audit">寫入 procedure 的稽核情境來源；只做查詢的測試可省略，此時呼叫寫入 procedure 會失敗。</param>
public abstract partial class DapperSqlDatabase<TDatabase>(AuditContextSource? audit) : ISqlDatabase<TDatabase>
{
    // 每次都設定全部鍵，沒有值就設 NULL：同一條連線（EF 交易中、或連線池重用）上一次的值不能殘留到這次（ADR 0014）。
    private const string AuditBatch = """
        EXEC sys.sp_set_session_context @key = N'audit.owner_id', @value = @__AuditOwnerId;
        EXEC sys.sp_set_session_context @key = N'audit.actor_id', @value = @__AuditActorId;
        EXEC sys.sp_set_session_context @key = N'audit.trace_id', @value = @__AuditTraceId;
        EXEC sys.sp_set_session_context @key = N'audit.operation_id', @value = @__AuditOperationId;
        EXEC sys.sp_set_session_context @key = N'audit.at', @value = @__AuditAt;

        """;

    /// <summary>在已開啟的連線上執行 <paramref name="command"/>，並帶入目前應加入的交易（若有）。</summary>
    protected abstract Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken);

    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<T>>(async (connection, transaction) =>
            (await connection.QueryAsync<T>(Command(sql, parameters, transaction, commandTimeout, cancellationToken))).AsList(), cancellationToken);

    public Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync((connection, transaction) => connection.QuerySingleAsync<T>(Command(sql, parameters, transaction, commandTimeout, cancellationToken)), cancellationToken);

    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync((connection, transaction) => connection.QuerySingleOrDefaultAsync<T?>(Command(sql, parameters, transaction, commandTimeout, cancellationToken)), cancellationToken);

    public Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync((connection, transaction) => connection.ExecuteAsync(Command(sql, parameters, transaction, commandTimeout, cancellationToken)), cancellationToken);

    public Task<IReadOnlyList<T>> QueryProcedureAsync<T>(string procedure, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<T>>(async (connection, transaction) =>
            (await connection.QueryAsync<T>(Command(Name(procedure), parameters, transaction, commandTimeout, cancellationToken, CommandType.StoredProcedure))).AsList(), cancellationToken);

    public Task<TResult> QueryProcedureAsync<TResult>(string procedure, object? parameters, Func<SqlResults, Task<TResult>> read, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync(async (connection, transaction) =>
        {
            await using var grid = await connection.QueryMultipleAsync(Command(Name(procedure), parameters, transaction, commandTimeout, cancellationToken, CommandType.StoredProcedure));
            return await read(new SqlResults(grid));
        }, cancellationToken);

    public Task<IReadOnlyList<T>> ExecuteProcedureAsync<T>(string procedure, object? parameters = null, Guid? auditOwner = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        var source = audit ?? throw new InvalidOperationException("寫入 procedure 需要 AuditContextSource。");
        // sp_set_session_context 與 EXEC 要在同一個批次（一次往返），所以不能用 CommandType.StoredProcedure；
        // 參數名稱逐一寫進 EXEC，只接受識別字，procedure 名稱也先驗證過，批次文字不含任何外部輸入。
        var arguments = new DynamicParameters(parameters);
        var names = Names(parameters);
        var context = source.Capture(auditOwner);
        arguments.Add("__AuditOwnerId", context.OwnerId, DbType.Guid);
        arguments.Add("__AuditActorId", context.ActorId, DbType.Guid);
        arguments.Add("__AuditTraceId", context.TraceId, DbType.String, size: 32);
        arguments.Add("__AuditOperationId", context.OperationId, DbType.Guid);
        arguments.Add("__AuditAt", context.At, DbType.DateTimeOffset);
        var batch = new StringBuilder(AuditBatch).Append("EXEC ").Append(Name(procedure));
        for (var i = 0; i < names.Count; i++) batch.Append(i == 0 ? " " : ", ").Append('@').Append(names[i]).Append(" = @").Append(names[i]);
        batch.Append(';');
        return RunAsync<IReadOnlyList<T>>(async (connection, transaction) =>
            (await connection.QueryAsync<T>(Command(batch.ToString(), arguments, transaction, commandTimeout, cancellationToken))).AsList(), cancellationToken);
    }

    private static CommandDefinition Command(string sql, object? parameters, DbTransaction? transaction, int? commandTimeout, CancellationToken cancellationToken, CommandType type = CommandType.Text)
        => new(sql, parameters, transaction, commandTimeout, type, cancellationToken: cancellationToken);

    private static string Name(string procedure) => ProcedureName().IsMatch(procedure)
        ? procedure
        : throw new ArgumentException($"procedure 名稱必須是 schema.usp_<UseCase>：{procedure}", nameof(procedure));

    /// <summary>與 Dapper 相同的取名規則：<c>DynamicParameters</c> 取明確加入的參數，其他物件取公開的可讀屬性。</summary>
    private static List<string> Names(object? parameters)
    {
        var names = parameters switch
        {
            null => [],
            DynamicParameters dynamic => dynamic.ParameterNames.ToList(),
            _ => parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.CanRead && x.GetIndexParameters().Length == 0).Select(x => x.Name).ToList(),
        };
        foreach (var name in names)
            if (!ParameterName().IsMatch(name) || name.StartsWith("__", StringComparison.Ordinal))
                throw new ArgumentException($"procedure 參數名稱不合法：{name}", nameof(parameters));
        return names;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9]*\.usp_[A-Za-z0-9]+$")]
    private static partial Regex ProcedureName();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ParameterName();
}
