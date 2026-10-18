using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity.Authentication;

/// <summary>首次管理員的設定（<c>Identity:Bootstrap</c>）。用子區段而非整個 <c>Identity</c>，未知設定檢查才不會把 <c>Identity:ActiveDirectory</c> 當成打錯的鍵。</summary>
public sealed class AdminBootstrapOptions
{
    public const string Section = "Identity:Bootstrap";

    /// <summary>首次啟動時授予管理者角色的目錄短帳號；之後的變更一律透過管理頁面進行。</summary>
    [MaxLength(20), IdentifierItems] public string[] Administrators { get; set; } = [];
}

[OptionsValidator]
public sealed partial class AdminBootstrapOptionsValidator : IValidateOptions<AdminBootstrapOptions>;
