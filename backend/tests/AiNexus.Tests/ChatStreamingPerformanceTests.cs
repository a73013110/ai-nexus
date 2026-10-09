using System.Globalization;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Chat;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

/// <summary>
/// Several users stream long answers at the same time. Each flush must write only its delta (no full-content rewrite),
/// and runs of different conversations must not wait for each other. TestServer + SQLite: the numbers describe the app's
/// own write volume and locking, not production SQL Server latency (SQLite itself admits one writer at a time).
/// </summary>
public sealed class ChatStreamingPerformanceTests(ITestOutputHelper output)
{
    private const int Users = 4, Chunks = 60, ChunkSize = 600, DelayMs = 10;

    [Fact, Trait("Category", "Performance")]
    public async Task ConcurrentStreamingRunsWriteOnlyDeltasAndProgressTogether()
    {
        var commands = new WriteCounter();
        var provider = new LongAnswerProvider();
        await using var factory = new NexusFactory(inference: options =>
        {
            options.ProviderConcurrency = new() { ["google"] = Users };
            options.QueueCapacity = Users;
            options.MaxOutputCharacters = 200_000;
            options.TimeoutSeconds = 60;
        }, services: services =>
        {
            services.RemoveAllKeyed<IInferenceProvider>("google");
            services.AddKeyedSingleton<IInferenceProvider>("google", provider);
            services.ConfigureDbContext<NexusDbContext>(options => options.AddInterceptors(commands));
        });
        var clients = new List<HttpClient>();
        var conversations = new List<Guid>();
        for (var i = 0; i < Users; i++)
        {
            var client = await factory.SignedInAsync($"stream{i}");
            clients.Add(client);
            conversations.Add((await CreateConversation(client)).Id);
        }
        commands.Reset();
        var watch = Stopwatch.StartNew();
        var runs = await Task.WhenAll(clients.Select((client, i) => CreateRun(client, conversations[i], $"長回答 {i}")));
        var finished = await Task.WhenAll(clients.Select((client, i) => WaitForTerminal(client, runs[i].Id)));
        watch.Stop();
        var writes = commands.Snapshot();

        var expected = LongAnswerProvider.Answer;
        Assert.All(finished, run => { Assert.Equal(RunStates.Completed, run.Status); Assert.Equal(expected, run.Content); });
        for (var i = 0; i < Users; i++)
        {
            // The replayed deltas rebuild exactly the persisted answer, and the message holds the same text.
            var stream = await clients[i].GetStringAsync($"/api/v1/runs/{runs[i].Id}/events?after=0");
            var deltas = stream.Split('\n').Where(x => x.StartsWith("data: ", StringComparison.Ordinal))
                .Select(x => JsonSerializer.Deserialize<RunEventDto>(x[6..], JsonSerializerOptions.Web)!)
                .Where(x => x.Type == "delta").Select(x => x.Delta).ToList();
            Assert.Equal(expected, string.Concat(deltas));
            var detail = (await clients[i].GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversations[i]}"))!;
            Assert.Equal(expected, detail.Messages.Single(x => x.Id == runs[i].AssistantMessageId).Content);
        }
        Assert.Equal(Users, provider.MaxConcurrent);

        var flushes = Users * Chunks;
        var serializedFloor = Users * Chunks * DelayMs;
        output.WriteLine($"users={Users} chunks={Chunks} chunkChars={ChunkSize} wall={watch.ElapsedMilliseconds}ms serializedProviderFloor={serializedFloor}ms");
        output.WriteLine($"writes={writes.Commands} writesPerFlush={(double)writes.Commands / flushes:F2} writtenChars={writes.Characters} writtenCharsPerFlush={writes.Characters / flushes} largestWriteChars={writes.Largest}");
        output.WriteLine($"workerCommands={writes.WorkerCommands} workerCommandsPerFlush={(double)writes.WorkerCommands / flushes:F2}");
        // Each delta is written a bounded number of times (event row, run, message): linear, not quadratic, in the answer length.
        // (SQLite also sends ids and timestamps as text; rewriting the whole answer per flush would be about Chunks times more.)
        Assert.True(writes.Largest < 4 * ChunkSize, $"A single write carried {writes.Largest} characters; flushes must not resend the whole answer.");
        Assert.True(writes.Characters < 12L * Users * expected.Length, $"Wrote {writes.Characters} characters for {Users * expected.Length} characters of answers.");
        // A flush neither reloads the run, charge, message and conversation nor rewrites them: a few set-based statements.
        Assert.True(writes.WorkerCommands < 6 * flushes, $"The worker issued {writes.WorkerCommands} commands for {flushes} flushes.");
        foreach (var client in clients) client.Dispose();
    }

    [Fact]
    public async Task OtherConversationsKeepStreamingWhileOneConversationIsBusy()
    {
        var provider = new LongAnswerProvider(chunks: 5);
        await using var factory = new NexusFactory(inference: options => { options.ProviderConcurrency = new() { ["google"] = Users }; options.QueueCapacity = Users; options.MaxOutputCharacters = 200_000; options.TimeoutSeconds = 60; },
            services: services => { services.RemoveAllKeyed<IInferenceProvider>("google"); services.AddKeyedSingleton<IInferenceProvider>("google", provider); });
        var clients = new List<HttpClient>();
        var conversations = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            clients.Add(await factory.SignedInAsync($"busy{i}"));
            conversations.Add((await CreateConversation(clients[i])).Id);
        }
        var scheduler = factory.Services.GetRequiredService<GenerationScheduler>();
        Task<HttpResponseMessage> blocked;
        // Hold the first conversation as a slow branch switch, deletion or share would.
        using (await scheduler.LockConversationAsync(conversations[0], CancellationToken.None))
        {
            blocked = PostRun(clients[0], new(conversations[0], "test-model", "等待中", null, null));
            var others = await Task.WhenAll(Enumerable.Range(1, 2).Select(i => CreateRun(clients[i], conversations[i], $"其他 {i}")));
            var finished = await Task.WhenAll(others.Select((run, i) => WaitForTerminal(clients[i + 1], run.Id)));
            Assert.All(finished, run => Assert.Equal(LongAnswerProvider.AnswerOf(5), run.Content));
            Assert.False(blocked.IsCompleted);
        }
        var response = await blocked.WaitAsync(TimeSpan.FromSeconds(10));
        response.EnsureSuccessStatusCode();
        var released = (await response.Content.ReadFromJsonAsync<RunDto>())!;
        Assert.Equal(RunStates.Completed, (await WaitForTerminal(clients[0], released.Id)).Status);
        Assert.Equal(0, scheduler.LockedConversations);
        foreach (var client in clients) client.Dispose();
    }

    private sealed class LongAnswerProvider(int chunks = Chunks) : IInferenceProvider
    {
        public static string AnswerOf(int chunks) => string.Concat(Enumerable.Range(0, chunks).Select(Chunk));
        public static readonly string Answer = AnswerOf(Chunks);
        private int concurrent;
        public int MaxConcurrent;
        private static string Chunk(int index) => new StringBuilder(ChunkSize).Append((char)('一' + index), ChunkSize - 4).Append(CultureInfo.InvariantCulture, $"{index:D4}").ToString();
        public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model" });
        public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
        {
            var now = Interlocked.Increment(ref concurrent);
            int seen;
            while ((seen = Volatile.Read(ref MaxConcurrent)) < now && Interlocked.CompareExchange(ref MaxConcurrent, now, seen) != seen) { }
            try
            {
                for (var i = 0; i < chunks; i++)
                {
                    await Task.Delay(DelayMs, ct);
                    yield return new InferenceChunk(Chunk(i));
                }
                yield return new InferenceChunk("", true, 100, chunks * ChunkSize / 2, CachedInputTokens: 0, ReasoningTokens: 0);
            }
            finally { Interlocked.Decrement(ref concurrent); }
        }
    }

    /// <summary>Counts INSERT/UPDATE commands with the characters their string parameters carry, and every command of the generation worker.</summary>
    private sealed class WriteCounter : DbCommandInterceptor
    {
        private long commands, characters, largest, worker;
        public void Reset() { Interlocked.Exchange(ref commands, 0); Interlocked.Exchange(ref characters, 0); Interlocked.Exchange(ref largest, 0); Interlocked.Exchange(ref worker, 0); }
        public (long Commands, long Characters, long Largest, long WorkerCommands) Snapshot() => (Interlocked.Read(ref commands), Interlocked.Read(ref characters), Interlocked.Read(ref largest), Interlocked.Read(ref worker));
        private void Record(DbCommand command)
        {
            for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
                if (activity.OperationName == "generation.execute") { Interlocked.Increment(ref worker); break; }
            var text = command.CommandText;
            if (!text.Contains("UPDATE ", StringComparison.OrdinalIgnoreCase) && !text.Contains("INSERT ", StringComparison.OrdinalIgnoreCase)) return;
            long size = 0;
            foreach (DbParameter parameter in command.Parameters) if (parameter.Value is string value) size += value.Length;
            Interlocked.Increment(ref commands);
            Interlocked.Add(ref characters, size);
            long seen;
            while ((seen = Interlocked.Read(ref largest)) < size && Interlocked.CompareExchange(ref largest, size, seen) != seen) { }
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Record(command); return result; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Record(command); return ValueTask.FromResult(result); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { Record(command); return result; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Record(command); return ValueTask.FromResult(result); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { Record(command); return result; }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) { Record(command); return ValueTask.FromResult(result); }
    }
}
