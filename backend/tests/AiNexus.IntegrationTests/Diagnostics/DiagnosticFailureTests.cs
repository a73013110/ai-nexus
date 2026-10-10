using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.IntegrationTests.Diagnostics;

public sealed class DiagnosticFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JournalPermissionOrDiskFailureDoesNotRecurseOrBlockBusiness(bool permission)
    {
        var journal = new FailedJournal(permission);
        await using var factory = new NexusFactory(workers: [typeof(DiagnosticWorker)], services: services => { services.RemoveAll<IDiagnosticJournal>(); services.AddSingleton<IDiagnosticJournal>(journal); });
        using var client = await factory.SignedInAsync(); var watch = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++) (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
        for (var i = 0; i < 100 && factory.Services.GetRequiredService<DiagnosticHealth>().WriteFailures == 0; i++) await Task.Delay(10);
        var health = factory.Services.GetRequiredService<DiagnosticHealth>(); Assert.True(health.WriteFailures > 0);
        Assert.True(journal.Calls <= journal.Elapsed.TotalSeconds + 3, "Retries must be time-bounded rather than a recursive/hot loop");
        var accepted = health.Accepted; await Task.Delay(100); Assert.True(health.Accepted - accepted < 20, "Diagnostics failure must not recursively emit logs");
    }

    private sealed class FailedJournal(bool permission) : IDiagnosticJournal
    {
        public int Calls;
        private readonly Stopwatch lifetime = Stopwatch.StartNew();
        public TimeSpan Elapsed => lifetime.Elapsed;
        public Task AppendAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct) { Interlocked.Increment(ref Calls); if (permission) throw new UnauthorizedAccessException("secret path"); throw new IOException("disk full fixture"); }
        public Task ReplayAsync(IDiagnosticStore store, CancellationToken ct) => Task.CompletedTask;
        public void Cleanup() { }
    }

    [Fact]
    public async Task Sql30053HybridReturns200AndPersistsCorrelatedDegradationWarning()
    {
        await using var factory = new NexusFactory(workers: [typeof(DiagnosticWorker)], services: services => {
            services.RemoveAll<IRetrievalStore>(); services.AddScoped<IRetrievalStore, SqlServerRetrievalStore>();
            services.RemoveAll<ISqlDatabase<NexusDbContext>>(); services.AddSingleton<ISqlDatabase<NexusDbContext>>(new FullTextFailure());
        });
        using var client = await factory.SignedInAsync(); var seed = await KnowledgeApi.SeedAsync(factory, client);
        using var response = await client.PostAsJsonAsync("/api/v1/knowledge/search", new KnowledgeSearchRequest("採購", [seed.Collection]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var result = await response.Content.ReadFromJsonAsync<KnowledgeSearchDto>(); Assert.Equal("vector", result!.Mode);
        DiagnosticEvent? warning = null;
        for (var i = 0; i < 200; i++) {
            using var scope = factory.Services.CreateScope(); warning = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<DiagnosticEvent>().AsNoTracking().FirstOrDefaultAsync(x => x.EventId == DiagnosticEvents.Degraded);
            if (warning is not null) break; await Task.Delay(25);
        }
        Assert.NotNull(warning); Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, warning.Level); Assert.True(Issues.ValidCode(warning.IssueCode));
        Assert.Contains("30053", warning.PropertiesJson); Assert.Contains("hybrid", warning.PropertiesJson); Assert.Contains("vector", warning.PropertiesJson);
        Assert.NotNull(warning.RequestId); Assert.NotNull(warning.TraceId); Assert.DoesNotContain("fixture-secret", warning.ExceptionDetail!);
        var completed = false;
        for (var i = 0; i < 200 && !completed; i++) {
            using var check = factory.Services.CreateScope(); completed = await check.ServiceProvider.GetRequiredService<NexusDbContext>().Set<DiagnosticEvent>().AnyAsync(x => x.TraceId == warning.TraceId && x.StatusCode == 200);
            if (!completed) await Task.Delay(25);
        }
        Assert.True(completed);
    }

    // SQL failure is injected at the SQL seam, exercising the production retrieval/fallback branch.
    private sealed class FullTextFailure : ISqlDatabase<NexusDbContext>
    {
        public Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => typeof(T) == typeof(bool) ? Task.FromResult((T)(object)true) : throw new InvalidOperationException("Unexpected SQL: " + sql);
        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
            => sql.Contains("FREETEXTTABLE", StringComparison.Ordinal) ? throw SqlFailure() : Task.FromResult<IReadOnlyList<T>>([]);
        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected SQL: " + sql);
        public Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected SQL: " + sql);
    }

    private static SqlException SqlFailure()
    {
        // Microsoft.Data.SqlClient intentionally has no public factory for server errors; reflection is confined to this fixture.
        var errorConstructor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First(x => x.GetParameters().Length == 9);
        var error = (SqlError)errorConstructor.Invoke([30053, (byte)1, (byte)16, "fixture-server", "fixture-secret", "fixture-procedure", 1, 0, null]);
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(collection, [error]);
        var factory = typeof(SqlException).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).First(x => x.Name == "CreateException" && x.GetParameters().Length == 2);
        return (SqlException)factory.Invoke(null, [collection, "16.0"])!;
    }
}
