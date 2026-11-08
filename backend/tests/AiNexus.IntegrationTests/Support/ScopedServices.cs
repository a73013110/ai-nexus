using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>在新的 scope 裡取得 scoped 服務（例如排程任務）並呼叫一次，跟引擎每一輪各開一個 scope 相同。</summary>
internal static class ScopedServices
{
    public static async Task<TResult> InScopeAsync<TService, TResult>(this AppFactory factory, Func<TService, Task<TResult>> call) where TService : notnull
    {
        using var scope = factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
