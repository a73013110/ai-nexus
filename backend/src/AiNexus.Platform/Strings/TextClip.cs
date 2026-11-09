using System.Diagnostics.CodeAnalysis;

namespace AiNexus.Platform.Strings;

public static class TextClip
{
    /// <summary>
    /// 截到最多 <paramref name="max"/> 個 UTF-16 單元（與資料庫 nvarchar 的長度同一種單位）。
    /// 截點落在代理對中間時少留一個單元：半個 emoji 存進資料庫或序列化成 JSON 都會變成亂碼。
    /// </summary>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? Clip(this string? text, int max)
    {
        if (text is null || text.Length <= max) return text;
        return text[..(max > 0 && char.IsHighSurrogate(text[max - 1]) ? max - 1 : max)];
    }
}
