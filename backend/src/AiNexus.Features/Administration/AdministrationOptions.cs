using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Administration;

public sealed class AdministrationOptions
{
    public const string Section = "Administration";

    /// <summary>Directory short accounts granted the administrator role on first bootstrap; later changes go through the admin pages.</summary>
    [MaxLength(20), IdentifierItems] public string[] BootstrapAdministrators { get; set; } = [];
}

[OptionsValidator]
public sealed partial class AdministrationOptionsValidator : IValidateOptions<AdministrationOptions>;
