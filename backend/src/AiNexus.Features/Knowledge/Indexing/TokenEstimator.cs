namespace AiNexus.Features.Knowledge.Indexing;

public static class TokenEstimator
{
    public static int Estimate(string text)
    {
        var cjk = 0; var other = 0;
        foreach (var rune in text.EnumerateRunes())
            if (rune.Value is >= 0x2e80 and <= 0x9fff or >= 0xf900 and <= 0xfaff or >= 0x20000 and <= 0x323af or >= 0xff00 and <= 0xffef) cjk++;
            else other++;
        return cjk + (other + 3) / 4;
    }
    public static string Truncate(string text, int budget)
    {
        if (budget <= 0) return "";
        if (Estimate(text) <= budget) return text;
        var end = 0; var cjk = 0; var other = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Estimate(rune.ToString()) == 1 && rune.Value is >= 0x2e80 and <= 0x9fff or >= 0xf900 and <= 0xfaff or >= 0x20000 and <= 0x323af or >= 0xff00 and <= 0xffef) cjk++;
            else other++;
            if (cjk + (other + 3) / 4 > budget) break;
            end += rune.Utf16SequenceLength;
        }
        return text[..end];
    }
}
