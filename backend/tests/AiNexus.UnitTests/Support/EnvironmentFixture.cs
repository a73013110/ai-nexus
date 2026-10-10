using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AiNexus.UnitTests.Support;

internal sealed class EnvironmentFixture : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "AiNexus";
    public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "nexus-deployment-fixture");
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
