using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AiNexus.Features.Account;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AiNexus.Features.Attachments;
using Xunit;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Tests;

/// <summary>
/// One application host per test, on its own SQLite file copied from a schema template. Only the generation worker runs;
/// other hosted workers are opt-in through <c>workers</c>, and tests drive periodic work by calling it directly.
/// These substitutions are confined to the test assembly. Production has no fake-user header.
/// </summary>
public sealed class NexusFactory : WebApplicationFactory<Program>
{
    /// <summary>Far below production work, so password tests do not spend their time hashing.</summary>
    public static readonly Argon2Cost PasswordCost = new(1024, 1);
    private static readonly Lazy<string> Template = new(CreateTemplate);
    private readonly bool ldap;
    private readonly Action<InferenceOptions>? configureInference;
    private readonly Action<AttachmentOptions>? configureAttachments;
    private readonly string[] bootstrapAdministrators;
    private readonly Type[] workers;
    private readonly TimeProvider? clock;
    private readonly Action<IServiceCollection>? configureServices;
    private readonly string? webRoot;
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}.db");
    public TestProvider Provider { get; } = new();
    public TestEmbeddings Embeddings { get; } = new();
    public NexusFactory(Action<NexusDbContext>? seed = null, bool ldap = false, Action<InferenceOptions>? inference = null, Action<AttachmentOptions>? attachments = null, string[]? administrators = null,
        Type[]? workers = null, TimeProvider? clock = null, Action<IServiceCollection>? services = null, string? webRoot = null)
    {
        this.ldap = ldap;
        configureInference = inference;
        configureAttachments = attachments;
        bootstrapAdministrators = administrators ?? [];
        this.workers = [typeof(AiNexus.Features.Chat.GenerationWorker), .. workers ?? []];
        this.clock = clock;
        configureServices = services;
        this.webRoot = webRoot;
        File.Copy(Template.Value, databasePath);
        using var db = new NexusDbContext(DatabaseOptions(databasePath));
        seed?.Invoke(db);
        foreach (var user in db.Users.AsNoTracking().ToList())
            if (!db.Set<AiNexus.Features.AccessControl.UserRole>().Any(x => x.UserId == user.Id)) db.Set<AiNexus.Features.AccessControl.UserRole>().Add(new() { UserId = user.Id, RoleId = AiNexus.Features.AccessControl.BuiltInAccess.MemberRole });
        db.SaveChanges();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (webRoot is not null) builder.UseWebRoot(webRoot);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<NexusDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<NexusDbContext>>();
            services.AddDbContext<NexusDbContext>(options => options.UseSqlite(ConnectionString(databasePath)));
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestIdentityHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(options => { options.DefaultAuthenticateScheme = ldap ? AuthEndpoints.CookieScheme : "Test"; options.DefaultChallengeScheme = ldap ? AuthEndpoints.CookieScheme : "Test"; });
            services.AddSingleton<IAuthenticationSchemeProvider, TestSchemeProvider>();
            services.RemoveAll<IAdAuthenticator>();
            services.AddSingleton<IAdAuthenticator, FixtureAdAuthenticator>();
            services.PostConfigure<AdAuthenticationOptions>(options => { options.Mode = ldap ? "Ldap" : "Windows"; options.DnPass = "fixture-only"; });
            services.RemoveAllKeyed<IInferenceProvider>("google");
            services.RemoveAllKeyed<IInferenceProvider>("ollama");
            services.AddKeyedSingleton<IInferenceProvider>("google", Provider);
            services.AddKeyedSingleton<IInferenceProvider>("ollama", Provider);
            services.RemoveAll<AiNexus.Features.Knowledge.Embeddings.IEmbeddingClient>();
            services.AddSingleton<AiNexus.Features.Knowledge.Embeddings.IEmbeddingClient>(Embeddings);
            services.PostConfigure<AiNexus.Features.Knowledge.KnowledgeOptions>(x => { x.EmbeddingProvider = "ollama"; x.Dimensions = 768; });
            foreach (var worker in services.Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType?.Assembly.GetName().Name?.StartsWith("AiNexus.", StringComparison.Ordinal) == true
                && !workers.Contains(x.ImplementationType)).ToList()) services.Remove(worker);
            services.Replace(ServiceDescriptor.Singleton(PasswordCost));
            if (clock is not null) services.Replace(ServiceDescriptor.Singleton(clock));
            services.PostConfigure<InferenceOptions>(options =>
            {
                options.QueueCapacity = 2;
                options.ProviderConcurrency = new() { ["google"] = 1 };
                options.DefaultModelId = null; options.AllowModelSelection = true; options.ShowModelNames = true;
                options.TimeoutSeconds = 5;
                options.SystemPrompt = "請用繁體中文回答。";
                options.Models = [new ModelProfile { Id = "test-model", DisplayName = "測試模型", ContextTokens = 8192, MaxOutputTokens = 512 }];
                configureInference?.Invoke(options);
                foreach (var model in options.Models)
                    if (model.ProviderModelId.Length == 0) model.ProviderModelId = model.Id;
            });
            services.PostConfigure<AttachmentOptions>(options => { options.StoragePath = databasePath + ".attachments"; configureAttachments?.Invoke(options); });
            services.PostConfigure<AiNexus.Platform.Diagnostics.DiagnosticOptions>(options => { options.Directory = databasePath + ".logs"; options.FlushIntervalMs = 10; options.RetrySeconds = 1; options.OtlpEnabled = false; });
            services.PostConfigure<AiNexus.Features.Administration.AdministrationOptions>(options => options.BootstrapAdministrators = bootstrapAdministrators);
            configureServices?.Invoke(services);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        host.Start();
        return host;
    }

    public async Task<HttpClient> SignedInAsync(string account = "alice")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", account);
        var response = await client.GetAsync("/api/v1/me");
        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<MeDto>();
        client.DefaultRequestHeaders.Add("X-Nexus-CSRF", me!.CsrfToken);
        await Services.GetRequiredService<GenerationScheduler>().WhenFirstReady.WaitAsync(TimeSpan.FromSeconds(10));
        return client;
    }

    private static string ConnectionString(string path) => $"Data Source={path};Default Timeout=10";
    private static DbContextOptions<NexusDbContext> DatabaseOptions(string path) => new DbContextOptionsBuilder<NexusDbContext>().UseSqlite(ConnectionString(path)).Options;

    private static string CreateTemplate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nexus-template-{Guid.NewGuid():N}.db");
        using (var db = new NexusDbContext(DatabaseOptions(path))) db.Database.EnsureCreated();
        SqliteConnection.ClearAllPools();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => File.Delete(path);
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { SqliteConnection.ClearAllPools(); File.Delete(databasePath); if (Directory.Exists(databasePath + ".attachments")) Directory.Delete(databasePath + ".attachments", recursive: true); if (Directory.Exists(databasePath + ".logs")) Directory.Delete(databasePath + ".logs", recursive: true); }
    }
}

public sealed class TestEmbeddings : AiNexus.Features.Knowledge.Embeddings.IEmbeddingClient
{
    public string Provider => "ollama";
    public bool Enabled { get; set; } = true;
    public bool Fail { get; set; }
    public int? FailProfileId { get; set; }
    public int? FailOnCall { get; set; }
    public int LastProfileId { get; private set; }
    public int Calls;
    public int DelayMs { get; set; }
    public async Task<AiNexus.Features.Knowledge.Embeddings.EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, AiNexus.Features.Knowledge.Embeddings.EmbeddingPurpose purpose, AiNexus.Features.Knowledge.Embeddings.EmbeddingProfile profile, CancellationToken ct)
    {
        var call = Interlocked.Increment(ref Calls); LastProfileId = profile.Id; await Task.Delay(DelayMs, ct);
        if (Fail || FailProfileId == profile.Id || FailOnCall == call) throw new ApiException(503, "fixture_embedding_failed", "測試索引服務暫停。");
        return new(inputs.Select(_ => { var value = new float[profile.Dimensions]; value[0] = 1; return value; }).ToArray());
    }
}

public sealed class FixtureAdAuthenticator : IAdAuthenticator
{
    public Task VerifyServiceAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<AdIdentity> AuthenticateAsync(string account, string password, CancellationToken ct)
        => password == "fixture-password" && account is "alice" or "bob"
            ? Task.FromResult(new AdIdentity($"S-1-5-21-fixture-{account}", $"{account}@fixture.test", account))
            : throw new ApiException(401, "ad_invalid_credentials", "AD 帳號或密碼不正確。");
}

public sealed class TestSchemeProvider : AuthenticationSchemeProvider
{
    public TestSchemeProvider(IOptions<AuthenticationOptions> options) : base(options)
    {
        RemoveScheme("Negotiate");
        AddScheme(new AuthenticationScheme("Negotiate", "Fixture Windows", typeof(TestIdentityHandler)));
    }
}

public sealed class TestIdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers["X-Test-User"].ToString();
        if (user.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new[] { new Claim(ClaimTypes.Name, $"TEST\\{user}"), new Claim(ClaimTypes.PrimarySid, $"S-1-5-21-test-{user}"), new Claim("display_name", user) };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = (int)HttpStatusCode.Unauthorized; return Task.CompletedTask; }
}

public sealed class TestProvider : IInferenceProvider
{
    public bool DiscoveryFail { get; set; }
    public int DelayMs { get; set; } = 30;
    public bool Fail { get; set; }
    public bool NeverFinish { get; set; }
    public int Calls;
    public int Concurrent;
    public int MaxConcurrent;
    private readonly List<(int Calls, TaskCompletionSource Signal)> callWaiters = [];
    public IReadOnlyList<InferenceMessage> LastMessages { get; private set; } = [];

    /// <summary>Completes once the provider has been called <paramref name="calls"/> times, so tests need not poll <see cref="Calls"/>.</summary>
    public Task WhenCalledAsync(int calls = 1)
    {
        lock (callWaiters)
        {
            if (Volatile.Read(ref Calls) >= calls) return Task.CompletedTask;
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            callWaiters.Add((calls, signal));
            return signal.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    public GenerationParameters? LastParameters { get; private set; }
    public string? LastModel { get; private set; }
    public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => DiscoveryFail ? throw new HttpRequestException("fixture discovery failure") : Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model", "not-approved" });
    public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
    {
        var calls = Interlocked.Increment(ref Calls);
        lock (callWaiters)
            foreach (var waiter in callWaiters.Where(x => x.Calls <= calls).ToList()) { callWaiters.Remove(waiter); waiter.Signal.TrySetResult(); }
        var concurrent = Interlocked.Increment(ref Concurrent);
        MaxConcurrent = Math.Max(MaxConcurrent, concurrent);
        LastMessages = messages;
        LastParameters = parameters;
        LastModel = model;
        try
        {
            if (Fail) throw new HttpRequestException("fixture failure");
            if (parameters.SystemPrompt.Contains("\"conclusion\"", StringComparison.Ordinal))
            {
                await Task.Delay(DelayMs, ct);
                yield return new InferenceChunk("{\"conclusion\":\"已完成快速初檢，未發現明確缺陷。\",\"changes\":[\"更新程式邏輯。\"],\"findings\":[],\"limitation\":\"\"}");
                yield return new InferenceChunk("", true, 123, 6, "STOP", CachedInputTokens: 0, ReasoningTokens: 0);
                yield break;
            }
            foreach (var text in new[] { "這是", "測試", "回答。" })
            {
                await Task.Delay(DelayMs, ct);
                yield return new InferenceChunk(text);
            }
            if (NeverFinish) await Task.Delay(Timeout.Infinite, ct);
            yield return new InferenceChunk("", true, 123, 6, CachedInputTokens: 0, ReasoningTokens: 0);
        }
        finally { Interlocked.Decrement(ref Concurrent); }
    }
}
