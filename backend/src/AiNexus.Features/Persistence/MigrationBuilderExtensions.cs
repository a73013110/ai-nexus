using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace AiNexus.Features.Persistence;

/// <summary>
/// Helpers for hand-written migration code. The release SQL (<c>db script</c>) files every operation as schema or data;
/// EF's own seed operations are data by type, but raw SQL is schema unless it carries <see cref="KindAnnotation"/>, so
/// hand-written SQL that changes rows must go through <see cref="DataSql"/>.
/// </summary>
public static class MigrationBuilderExtensions
{
    /// <summary>Annotation on a <see cref="SqlOperation"/> naming its kind; <see cref="DataKind"/> files it under data/.</summary>
    public const string KindAnnotation = "Nexus:Kind";

    public const string DataKind = "data";

    /// <summary>Annotation naming the object (<c>schema.Table</c>) a raw SQL operation changes, used for its file name.</summary>
    public const string ObjectAnnotation = "Nexus:Object";

    /// <summary>
    /// Raw SQL that inserts, updates or deletes rows; <paramref name="target"/> (<c>schema.Table</c>) names its release SQL
    /// file. Migrations live in a child namespace, so they call it without a using.
    /// </summary>
    public static OperationBuilder<SqlOperation> DataSql(this MigrationBuilder migrationBuilder, string sql, string? target = null, bool suppressTransaction = false)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        var operation = migrationBuilder.Sql(sql, suppressTransaction).Annotation(KindAnnotation, DataKind);
        return target is null ? operation : operation.Annotation(ObjectAnnotation, target);
    }
}
