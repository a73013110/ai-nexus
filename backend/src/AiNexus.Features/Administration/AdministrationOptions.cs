using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Administration;

public sealed class AdministrationOptions
{
    public const string Section = "Administration";

    /// <summary>首次啟動時授予管理者角色的目錄短帳號；之後的變更一律透過管理頁面進行。</summary>
    [MaxLength(20), IdentifierItems] public string[] BootstrapAdministrators { get; set; } = [];
}

[OptionsValidator]
public sealed partial class AdministrationOptionsValidator : IValidateOptions<AdministrationOptions>;
