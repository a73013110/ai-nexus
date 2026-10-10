using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity.Authentication;

public sealed class AdAuthenticationOptions
{
    public const string Section = "Identity:ActiveDirectory";

    /// <summary><c>Ldap</c> signs in through the site form; <c>Windows</c> uses IIS integrated authentication.</summary>
    [AllowedValues("Ldap", "Windows")] public string Mode { get; set; } = "Ldap";
    public string Url { get; set; } = "";
    public string DnUser { get; set; } = "";
    /// <summary>Directory service account password; belongs in the secrets file.</summary>
    public string DnPass { get; set; } = "";
    public string AdAccountAttrName { get; set; } = "sAMAccountName";
    public string Domain { get; set; } = "";
    public bool Configured => !string.IsNullOrWhiteSpace(DnPass);
}

[OptionsValidator]
public sealed partial class AdAuthenticationOptionsValidator : IValidateOptions<AdAuthenticationOptions>;
