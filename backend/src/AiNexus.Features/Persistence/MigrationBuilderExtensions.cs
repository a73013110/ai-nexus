using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace AiNexus.Features.Persistence;

/// <summary>手寫 migration 用的輔助方法。</summary>
/// <remarks>
/// 發布腳本會依操作種類分到 schema 或 data 檔案。EF 自己產生的 seed 操作本身就是 data，但手寫的原生 SQL 預設視為 schema，
/// 因此會異動資料列的手寫 SQL 必須透過 <see cref="DataSql"/>，並帶上 <see cref="KindAnnotation"/>，否則會被歸入錯誤的檔案。
/// </remarks>
public static class MigrationBuilderExtensions
{
    /// <summary>標在 <see cref="SqlOperation"/> 上的種類註記；值為 <see cref="DataKind"/> 時歸入 data/ 目錄。</summary>
    public const string KindAnnotation = "Nexus:Kind";

    public const string DataKind = "data";

    /// <summary>標在原生 SQL 操作上的目標物件（<c>schema.Table</c>），用來決定發布檔案的名稱。</summary>
    public const string ObjectAnnotation = "Nexus:Object";

    /// <summary>
    /// 新增、更新或刪除資料列的原生 SQL；<paramref name="target"/>（<c>schema.Table</c>）決定發布檔案的名稱。
    /// </summary>
    /// <remarks>
    /// SQL 會包在 <c>EXEC</c> 中執行。可重跑的腳本會把整個 migration 放進同一個批次，SQL Server 會在執行前先編譯整批，
    /// 若同一個 migration 前面才新增的欄位在這裡被引用，編譯就會失敗（Invalid column name）。
    /// 用 <c>EXEC</c> 則是執行時才編譯。因此這段 SQL 仍是同一批次，但不可包含 <c>GO</c>。
    /// </remarks>
    public static OperationBuilder<SqlOperation> DataSql(this MigrationBuilder migrationBuilder, string sql, string? target = null, bool suppressTransaction = false)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        var operation = migrationBuilder.Sql(Deferred(sql), suppressTransaction).Annotation(KindAnnotation, DataKind);
        return target is null ? operation : operation.Annotation(ObjectAnnotation, target);
    }

    /// <summary>以 <c>EXEC</c> 包住 <paramref name="sql"/>，讓 SQL Server 在執行時才編譯，而不是隨整個批次編譯。</summary>
    public static string Deferred(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return $"EXEC(N'{sql.Replace("'", "''", StringComparison.Ordinal)}');";
    }
}
