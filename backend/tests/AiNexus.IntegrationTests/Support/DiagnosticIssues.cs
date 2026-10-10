using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

internal static class DiagnosticIssues
{
    internal static async Task<int> CountIssue(NexusFactory factory, string code) { using var scope = factory.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<DiagnosticEvent>().CountAsync(x => x.IssueCode == code); }

    internal static async Task<DiagnosticEvent> WaitForIssue(NexusFactory factory, string code)
    {
        for (var attempt = 0; attempt < 200; attempt++) {
            using var scope = factory.Services.CreateScope(); var item = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<DiagnosticEvent>().AsNoTracking().FirstOrDefaultAsync(x => x.IssueCode == code);
            if (item is not null) return item; await Task.Delay(25);
        }
        throw new Xunit.Sdk.XunitException("Issue was not replayed to persistent storage: " + code);
    }
}
