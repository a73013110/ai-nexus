using AiNexus.Features.Work.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

internal static class BackgroundJobs
{
    internal static async Task Drain(AppFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
    }

    internal static async Task Process(AppFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
    }
}
