namespace AiNexus.IntegrationTests.Support;

/// <summary>每個 Dashboard 瀏覽器測試都會啟動 Kestrel 與 Chromium，因此獨立執行，避免計時斷言被其他測試搶走資源。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BrowserCollection
{
    public const string Name = "Browser";
}
