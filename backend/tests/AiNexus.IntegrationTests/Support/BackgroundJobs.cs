using AiNexus.Features.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

internal static class BackgroundJobs
{
    internal static async Task Drain(NexusFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
    }

    internal static async Task Process(NexusFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
    }
}
