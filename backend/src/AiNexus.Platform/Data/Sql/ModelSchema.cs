using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// 唯讀比對 EF 模型與資料庫的實際結構（<c>sys.columns</c>）：表與檢視、欄位、store type（含長度、精度、向量維度）與 nullable。
/// 資料庫優先時 EF 只做對應（ADR 0011）；整合測試以 dacpac 建立的資料庫比對，啟動檢查對正式庫做同樣的比對。
/// </summary>
public static class ModelSchema
{
    /// <summary>回傳所有差異，依物件名稱排序；沒有差異時為空。</summary>
    /// <remarks>讀 SQL Server 2025 才有的 <c>vector_dimensions</c> 欄位，較舊的版本會失敗。</remarks>
    public static async Task<IReadOnlyList<SchemaDifference>> CompareAsync<TContext>(TContext context, ISqlDatabase<TContext> sql, CancellationToken ct)
        where TContext : DbContext
    {
        var expected = Model(context);
        var actual = await DatabaseAsync(sql, ct);
        return [.. Compare(expected, actual)];
    }

    private static Dictionary<string, Shape> Model(DbContext context)
    {
        // design-time model 保留完整的關聯對應；runtime model 會裁掉部分只給設計工具用的註記。
        var relational = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var result = new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in relational.Tables.Cast<ITableBase>().Concat(relational.Views))
        {
            result[Key(table.Schema, table.Name)] = new(table is IView ? "VIEW" : "TABLE",
                table.Columns.ToDictionary(c => c.Name, c => new Column(Normalize(c.StoreType), c.IsNullable, Required: false), StringComparer.OrdinalIgnoreCase));
        }
        return result;
    }

    private static async Task<Dictionary<string, Shape>> DatabaseAsync<TContext>(ISqlDatabase<TContext> sql, CancellationToken ct)
    {
        var rows = await sql.QueryAsync<Row>("""
            SELECT s.[name] AS [Schema], o.[name] AS [Object], o.[type] AS [Type], c.[name] AS [Column], t.[name] AS [TypeName],
                   c.[max_length] AS [MaxLength], c.[precision] AS [Precision], c.[scale] AS [Scale], c.[is_nullable] AS [Nullable],
                   CONVERT(int, c.[vector_dimensions]) AS [VectorDimensions], c.[vector_base_type_desc] AS [VectorBase],
                   CONVERT(bit, CASE WHEN c.[default_object_id] <> 0 OR c.[is_identity] = 1 OR c.[is_computed] = 1 OR t.[name] = 'timestamp' THEN 1 ELSE 0 END) AS [Generated]
            FROM sys.objects o
            JOIN sys.schemas s ON s.[schema_id] = o.[schema_id]
            JOIN sys.columns c ON c.[object_id] = o.[object_id]
            JOIN sys.types t ON t.[user_type_id] = c.[user_type_id]
            WHERE o.[type] IN ('U', 'V') AND o.[is_ms_shipped] = 0
            ORDER BY s.[name], o.[name], c.[column_id]
            """, cancellationToken: ct);
        var result = new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = Key(row.Schema, row.Object);
            if (!result.TryGetValue(key, out var shape))
            {
                result[key] = shape = new(row.Type.Trim() == "V" ? "VIEW" : "TABLE", new(StringComparer.OrdinalIgnoreCase));
            }
            shape.Columns[row.Column] = new(Render(row), row.Nullable, Required: !row.Nullable && !row.Generated);
        }
        return result;
    }

    private static IEnumerable<SchemaDifference> Compare(Dictionary<string, Shape> expected, Dictionary<string, Shape> actual)
    {
        foreach (var (name, table) in expected.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!actual.TryGetValue(name, out var database))
            {
                yield return new(SchemaDifferenceKind.MissingObject, name, $"EF 對應的 {table.Kind} 不存在", Blocking: true);
                continue;
            }
            foreach (var (column, shape) in table.Columns.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                var path = $"{name}.{column}";
                if (!database.Columns.TryGetValue(column, out var existing))
                    yield return new(SchemaDifferenceKind.MissingColumn, path, $"EF 對應的欄位不存在（EF：{shape}）", Blocking: true);
                else if (shape.StoreType != existing.StoreType)
                    yield return new(SchemaDifferenceKind.TypeMismatch, path, $"EF {shape.StoreType}，資料庫 {existing.StoreType}", Blocking: true);
                else if (shape.Nullable != existing.Nullable)
                    yield return new(SchemaDifferenceKind.NullabilityMismatch, path, $"EF {Nullability(shape.Nullable)}，資料庫 {Nullability(existing.Nullable)}", Blocking: true);
            }
            // EF 不知道的欄位：新增列時不會給值，NOT NULL 又沒有預設值就會失敗。
            foreach (var (column, existing) in database.Columns.Where(c => !table.Columns.ContainsKey(c.Key)).OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                yield return new(SchemaDifferenceKind.UnmappedColumn, $"{name}.{column}", $"資料庫多出的欄位（{existing}）", Blocking: existing.Required && table.Kind == "TABLE");
        }
        foreach (var name in actual.Keys.Where(k => !expected.ContainsKey(k)).Order(StringComparer.OrdinalIgnoreCase))
            yield return new(SchemaDifferenceKind.UnmappedObject, name, $"EF 沒有對應的 {actual[name].Kind}", Blocking: false);
    }

    private static string Key(string? schema, string name) => $"{schema ?? "dbo"}.{name}";
    private static string Nullability(bool nullable) => nullable ? "NULL" : "NOT NULL";

    /// <summary>把 <c>sys.columns</c> 還原成 EF 慣用的 store type 寫法，兩邊才能直接字串比對。</summary>
    private static string Render(Row row) => row.TypeName switch
    {
        "nvarchar" or "nchar" => $"{row.TypeName}({(row.MaxLength == -1 ? "max" : (row.MaxLength / 2).ToString(CultureInfo.InvariantCulture))})",
        "varchar" or "char" or "varbinary" or "binary" => $"{row.TypeName}({(row.MaxLength == -1 ? "max" : row.MaxLength.ToString(CultureInfo.InvariantCulture))})",
        "decimal" or "numeric" => $"decimal({row.Precision},{row.Scale})",
        "datetime2" or "datetimeoffset" or "time" => row.Scale == 7 ? row.TypeName : $"{row.TypeName}({row.Scale})",
        "float" => row.Precision == 53 ? "float" : $"float({row.Precision})",
        "vector" => row.VectorBase is null or "float32" ? $"vector({row.VectorDimensions})" : $"vector({row.VectorDimensions},{row.VectorBase})",
        "timestamp" => "rowversion",
        _ => row.TypeName,
    };

    /// <summary>EF 的 store type 寫法不唯一（大小寫、空白、預設精度），統一成 <see cref="Render"/> 的形式。</summary>
    private static string Normalize(string storeType)
    {
        var type = storeType.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
        return type switch
        {
            "float(53)" => "float",
            "decimal" or "numeric" => "decimal(18,0)",
            "timestamp" => "rowversion",
            _ when type.StartsWith("numeric(", StringComparison.Ordinal) => "decimal" + type[7..],
            _ when type.EndsWith("(7)", StringComparison.Ordinal) && (type.StartsWith("datetime2", StringComparison.Ordinal) || type.StartsWith("datetimeoffset", StringComparison.Ordinal) || type.StartsWith("time", StringComparison.Ordinal)) => type[..^3],
            _ => type,
        };
    }

    private sealed record Shape(string Kind, Dictionary<string, Column> Columns);

    /// <summary>一個欄位的 store type 與 nullable；<c>Required</c>＝資料庫欄位 NOT NULL，且沒有預設值、identity、計算或 rowversion 自動給值。</summary>
    private sealed record Column(string StoreType, bool Nullable, bool Required)
    {
        public override string ToString() => $"{StoreType} {Nullability(Nullable)}";
    }

    private sealed class Row
    {
        public string Schema { get; init; } = "";
        public string Object { get; init; } = "";
        public string Type { get; init; } = "";
        public string Column { get; init; } = "";
        public string TypeName { get; init; } = "";
        public short MaxLength { get; init; }
        public byte Precision { get; init; }
        public byte Scale { get; init; }
        public bool Nullable { get; init; }
        public int? VectorDimensions { get; init; }
        public string? VectorBase { get; init; }
        public bool Generated { get; init; }
    }
}
