using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Data;

/// <summary>
/// Every SQL Server connection of the application, keyed by database name: <c>Databases:Nexus</c>,
/// <c>Databases:Gdweb</c>, … A module declares the names it uses (<see cref="Sql.SqlDatabaseServices.AddDatabase"/>);
/// an entry nobody declares fails validation, so a misspelt name never leaves a database silently unconfigured.
/// </summary>
public sealed class DatabasesOptions() : Dictionary<string, DatabaseOptions>(StringComparer.OrdinalIgnoreCase), IValidatableObject
{
    public const string Section = "Databases";

    private readonly Dictionary<string, bool> declared = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The entry for <paramref name="name"/>, or an unconfigured one when the section has none.</summary>
    public DatabaseOptions For(string name) => TryGetValue(name, out var database) ? database : new();

    /// <inheritdoc cref="DatabaseOptions.ConnectionString"/>
    public string ConnectionString(string name, string? catalog = null) => For(name).ConnectionString(catalog);

    /// <summary>A module uses entry <paramref name="name"/>; with <paramref name="readOnly"/> it only reads it, so the entry must set <c>ReadOnly=true</c>.</summary>
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
