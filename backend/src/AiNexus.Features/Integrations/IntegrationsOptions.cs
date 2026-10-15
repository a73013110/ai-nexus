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
}

/// <summary>Business settings of one source; its SQL connection is the source's entry under <c>Databases</c>.</summary>
public sealed class SourceOptions
{
    /// <summary>Only <c>sql</c> is implemented; other values show the source as unsupported instead of falling back.</summary>
    public string Transport { get; set; } = "sql";
    public bool Enabled { get; set; }
    public bool AclContractConfirmed { get; set; }
    [MaxLength(20), IdentifierItems] public string[] AllowedGroupIds { get; set; } = [];
    [Range(2, 30)] public int CommandTimeoutSeconds { get; set; } = 10;
    [Range(1, 50)] public int MaxResults { get; set; } = 30;
}

[OptionsValidator]
public sealed partial class IntegrationsOptionsValidator : IValidateOptions<IntegrationsOptions>;
