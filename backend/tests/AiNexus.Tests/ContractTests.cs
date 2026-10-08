using System.Text.Json.Nodes;
using Xunit;

namespace AiNexus.Tests;

// The published contract is the frontend's generated type source. Refactors must keep it byte-for-byte equivalent;
// intentional API changes regenerate it with scripts/Export-Contracts.ps1 in the same change.
public sealed class ContractTests
{
    [Fact]
    public async Task OpenApiMatchesPublishedContract()
    {
        await using var factory = new NexusFactory();
        using var client = factory.CreateClient();
        var actual = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var path = Path.Combine(RepositoryRoot(), "contracts", "openapi.json");
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(path));
        if (!JsonNode.DeepEquals(expected, actual))
        {
            var output = Path.Combine(Path.GetTempPath(), "ai-nexus-openapi.actual.json");
            await File.WriteAllTextAsync(output, actual!.ToJsonString(new() { WriteIndented = true }));
            Assert.Fail($"OpenAPI differs from contracts/openapi.json. Actual contract: {output}. Regenerate with scripts/Export-Contracts.ps1 when the change is intentional.");
        }
    }

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json"))) return directory.FullName;
        throw new InvalidOperationException("Repository root (global.json) was not found.");
    }
}
