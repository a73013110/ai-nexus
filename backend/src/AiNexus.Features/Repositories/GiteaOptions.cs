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
    /// <summary>Files one chat handoff may bundle into its attachment.</summary>
    [Range(1, 50)] public int MaxChatFiles { get; set; } = 20;
    /// <summary>Characters of the bundled attachment; keep it within the attachment text extraction limit.</summary>
    [Range(1000, 256000)] public int MaxChatCharacters { get; set; } = 60000;
    /// <summary>Text files one folder import may add to a knowledge collection.</summary>
    [Range(1, 1000)] public int MaxImportFiles { get; set; } = 200;
}

[OptionsValidator]
public sealed partial class GiteaOptionsValidator : IValidateOptions<GiteaOptions>;
