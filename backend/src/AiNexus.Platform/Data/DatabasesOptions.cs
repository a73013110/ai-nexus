using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Data;

/// <summary>
/// 應用程式所有 SQL Server 連線設定集合字典，以資料庫名稱為鍵值（如 <c>Databases:Nexus</c>、<c>Databases:Gdweb</c>）。
/// 各業務模組必須主動宣告其所使用的資料庫名稱（<see cref="Sql.SqlDatabaseServices.AddDatabase"/>）；
/// 未經任何模組宣告的額外設定項目將導致啟動驗證失敗，防止因設定檔鍵名拼錯而導致資料庫靜默未配置。
/// </summary>
public sealed class DatabasesOptions() : Dictionary<string, DatabaseOptions>(StringComparer.OrdinalIgnoreCase), IValidatableObject
{
    public const string Section = "Databases";

    private readonly Dictionary<string, bool> declared = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>取得指定 <paramref name="name"/> 的連線設定；若無此名稱則回傳空的未配置項目。</summary>
    public DatabaseOptions For(string name) => TryGetValue(name, out var database) ? database : new();

    /// <inheritdoc cref="DatabaseOptions.ConnectionString"/>
    public string ConnectionString(string name, string? catalog = null) => For(name).ConnectionString(catalog);

    /// <summary>業務模組宣告使用項目 <paramref name="name"/>；若 <paramref name="readOnly"/> 為 true 則該設定必須明確指定 <c>ReadOnly=true</c>。</summary>
    internal void Declare(string name, bool readOnly) => declared[name] = readOnly || declared.GetValueOrDefault(name);

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
