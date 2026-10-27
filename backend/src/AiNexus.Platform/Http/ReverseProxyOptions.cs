using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Http;

/// <summary>站台前方反向代理（防火牆、負載平衡、ARR）的信任設定；決定能否採用 <c>X-Forwarded-For</c> 還原真實來源位址。</summary>
public sealed class ReverseProxyOptions : IValidatableObject
{
    public const string Section = "ReverseProxy";

    /// <summary>
    /// 會轉送請求並附上 <c>X-Forwarded-For</c>／<c>X-Forwarded-Proto</c> 的代理：位址或 CIDR 網段。
    /// 只有連線來源落在這裡（或本機 loopback）時才採信標頭；空清單＝只有本機代理被信任，直連的客戶端送來的標頭一律忽略。
    /// 不可填 <c>0.0.0.0/0</c> 這類過寬的網段，否則任何人都能偽造來源 IP 繞過登入限流與稽核。
    /// </summary>
    [MaxLength(50), IpRangeItems] public string[] TrustedProxies { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var key = $"{Section}:{nameof(TrustedProxies)}";
        foreach (var item in TrustedProxies)
        {
            if (IpRangeItemsAttribute.TryParse(item, out var network) && network.PrefixLength == 0)
                yield return new($"{key} must not trust every address ({item}); list the proxy addresses instead.", [key]);
        }
    }
}

[OptionsValidator]
public sealed partial class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>;
