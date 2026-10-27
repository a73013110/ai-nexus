using System.ComponentModel.DataAnnotations;
using System.Net;

namespace AiNexus.Platform.Configuration;

/// <summary>
/// IP 允許清單的每個元素：單一位址（<c>10.0.0.5</c>）或 CIDR 網段（<c>10.0.0.0/24</c>）。
/// 寫錯的項目若被略過，信任範圍就會悄悄和設定者以為的不同，所以在啟動時就擋下。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IpRangeItemsAttribute : ValidationAttribute
{
    public IpRangeItemsAttribute() : base("Every {0} item must be an IP address or a CIDR network such as 10.0.0.0/24.") { }

    public override bool IsValid(object? value) => value is null || value is IEnumerable<string> items && items.All(item => TryParse(item, out _));

    /// <summary>把單一位址視為只含自己的網段（IPv4 /32、IPv6 /128），讓呼叫端只處理一種型別。</summary>
    public static bool TryParse(string? item, out IPNetwork network)
    {
        network = default;
        if (string.IsNullOrWhiteSpace(item)) return false;
        if (IPAddress.TryParse(item, out var address) && !item.Contains('/', StringComparison.Ordinal))
        {
            network = new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
            return true;
        }
        return IPNetwork.TryParse(item, out network);
    }
}
