namespace AiNexus.IntegrationTests.Support;

/// <summary>Dashboard browser tests each start Kestrel and Chromium; they run alone so timing assertions are not starved.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BrowserCollection
{
    public const string Name = "Browser";
}
