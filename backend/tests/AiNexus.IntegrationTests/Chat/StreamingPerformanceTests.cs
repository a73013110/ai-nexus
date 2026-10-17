using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Chat.Transcripts;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

/// <summary>
/// 多位使用者同時串流長回答。每次 flush 只能寫入增量（不得整段重寫），
/// 不同對話的 run 之間也不能互相等待。使用 TestServer + SQLite 時，數字反映的是應用程式自身的
/// 寫入量與鎖定，不代表正式環境 SQL Server 的延遲（SQLite 同一時間只允許一個寫入者）。
/// </summary>
public sealed class StreamingPerformanceTests(ITestOutputHelper output)
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
            // 重播增量應能完整還原已儲存的回答，並與訊息內容一致；否則代表 flush 寫入的內容有缺漏。
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
        // 每個增量只會寫入固定次數（事件列、run、訊息）：寫入量隨回答長度線性成長，而非平方成長。
        // （SQLite 也會把 id 與時間戳以文字送出；若每次 flush 都重寫整段回答，寫入量約為 Chunks 倍。）
        Assert.True(writes.Largest < 4 * ChunkSize, $"A single write carried {writes.Largest} characters; flushes must not resend the whole answer.");
        Assert.True(writes.Characters < 12L * Users * expected.Length, $"Wrote {writes.Characters} characters for {Users * expected.Length} characters of answers.");
        // flush 不會重新讀取或重寫 run、charge、訊息與對話，只執行少數集合式（set-based）陳述式。
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
        // 與緩慢的分支切換、刪除或分享一樣，先持有第一個對話的鎖，讓其他對話的串流不必等待它。
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

    /// <summary>統計 INSERT／UPDATE 指令中字串參數所帶的字元數，以及生成工作者（generation worker）發出的每一筆指令。</summary>
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
