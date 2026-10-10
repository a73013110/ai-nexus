using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Integrations;

public sealed class IntegrationsOptions
{
    public const string Section = "Integrations";

    [ValidateObjectMembers] public SourceOptions Gdweb { get; set; } = new();
    [ValidateObjectMembers] public SourceOptions Meiho { get; set; } = new();
    public SourceOptions For(string id) => id switch { "gdweb" => Gdweb, "meiho" => Meiho, _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown source.") };
    public static string ConnectionKey(string id) => id switch { "gdweb" => "LegacyGdweb", "meiho" => "LegacyMeiho", _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown source.") };
}

public sealed class SourceOptions
{
    /// <summary>Only <c>sql</c> is implemented; other values show the source as unsupported instead of falling back.</summary>
    public string Transport { get; set; } = "sql";
    public bool Enabled { get; set; }
    public bool AclContractConfirmed { get; set; }
    [MaxLength(20), IdentifierItems] public string[] AllowedGroupIds { get; set; } = [];
    [Range(2, 30)] public int CommandTimeoutSeconds { get; set; } = 10;
    [Range(1, 50)] public int MaxResults { get; set; } = 30;
    /// <summary>Read-only source login; <see cref="IntegrationsSettings.SourceConnections"/> turns it into a connection string at startup.</summary>
    [ValidateObjectMembers] public SourceDatabaseOptions Database { get; set; } = new();
}

public sealed class SourceDatabaseOptions
{
    public string Server { get; set; } = "";
    public string Name { get; set; } = "";
    public bool TrustServerCertificate { get; set; }
    [Range(1, 120)] public int ConnectTimeoutSeconds { get; set; } = 10;
    /// <summary>Belongs in the secrets file, with <see cref="Password"/>.</summary>
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
}

[OptionsValidator]
public sealed partial class IntegrationsOptionsValidator : IValidateOptions<IntegrationsOptions>;
