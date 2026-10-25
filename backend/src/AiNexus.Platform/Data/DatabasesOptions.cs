using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Data;

/// <summary>
/// 應用程式所有 SQL Server 連線設定的集合，以資料庫名稱為鍵（如 <c>Databases:App</c>）。
/// 模組必須用 <see cref="Sql.SqlDatabaseServices.AddDatabase"/> 宣告自己用到的資料庫；
/// 沒有被宣告的設定項目會讓啟動驗證失敗，避免鍵名拼錯時資料庫被悄悄略過。
/// </summary>
public sealed class DatabasesOptions() : Dictionary<string, DatabaseOptions>(StringComparer.OrdinalIgnoreCase), IValidatableObject
{
    public const string Section = "Databases";

    private readonly Dictionary<string, bool> declared = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> labels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>取得指定名稱的連線設定；沒有該名稱時回傳未設定的空白項目。</summary>
    public DatabaseOptions For(string name) => TryGetValue(name, out var database) ? database : new();

    /// <inheritdoc cref="DatabaseOptions.ConnectionString"/>
    public string ConnectionString(string name, string? catalog = null) => For(name).ConnectionString(catalog);

    /// <summary>宣告時給的顯示名稱（例如「公文系統 GD」），供監控等畫面使用；沒有給就是設定鍵名稱。</summary>
    public string Label(string name) => labels.GetValueOrDefault(name, name);

    /// <summary>模組宣告使用此資料庫；<paramref name="readOnly"/> 為 true 時，該設定必須明確設 <c>ReadOnly=true</c>。</summary>
    internal void Declare(string name, bool readOnly, string? label)
    {
        declared[name] = readOnly || declared.GetValueOrDefault(name);
        if (label is not null) labels[name] = label;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, database) in this)
        {
            var key = $"{Section}:{name}";
            if (!declared.TryGetValue(name, out var readOnly))
            {
                yield return new($"{key} is not a database of this application. Known: {string.Join(", ", declared.Keys.Order(StringComparer.Ordinal))}.", [key]);
                continue;
            }
            if (readOnly && !database.ReadOnly) yield return new($"{key}:{nameof(DatabaseOptions.ReadOnly)} must be true; this database is only read.", [key]);
            foreach (var error in database.Validate(key)) yield return error;
        }
    }
}

[OptionsValidator]
public sealed partial class DatabasesOptionsValidator : IValidateOptions<DatabasesOptions>;
