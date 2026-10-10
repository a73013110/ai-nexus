using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Identity.Authentication;

public sealed class AdAuthenticationOptions : IValidatableObject
{
    public const string Section = "Identity:ActiveDirectory";

    /// <summary><c>Ldap</c> signs in through the site form; <c>Windows</c> uses IIS integrated authentication.</summary>
    [AllowedValues("Ldap", "Windows")] public string Mode { get; set; } = "Ldap";
    /// <summary>AD DNS domain such as <c>company.internal</c>; server, search base and account suffix derive from it.</summary>
    [RegularExpression(@"^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$")] public string Domain { get; set; } = "";
    /// <summary>Domain controller host; empty connects to <see cref="Domain"/>, which DNS resolves to a controller.</summary>
    [RegularExpression(@"^[A-Za-z0-9.-]+$")] public string Server { get; set; } = "";
    /// <summary>true connects with LDAPS on 636; false uses StartTLS on 389.</summary>
    public bool UseLdaps { get; set; }
    /// <summary>Where users are searched; empty derives <c>DC=…</c> from <see cref="Domain"/>.</summary>
    public string SearchBase { get; set; } = "";
    /// <summary>Directory service account: a bare name gets <c>@Domain</c>; UPN, <c>DOMAIN\name</c> and DN are used as given.</summary>
    public string BindUser { get; set; } = "";
    /// <summary>Directory service account password; belongs in the secrets file.</summary>
    public string BindPassword { get; set; } = "";
    /// <summary>Attribute matched against the sign-in name.</summary>
    [RegularExpression("^[A-Za-z][A-Za-z0-9-]{0,63}$")] public string AccountAttribute { get; set; } = "sAMAccountName";

    public bool Configured => !string.IsNullOrWhiteSpace(BindPassword);
    public string Host => Server.Length > 0 ? Server : Domain;
    public int Port => UseLdaps ? 636 : 389;
    public string Base => SearchBase.Length > 0 ? SearchBase : string.Join(',', Domain.Split('.').Select(part => "DC=" + part));
    public string BindName => BindUser.IndexOfAny(['@', '\\', '=']) >= 0 ? BindUser : $"{BindUser}@{Domain}";

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Mode == "Ldap" && Configured && (Domain.Length == 0 || BindUser.Length == 0))
            yield return new("BindPassword is set, so Domain and BindUser are required.", [nameof(Domain), nameof(BindUser)]);
    }
}

[OptionsValidator]
public sealed partial class AdAuthenticationOptionsValidator : IValidateOptions<AdAuthenticationOptions>;
