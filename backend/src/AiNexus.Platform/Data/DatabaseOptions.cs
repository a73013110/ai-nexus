using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace AiNexus.Platform.Data;

/// <summary>
/// One SQL Server database under <c>Databases:&lt;Name&gt;</c>. <see cref="ConnectionString"/> is the only place a
/// connection string is built, so every database gets the same rules: always encrypted, no persisted credentials, and
/// read-only intent when <see cref="ReadOnly"/> is set.
/// </summary>
public sealed class DatabaseOptions
{
    public const int MinConnectTimeoutSeconds = 1;
    public const int MaxConnectTimeoutSeconds = 120;

    /// <summary>Host or <c>host\instance</c>, with <c>,port</c> for a non-default port.</summary>
    public string Server { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Belongs in the secrets file, with <see cref="Password"/>. Empty with <see cref="IntegratedSecurity"/>.</summary>
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>Signs in as the process identity (IIS application pool, gMSA) instead of a SQL login.</summary>
    public bool IntegratedSecurity { get; set; }
    /// <summary>Skips certificate chain validation only; the connection stays encrypted.</summary>
    public bool TrustServerCertificate { get; set; }
    public int ConnectTimeoutSeconds { get; set; } = 10;
    /// <summary>Adds <c>ApplicationIntent=ReadOnly</c>. It does not replace read-only permissions on the SQL side.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Server and database are set, and either a complete SQL login or integrated security.</summary>
    public bool Configured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Name)
        && (IntegratedSecurity || !string.IsNullOrWhiteSpace(User) && !string.IsNullOrEmpty(Password));

    /// <param name="catalog">A database on the same server to open instead of <see cref="Name"/> (for example <c>master</c>).</param>
    /// <returns>The connection string, or an empty string when the database is not <see cref="Configured"/>.</returns>
    public string ConnectionString(string? catalog = null)
    {
        if (!Configured) return "";
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = Server, InitialCatalog = catalog ?? Name,
            Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = ConnectTimeoutSeconds, PersistSecurityInfo = false
        };
        if (IntegratedSecurity) connection.IntegratedSecurity = true;
        else { connection.UserID = User; connection.Password = Password; }
        if (ReadOnly) connection.ApplicationIntent = ApplicationIntent.ReadOnly;
        return connection.ConnectionString;
    }

    /// <param name="key">The configuration key of this entry, so every message names the key to fix.</param>
    internal IEnumerable<ValidationResult> Validate(string key)
    {
        if (ConnectTimeoutSeconds is < MinConnectTimeoutSeconds or > MaxConnectTimeoutSeconds)
            yield return new($"{key}:{nameof(ConnectTimeoutSeconds)} must be {MinConnectTimeoutSeconds}–{MaxConnectTimeoutSeconds}.", [key]);
        var hasUser = !string.IsNullOrWhiteSpace(User);
        var hasPassword = !string.IsNullOrEmpty(Password);
        if (IntegratedSecurity && (hasUser || hasPassword))
            yield return new($"{key}:{nameof(IntegratedSecurity)}=true signs in as the process identity; leave {key}:{nameof(User)} and {key}:{nameof(Password)} empty.", [key]);
        if (!IntegratedSecurity && hasUser != hasPassword)
            yield return new($"{key}:{(hasUser ? nameof(Password) : nameof(User))} is missing; {nameof(User)} and {nameof(Password)} are set together.", [key]);
        if ((IntegratedSecurity || hasUser || hasPassword) && (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Name)))
            yield return new($"{key}:{(string.IsNullOrWhiteSpace(Server) ? nameof(Server) : nameof(Name))} is required once the sign-in is set.", [key]);
    }
}
