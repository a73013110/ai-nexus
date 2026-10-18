using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories;

/// <summary>Gitea 整合設定，區段為 <c>Repositories:Gitea</c>。</summary>
public sealed class GiteaOptions
{
    public const string Section = "Repositories:Gitea";

    public bool Enabled { get; set; }
    [Required, HttpEndpoint(HttpsOutsideLoopback = true)] public string BaseUrl { get; set; } = "https://gitea.hanglong.com.tw/";
    [Range(2, 30)] public int TimeoutSeconds { get; set; } = 10;
    [Range(1024, 500000)] public int MaxFileBytes { get; set; } = 200000;
    /// <summary>單次交給對話時，可合併進附件的檔案數。</summary>
    [Range(1, 50)] public int MaxChatFiles { get; set; } = 20;
    /// <summary>合併附件的字元上限，必須不大於附件文字擷取的上限。</summary>
    [Range(1000, 256000)] public int MaxChatCharacters { get; set; } = 60000;
    /// <summary>單次資料夾匯入可加入知識庫的文字檔數。</summary>
    [Range(1, 1000)] public int MaxImportFiles { get; set; } = 200;
}

[OptionsValidator]
public sealed partial class GiteaOptionsValidator : IValidateOptions<GiteaOptions>;
