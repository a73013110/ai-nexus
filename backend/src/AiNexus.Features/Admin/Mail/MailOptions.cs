using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail;

/// <summary>
/// 郵件寄送設定（區段 <c>Mail</c>）。公司自架 SMTP 與 Google Workspace SMTP relay 都是 <see cref="MailTransports.Smtp"/>，
/// 差別只在主機、埠、TLS 與帳密；<c>Transport</c> 空值在 Development 補成 <see cref="MailTransports.Pickup"/>（寫 .eml 不寄出），
/// Testing 補成 <see cref="MailTransports.Disabled"/>，其他環境必須明確設定，避免正式站悄悄不寄信。
/// </summary>
public sealed class MailOptions : IValidatableObject
{
    public const string Section = "Mail";

    [AllowedValues("", MailTransports.Disabled, MailTransports.Pickup, MailTransports.Smtp)] public string Transport { get; set; } = "";
    [ValidateObjectMembers] public MailSenderOptions From { get; set; } = new();
    /// <summary>回覆地址；空值不加 Reply-To。</summary>
    public string ReplyTo { get; set; } = "";
    /// <summary>Message-ID 的網域；空值用寄件地址的網域。</summary>
    [RegularExpression(@"^$|^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*$")] public string MessageIdDomain { get; set; } = "";
    /// <summary>信內連結的 origin（例：https://ainexus.company.internal）；不從請求 Host 取，避免 Host header 注入。</summary>
    public string PublicBaseUrl { get; set; } = "";
    /// <summary>允許的收件網域（小寫，不含 @）；空＝不限。UAT 設公司網域避免誤寄外部。</summary>
    [MaxLength(50)] public string[] AllowedRecipientDomains { get; set; } = [];
    [Range(1, 6000)] public int MaxPerMinute { get; set; } = 60;
    /// <summary>過去 24 小時最多寄出幾封；0＝不限。Gmail 帳號有每日上限，超過會被暫停寄信。</summary>
    [Range(0, 1_000_000)] public int MaxPerDay { get; set; }
    /// <summary>同時開的 SMTP 連線數；每條連線在一輪內連續寄多封。</summary>
    [Range(1, 8)] public int MaxConcurrency { get; set; } = 2;
    /// <summary>入列後超過幾小時仍未寄出就放棄（以時間為準，不以次數為準）。</summary>
    [Range(1, 720)] public int GiveUpAfterHours { get; set; } = 24;
    [ValidateObjectMembers] public MailSmtpOptions Smtp { get; set; } = new();
    public MailPickupOptions Pickup { get; set; } = new();
    [ValidateObjectMembers] public MailRetentionOptions Retention { get; set; } = new();

    /// <summary>會實際送出（或寫入 pickup 目錄）的設定；Disabled 時仍可入列，但不會被認領。</summary>
    public bool Enabled => Transport is MailTransports.Smtp or MailTransports.Pickup;

    public string MessageIdHost => MessageIdDomain.Length > 0 ? MessageIdDomain : From.Address[(From.Address.LastIndexOf('@') + 1)..];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Transport.Length == 0)
        {
            yield return new($"{nameof(Transport)} is required outside Development: set {MailTransports.Smtp}, {MailTransports.Pickup} or {MailTransports.Disabled}.", [nameof(Transport)]);
            yield break;
        }
        if (Retention.ContentDays > Retention.RecipientDays || Retention.RecipientDays > Retention.RecordDays)
            yield return new("Retention must satisfy ContentDays <= RecipientDays <= RecordDays.", [nameof(Retention)]);
        if (AllowedRecipientDomains.Any(x => !MailAddresses.IsDomain(x) || x.Any(char.IsUpper)))
            yield return new($"{nameof(AllowedRecipientDomains)} items must be lowercase domain names without '@'.", [nameof(AllowedRecipientDomains)]);
        if (ReplyTo.Length > 0 && !MailAddresses.TryNormalize(ReplyTo, out _))
            yield return new($"{nameof(ReplyTo)} must be a plain email address.", [nameof(ReplyTo)]);
        if (!Enabled) yield break;
        if (!MailAddresses.TryNormalize(From.Address, out _))
            yield return new($"{nameof(From)}.{nameof(MailSenderOptions.Address)} is required and must be a plain email address.", [nameof(From)]);
        if (!IsOrigin(PublicBaseUrl))
            yield return new($"{nameof(PublicBaseUrl)} is required: an absolute http(s) origin without path, query or user info.", [nameof(PublicBaseUrl)]);
        if (Transport == MailTransports.Smtp)
        {
            if (Smtp.Host.Length == 0) yield return new($"{nameof(Smtp)}.{nameof(MailSmtpOptions.Host)} is required when {nameof(Transport)} is {MailTransports.Smtp}.", [nameof(Smtp)]);
            else if (Smtp.Security == MailSmtpOptions.NoSecurity && !IsLoopback(Smtp.Host))
                yield return new($"{nameof(Smtp)}.{nameof(MailSmtpOptions.Security)}={MailSmtpOptions.NoSecurity} is only allowed for a loopback host.", [nameof(Smtp)]);
            if (Smtp.Password.Length > 0 && Smtp.UserName.Length == 0)
                yield return new($"{nameof(Smtp)}.{nameof(MailSmtpOptions.UserName)} is required when a password is set.", [nameof(Smtp)]);
        }
        if (Transport == MailTransports.Pickup && !Path.IsPathFullyQualified(Pickup.Directory))
            yield return new($"{nameof(Pickup)}.{nameof(MailPickupOptions.Directory)} must be an absolute path outside the site.", [nameof(Pickup)]);
    }

    private static bool IsOrigin(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0
           && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0 && !value.EndsWith('/');

    private static bool IsLoopback(string host) => host == "localhost" || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}

/// <summary><see cref="MailOptions.Transport"/> 的值。</summary>
public static class MailTransports
{
    public const string Disabled = "Disabled";
    public const string Pickup = "Pickup";
    public const string Smtp = "Smtp";
}

public sealed class MailSenderOptions
{
    /// <summary>寄件地址（例：no-reply@company.com）；SPF／DKIM／DMARC 要與它的網域對齊。</summary>
    [MaxLength(254)] public string Address { get; set; } = "";
    [MaxLength(120)] public string DisplayName { get; set; } = "AI Nexus";
}

public sealed class MailSmtpOptions
{
    public const string StartTls = "StartTls", SslOnConnect = "SslOnConnect", NoSecurity = "None";

    /// <summary>公司 mail server，或 Google 的 <c>smtp-relay.gmail.com</c>／<c>smtp.gmail.com</c>。</summary>
    [RegularExpression(@"^$|^[A-Za-z0-9.:-]+$")] public string Host { get; set; } = "";
    [Range(1, 65535)] public int Port { get; set; } = 587;
    /// <summary>StartTls（587）、SslOnConnect（465）或 None（只允許 loopback）。</summary>
    [AllowedValues(StartTls, SslOnConnect, NoSecurity)] public string Security { get; set; } = StartTls;
    /// <summary>空值＝匿名 relay（由 mail server 依 IIS 主機 IP 允許）。</summary>
    [MaxLength(254)] public string UserName { get; set; } = "";
    /// <summary>SMTP 密碼或 Google App Password，放秘密檔或環境變數 <c>Mail__Smtp__Password</c>。</summary>
    public string Password { get; set; } = "";
    /// <summary>EHLO 使用的網域；Google SMTP relay 要設成已登記的網域。空值由 MailKit 自動決定。</summary>
    [RegularExpression(@"^$|^[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*$")] public string LocalDomain { get; set; } = "";
    [Range(5, 300)] public int TimeoutSeconds { get; set; } = 30;
    /// <summary>自簽憑證的內部 server 以 SHA-1 或 SHA-256 指紋（十六進位）釘選；空值做完整憑證驗證。不提供「略過驗證」。</summary>
    [RegularExpression(@"^$|^([0-9A-Fa-f]{40}|[0-9A-Fa-f]{64})$")] public string ServerCertificateThumbprint { get; set; } = "";
}

public sealed class MailPickupOptions
{
    /// <summary>寫入 .eml 的站外絕對路徑；Development 空值為 <c>.local/mail</c>。也可指向 IIS SMTP 的 pickup 目錄。</summary>
    public string Directory { get; set; } = "";
}

public sealed class MailRetentionOptions
{
    /// <summary>內文保存天數；敏感信寄出或結束後立即刪除，不受此限。</summary>
    [Range(1, 3650)] public int ContentDays { get; set; } = 30;
    /// <summary>收件原址保存天數；之後只留遮罩與雜湊，不能再重寄。</summary>
    [Range(1, 3650)] public int RecipientDays { get; set; } = 90;
    /// <summary>寄送紀錄保存天數。</summary>
    [Range(1, 3650)] public int RecordDays { get; set; } = 365;
}

[OptionsValidator]
public sealed partial class MailOptionsValidator : IValidateOptions<MailOptions>;
