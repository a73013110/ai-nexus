using AiNexus.GeneratorTests.Support;
using AiNexus.Generators.Composition;

namespace AiNexus.GeneratorTests.Composition;

public sealed class ManualRegistrationAnalyzerTests
{
    [Fact]
    public async Task RegisteringAContractImplementationByHandIsReportedAtTheCall()
    {
        const string setup = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;

            namespace Sample;

            internal sealed class ItemSource : ISource;

            internal sealed class Worker : BackgroundService
            {
                protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
            }

            internal sealed class Plain;

            internal static class Setup
            {
                public static void Add(IServiceCollection services)
                {
                    services.AddSource<ItemSource>();
                    services.AddHostedService<Worker>();
                    // 不是契約的註冊方法，照常手動註冊。
                    services.AddScoped<ItemSource>();
                    services.AddSingleton<Plain>();
                }
            }
            """;

        var diagnostics = await GeneratorHarness.AnalyzeAsync(new ManualRegistrationAnalyzer(), FeatureCompositionGeneratorTests.Contracts, setup);

        Assert.Equal(["AINX1007", "AINX1007"], diagnostics.Select(x => x.Id));
        var calls = diagnostics.Select(x => x.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(x.Location.SourceSpan)).Order(StringComparer.Ordinal);
        Assert.Equal(["services.AddHostedService<Worker>()", "services.AddSource<ItemSource>()"], calls);
        Assert.Contains(diagnostics, x => x.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("'Sample.ItemSource' 實作 'Sample.ISource'", StringComparison.Ordinal));
    }

    /// <summary>產生器看不到其他組件的型別，那些實作本來就要手動註冊。</summary>
    [Fact]
    public async Task ImplementationsFromOtherAssembliesAreIgnored()
    {
        const string setup = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Sample;

            internal static class Setup
            {
                public static void Add(IServiceCollection services) => services.AddHostedService<Microsoft.Extensions.Hosting.BackgroundService>();
            }
            """;

        Assert.Empty(await GeneratorHarness.AnalyzeAsync(new ManualRegistrationAnalyzer(), setup));
    }
}
