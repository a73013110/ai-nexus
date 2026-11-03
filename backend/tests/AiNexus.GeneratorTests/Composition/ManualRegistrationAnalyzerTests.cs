using AiNexus.GeneratorTests.Support;
using AiNexus.Generators.Composition;

namespace AiNexus.GeneratorTests.Composition;

public sealed class ManualRegistrationAnalyzerTests
{
    [Fact]
    public async Task RegisteringAGeneratedServiceByHandIsReportedAtTheCall()
    {
        const string setup = """
            using System.Threading;
            using System.Threading.Tasks;
            using AiNexus.Platform.Modules;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection.Extensions;
            using Microsoft.Extensions.Hosting;

            namespace Sample;

            internal sealed class ItemSource : ISource;

            internal sealed class Worker : BackgroundService
            {
                protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
            }

            internal sealed class Plain;

            [Scoped<IClock>]
            internal sealed class Clock : IClock;

            internal static class Setup
            {
                public static void Add(IServiceCollection services)
                {
                    services.AddSource<ItemSource>();
                    services.AddHostedService<Worker>();
                    services.AddSingleton<IClock, Clock>();
                    services.TryAddScoped<Clock>();
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IClock, Clock>());
                    // 沒有標記的類別照常手動註冊；轉接（最後一個型別參數是介面）不算重複。
                    services.AddScoped<ItemSource>();
                    services.AddSingleton<Plain>();
                    services.AddScoped<IClock>(provider => provider.GetRequiredService<Clock>());
                }
            }
            """;

        var diagnostics = await GeneratorHarness.AnalyzeAsync(new ManualRegistrationAnalyzer(), FeatureCompositionGeneratorTests.Contracts, setup);

        Assert.All(diagnostics, x => Assert.Equal("AINX1007", x.Id));
        var calls = diagnostics.Select(x => x.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(x.Location.SourceSpan)).Order(StringComparer.Ordinal);
        Assert.Equal(["ServiceDescriptor.Singleton<IClock, Clock>()", "services.AddHostedService<Worker>()", "services.AddSingleton<IClock, Clock>()", "services.AddSource<ItemSource>()", "services.TryAddScoped<Clock>()"], calls);
        var messages = diagnostics.Select(x => x.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Contains(messages, x => x.Contains("'Sample.ItemSource' 實作 'Sample.ISource'", StringComparison.Ordinal));
        Assert.Contains(messages, x => x.Contains("'Sample.Clock' 標了 [Scoped<IClock>]", StringComparison.Ordinal));
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
