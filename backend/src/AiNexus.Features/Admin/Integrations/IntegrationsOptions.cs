using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Integrations;

public sealed class IntegrationsOptions
{
    public const string Section = "Integrations";

    [ValidateObjectMembers] public SourceOptions Gd { get; set; } = new();
    [ValidateObjectMembers] public SourceOptions Meiho { get; set; } = new();
    public SourceOptions For(string id) => id switch
    {
        "gd" => Gd,
        "meiho" => Meiho,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown source."),
    };
}

/// <summary>單一來源的業務設定；其 SQL 連線位於 <c>Databases</c> 下同名的項目。</summary>
public sealed class SourceOptions
{
    /// <summary>目前只實作 <c>sql</c>；其他值會把來源標示為不支援，而不是退回預設值。</summary>
    public string Transport { get; set; } = "sql";
    public bool Enabled { get; set; }
    public bool AclContractConfirmed { get; set; }
    [MaxLength(20), IdentifierItems] public string[] AllowedGroupIds { get; set; } = [];
    [Range(2, 30)] public int CommandTimeoutSeconds { get; set; } = 10;
    [Range(1, 50)] public int MaxResults { get; set; } = 30;
}

[OptionsValidator]
public sealed partial class IntegrationsOptionsValidator : IValidateOptions<IntegrationsOptions>;
