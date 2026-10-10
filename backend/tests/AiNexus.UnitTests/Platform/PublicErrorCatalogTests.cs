
namespace AiNexus.UnitTests.Platform;

public sealed class PublicErrorCatalogTests
{
    [Fact]
    public void ReviewedPublicHintsStayIdenticalAcrossBackendAndFrontend()
    {
        var root = Path.GetFullPath("../../../../../..", AppContext.BaseDirectory);
        var backend = File.ReadAllText(Path.Combine(root, "backend/src/AiNexus.Platform/Diagnostics/PublicErrorCatalog.cs"));
        var frontend = File.ReadAllText(Path.Combine(root, "frontend/src/app/core/api/public-error-catalog.ts"));
        var expected = System.Text.RegularExpressions.Regex.Matches(backend, "\\[\"(?<key>[a-z0-9_]+)\"\\]\\s*=\\s*\"(?<hint>[^\"\\r\\n]*)\"")
            .ToDictionary(m => m.Groups["key"].Value, m => m.Groups["hint"].Value);
        var actual = System.Text.RegularExpressions.Regex.Matches(frontend, @"^\s*(?<key>[a-z0-9_]+):\s*'(?<hint>[^']*)'", System.Text.RegularExpressions.RegexOptions.Multiline)
            .ToDictionary(m => m.Groups["key"].Value, m => m.Groups["hint"].Value);
        Assert.NotEmpty(expected); Assert.Equal(expected.Count, actual.Count);
        foreach (var hint in expected) { Assert.True(actual.TryGetValue(hint.Key, out var value), "Missing fixed public hint: " + hint.Key); Assert.Equal(hint.Value, value); }
    }
}
