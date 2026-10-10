using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Repositories;

public sealed class GiteaOptions
{
    public const string Section = "Repositories:Gitea";

    public bool Enabled { get; set; }
    [Required, HttpEndpoint(HttpsOutsideLoopback = true)] public string BaseUrl { get; set; } = "https://gitea.hanglong.com.tw/";
    [Range(2, 30)] public int TimeoutSeconds { get; set; } = 10;
    [Range(1024, 500000)] public int MaxFileBytes { get; set; } = 200000;
}

[OptionsValidator]
public sealed partial class GiteaOptionsValidator : IValidateOptions<GiteaOptions>;
