using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.WebSearch;

public sealed class WebSearchOptions
{
    public const string Section = "WebSearch";

    public bool Enabled { get; set; }
    [AllowedValues("searxng", "brave")] public string Provider { get; set; } = "searxng";
    [Required, HttpEndpoint] public string Endpoint { get; set; } = "http://localhost:8080/";
    /// <summary>Brave key; belongs in the secrets file.</summary>
    public string ApiKey { get; set; } = "";
    [Range(2, 30)] public int TimeoutSeconds { get; set; } = 10;
    [Range(1, 8)] public int MaxResults { get; set; } = 5;
    [Range(1, 10000)] public int MaxDailyRequests { get; set; } = 100;
}

[OptionsValidator]
public sealed partial class WebSearchOptionsValidator : IValidateOptions<WebSearchOptions>;
