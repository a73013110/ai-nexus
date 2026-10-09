namespace AiNexus.Features.Integrations;

public sealed class IntegrationsOptions
{
    public SourceOptions Gdweb { get; set; } = new();
    public SourceOptions Meiho { get; set; } = new();
    public SourceOptions For(string id) => id switch { "gdweb" => Gdweb, "meiho" => Meiho, _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown source.") };
    public static string ConnectionKey(string id) => id switch { "gdweb" => "LegacyGdweb", "meiho" => "LegacyMeiho", _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown source.") };
}

public sealed class SourceOptions
{
    public string Transport { get; set; } = "sql";
    public bool Enabled { get; set; }
    public bool AclContractConfirmed { get; set; }
    public string[] AllowedGroupIds { get; set; } = [];
    public int CommandTimeoutSeconds { get; set; } = 10;
    public int MaxResults { get; set; } = 30;
}
