using AiNexus.Platform.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.UnitTests.Platform;

public sealed class KeyedServicesTests
{
    private interface ISource;

    private sealed class Created
    {
        public List<string> Names { get; } = [];
    }

    private sealed class Alpha : ISource
    {
        public Alpha(Created created) => created.Names.Add("alpha");
    }

    private sealed class Beta : ISource
    {
        public Beta(Created created) => created.Names.Add("beta");
    }

    private static ServiceProvider Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection().AddSingleton<Created>();
        register(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    // 查表不可為了比對鍵而建立所有實作：scoped 實作可能帶著資料庫連線或外部用戶端。
    [Fact]
    public void FindCreatesOnlyTheMatchingImplementationInTheCallersScope()
    {
        using var provider = Build(services => services
            .AddKeyedService<ISource, Alpha>("alpha", ServiceLifetime.Scoped)
            .AddKeyedService<ISource, Beta>("beta", ServiceLifetime.Scoped));
        using var scope = provider.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<KeyedServices<ISource>>();

        var found = sources.Find("beta");

        Assert.IsType<Beta>(found);
        Assert.Same(found, scope.ServiceProvider.GetRequiredService<KeyedServices<ISource>>().Find("beta"));
        Assert.Equal(["beta"], provider.GetRequiredService<Created>().Names);
    }

    [Fact]
    public void AMissingKeyIsNullForFindAndAnErrorForRequire()
    {
        using var provider = Build(services => services.AddKeyedService<ISource, Alpha>("alpha", ServiceLifetime.Singleton));
        var sources = provider.GetRequiredService<KeyedServices<ISource>>();

        Assert.Null(sources.Find("gamma"));
        Assert.Throws<InvalidOperationException>(() => sources.Require("gamma"));
        Assert.Empty(provider.GetRequiredService<Created>().Names);
    }

    // DI 對重複的鍵只取最後一個，前一個悄悄失效；啟動檢查要列出鍵與兩個實作。
    [Fact]
    public void DuplicateKeysFailStartupVerification()
    {
        using var provider = Build(services => services
            .AddKeyedService<ISource, Alpha>("same", ServiceLifetime.Scoped)
            .AddKeyedService<ISource, Beta>("same", ServiceLifetime.Singleton));

        var error = Assert.Throws<InvalidOperationException>(provider.VerifyKeyedServices);

        Assert.Contains("'same'", error.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(Alpha).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(Beta).FullName!, error.Message, StringComparison.Ordinal);
    }

    // 查表依容器建立時的最終註冊：測試以 RemoveAllKeyed 換掉實作不算重複，直接以 AddKeyed* 加入的也查得到。
    [Fact]
    public void TheIndexReflectsRegistrationsReplacedBeforeTheContainerIsBuilt()
    {
        using var provider = Build(services =>
        {
            services.AddKeyedService<ISource, Alpha>("alpha", ServiceLifetime.Singleton);
            services.RemoveAllKeyed<ISource>("alpha");
            services.AddKeyedSingleton<ISource, Beta>("alpha");
        });

        provider.VerifyKeyedServices();

        Assert.IsType<Beta>(provider.GetRequiredService<KeyedServices<ISource>>().Find("alpha"));
    }
}
