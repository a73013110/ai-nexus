using System.Text.Json.Nodes;

namespace AiNexus.IntegrationTests.Host;

public sealed class OpenApiContractTests
{
    // The published contract is the frontend's generated type source. Refactors must keep it byte-for-byte equivalent;
    // intentional API changes regenerate it with scripts/Export-Contracts.ps1 in the same change.
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

    [Fact]
    public async Task OpenApiContainsDiagnosticContractsAndCanBeExportedWithoutMachineSecrets()
    {
        await using var factory = new NexusFactory(); using var client = factory.CreateClient(); var contract = await client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("QuerySystemLogs", contract); Assert.Contains("DiagnosticHealthDto", contract); Assert.Contains("issueCode", contract);
        if (Environment.GetEnvironmentVariable("NEXUS_OPENAPI_OUTPUT") is { Length: > 0 } path) await File.WriteAllTextAsync(path, contract);
    }

    [Fact]
    public async Task OpenApiIncludesRetrievalContracts()
    {
        await using var factory = new NexusFactory(); using var client = factory.CreateClient();
        using var result = await client.GetAsync("/openapi/v1.json"); result.EnsureSuccessStatusCode();
        var json = await result.Content.ReadAsStringAsync(); Assert.Contains("EmbeddingProfileDto", json); Assert.Contains("rewriteMs", json);
    }
}
