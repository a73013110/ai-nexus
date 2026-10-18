using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Authentication;

public sealed class AdAuthenticationOptions : IValidatableObject
{
    public const string Section = "Identity:ActiveDirectory";

    /// <summary><c>Ldap</c> 透過網站表單登入；<c>Windows</c> 使用 IIS 整合驗證。</summary>
    [AllowedValues("Ldap", "Windows")] public string Mode { get; set; } = "Ldap";
    /// <summary>AD 的 DNS 網域，例如 <c>company.internal</c>；伺服器、搜尋根與帳號後綴都由此推導。</summary>
    [RegularExpression(@"^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$")] public string Domain { get; set; } = "";
    /// <summary>Domain controller 主機；留空時連到 <see cref="Domain"/>，由 DNS 解析到其中一台 DC。</summary>
    [RegularExpression(@"^[A-Za-z0-9.-]+$")] public string Server { get; set; } = "";
    /// <summary>true 使用 LDAPS（636）；false 使用 StartTLS（389）。</summary>
    public bool UseLdaps { get; set; }
    /// <summary>使用者搜尋根；留空時由 <see cref="Domain"/> 推導 <c>DC=…</c>。</summary>
    public string SearchBase { get; set; } = "";
    /// <summary>服務帳號：只寫帳號名時會補上 <c>@Domain</c>；UPN、<c>DOMAIN\name</c> 與 DN 原樣使用。</summary>
    public string BindUser { get; set; } = "";
    /// <summary>服務帳號密碼，應放在秘密檔而非一般設定檔。</summary>
    public string BindPassword { get; set; } = "";
    /// <summary>與登入名稱比對的 LDAP 屬性。</summary>
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
