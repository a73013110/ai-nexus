using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>One numbered release SQL file.</summary>
/// <param name="Number">Execution order, shared by schema/ and data/ and spaced by <see cref="ReleaseScriptGenerator.NumberStep"/>.</param>
/// <param name="Kind">The folder it goes to and the DBA who runs it.</param>
/// <param name="Target">The object it changes: <c>schema.Table</c>, a schema name, or the operation name.</param>
/// <param name="MigrationId">The migration it belongs to; <see langword="null"/> for the shared history-table file.</param>
/// <param name="OutsideTransaction">It holds statements that SQL Server cannot run inside a transaction.</param>
/// <param name="Path">Relative path with forward slashes, for example <c>schema/0420_knowledge.Chunks.sql</c>.</param>
/// <param name="Sql">The batches as EF generates them, without the header comment.</param>
/// <param name="Operations">The migration operations it was generated from; empty for history rows.</param>
public sealed record ReleaseScriptFile(
    int Number,
    ReleaseScriptKind Kind,
    string Target,
    string? MigrationId,
    bool OutsideTransaction,
    string Path,
    string Sql,
    IReadOnlyList<MigrationOperation> Operations)
{
    public string Folder => Kind == ReleaseScriptKind.Data ? "data" : "schema";

    /// <summary>The zero-padded number as it appears in the file name.</summary>
    public string Label => Path[(Folder.Length + 1)..Path.IndexOf('_', StringComparison.Ordinal)];

    /// <summary>The comment lines written above <see cref="Sql"/>.</summary>
    public string Header
    {
        get
        {
            var header = $"-- {Label}｜{Folder}｜{Target}｜{MigrationId ?? "共用"}{Environment.NewLine}";
            return OutsideTransaction ? header + "-- 交易外執行：含不能在交易內執行的語句，不要包進 BEGIN TRANSACTION。" + Environment.NewLine : header;
        }
    }
}
