using System.Text;
using System.Text.RegularExpressions;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// AD 的 DN（<c>CN=林可蘿,OU=平台部,OU=研發處,DC=example,DC=test</c>）解析與比較。依 RFC 4514 處理跳脫（<c>\,</c> 與 <c>\xx</c> 的 UTF-8 位元組）；
/// 比較不分大小寫，因為 AD 對 DN 不分大小寫，而同一個節點在不同查詢回來的大小寫可能不同。
/// </summary>
public static partial class DirectoryNames
{
    /// <summary>
    /// 去掉分隔符號前後的空白（<c>OU=Staff, DC=example</c> → <c>OU=Staff,DC=example</c>）。AD 回傳的 DN 一律是緊湊寫法，
    /// 設定檔的搜尋根若照人的習慣加了空白，「是否在搜尋根之下」的比對會全部失敗。跳脫的逗號（<c>\,</c>）不動。
    /// </summary>
    public static string Canonical(string distinguishedName) => Separator().Replace(distinguishedName.Trim(), "$1");

    /// <summary>
    /// 由上而下的 OU／容器路徑：<c>研發處 › 平台部</c>。去掉第一段（本人）與 DC，只留 OU 與 CN。
    /// </summary>
    public static string PathOf(string distinguishedName)
        => string.Join(" › ", Parse(distinguishedName).Skip(1)
            .Where(x => x.Type.Equals("OU", StringComparison.OrdinalIgnoreCase) || x.Type.Equals("CN", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value).Reverse());

    /// <summary>第一段的值（節點或人員自己的名稱）；DN 無法解析時回原字串。</summary>
    public static string NameOf(string distinguishedName) => Parse(distinguishedName).FirstOrDefault().Value is { Length: > 0 } name ? name : distinguishedName;

    /// <summary><paramref name="distinguishedName"/> 是 <paramref name="ancestor"/> 本身或在它之下。</summary>
    public static bool Within(string distinguishedName, string ancestor)
        => distinguishedName.Equals(ancestor, StringComparison.OrdinalIgnoreCase) || Below(distinguishedName, ancestor);

    /// <summary><paramref name="distinguishedName"/> 在 <paramref name="ancestor"/> 之下（不含本身）。</summary>
    public static bool Below(string distinguishedName, string ancestor)
        => distinguishedName.EndsWith("," + ancestor, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\s*(?<!\\)([,=])\s*")]
    private static partial Regex Separator();

    private static List<(string Type, string Value)> Parse(string distinguishedName)
    {
        var parts = new List<(string Type, string Value)>();
        var type = new StringBuilder();
        var value = new List<byte>();
        var inValue = false;
        for (var i = 0; i < distinguishedName.Length; i++)
        {
            var c = distinguishedName[i];
            if (c == '\\' && i + 1 < distinguishedName.Length)
            {
                if (i + 2 < distinguishedName.Length && Uri.IsHexDigit(distinguishedName[i + 1]) && Uri.IsHexDigit(distinguishedName[i + 2]))
                {
                    value.Add(Convert.ToByte(distinguishedName.Substring(i + 1, 2), 16));
                    i += 2;
                }
                else value.AddRange(Encoding.UTF8.GetBytes(distinguishedName[++i].ToString()));
            }
            else if (c is ',' or ';') { Flush(); inValue = false; }
            else if (c == '=' && !inValue) inValue = true;
            else if (inValue) value.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
            else type.Append(c);
        }
        Flush();
        return parts;

        void Flush()
        {
            if (type.Length > 0) parts.Add((type.ToString().Trim(), Encoding.UTF8.GetString([.. value]).Trim()));
            type.Clear(); value.Clear();
        }
    }
}
