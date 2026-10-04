using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;
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
using AiNexus.Database;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Attachments;
using EDoc.Core.Database.Interfaces;
using EDoc.Core.Database.Markers;
using System.Data.Common;

namespace AiNexus.Tests;

// These substitutions are confined to the test assembly. Production has no fake-user header.
public sealed class NexusFactory : WebApplicationFactory<Program>
{
    private readonly bool ldap;
    private readonly Action<InferenceOptions>? configureInference;
    private readonly Action<AttachmentOptions>? configureAttachments;
    private readonly string[] bootstrapAdministrators;
    private readonly bool backgroundJobs;
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}.db");
    public TestProvider Provider { get; } = new();
    public TestEmbeddings Embeddings { get; } = new();
    public NexusFactory(Action<NexusDbContext>? seed = null, bool ldap = false, Action<InferenceOptions>? inference = null, Action<AttachmentOptions>? attachments = null, string[]? administrators = null, bool backgroundJobs = true)
    {
        this.ldap = ldap;
        configureInference = inference;
        configureAttachments = attachments;
        bootstrapAdministrators = administrators ?? [];
        this.backgroundJobs = backgroundJobs;
        using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlite($"Data Source={databasePath};Default Timeout=10").Options);
        db.Database.EnsureCreated();
        seed?.Invoke(db);
        foreach (var user in db.Users.AsNoTracking().ToList())
            if (!db.Set<AiNexus.Modules.AccessControl.UserRole>().Any(x => x.UserId == user.Id)) db.Set<AiNexus.Modules.AccessControl.UserRole>().Add(new() { UserId = user.Id, RoleId = AiNexus.Modules.AccessControl.BuiltInAccess.MemberRole });
        db.SaveChanges();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<NexusDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<NexusDbContext>>();
            services.AddDbContext<NexusDbContext>(options => options.UseSqlite($"Data Source={databasePath};Default Timeout=10"));
            services.RemoveAll<IDbConnectionFactory>();
            services.AddSingleton<IDbConnectionFactory>(new TestConnectionFactory(databasePath));
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestIdentityHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(options => { options.DefaultAuthenticateScheme = ldap ? AuthEndpoints.CookieScheme : "Test"; options.DefaultChallengeScheme = ldap ? AuthEndpoints.CookieScheme : "Test"; });
            services.AddSingleton<IAuthenticationSchemeProvider, TestSchemeProvider>();
            services.RemoveAll<IAdAuthenticator>();
            services.AddSingleton<IAdAuthenticator, FixtureAdAuthenticator>();
            services.PostConfigure<AdAuthenticationOptions>(options => { options.Mode = ldap ? "Ldap" : "Windows"; options.DnPass = "fixture-only"; });
            services.RemoveAll<IInferenceProvider>();
            services.AddSingleton<IInferenceProvider>(Provider);
            services.RemoveAll<AiNexus.Modules.Knowledge.IEmbeddingProvider>();
            services.AddSingleton<AiNexus.Modules.Knowledge.IEmbeddingProvider>(Embeddings);
            if (!backgroundJobs) services.Remove(services.Single(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(AiNexus.Modules.Operations.BackgroundJobWorker)));
            services.PostConfigure<InferenceOptions>(options =>
            {
                options.QueueCapacity = 2;
                options.DefaultModelId = null; options.AllowModelSelection = true; options.ShowModelNames = true;
                options.TimeoutSeconds = 5;
                options.SystemPrompt = "請用繁體中文回答。";
                options.Models = [new ModelProfile { Id = "test-model", DisplayName = "測試模型", ContextTokens = 8192, MaxOutputTokens = 512 }];
                configureInference?.Invoke(options);
            });
            services.PostConfigure<AttachmentOptions>(options => configureAttachments?.Invoke(options));
            services.PostConfigure<AiNexus.Modules.Administration.AdministrationOptions>(options => options.BootstrapAdministrators = bootstrapAdministrators);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        using (var scope = host.Services.CreateScope()) scope.ServiceProvider.GetRequiredService<NexusDbContext>().Database.EnsureCreated();
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
        for (var attempt = 0; attempt < 100 && !Services.GetRequiredService<GenerationScheduler>().Ready; attempt++) await Task.Delay(10);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { SqliteConnection.ClearAllPools(); File.Delete(databasePath); }
    }
}

public sealed class TestEmbeddings : AiNexus.Modules.Knowledge.IEmbeddingProvider
{
    public string Profile => "fixture-embedding:768:v1";
    public bool Enabled { get; set; } = true;
    public bool Fail { get; set; }
    public int Calls;
    public int DelayMs { get; set; }
    public async Task<float[]> EmbedAsync(Guid owner, string text, bool document, string? title, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls); await Task.Delay(DelayMs, ct);
        if (Fail) throw new ApiException(503, "fixture_embedding_failed", "測試索引服務暫停。");
        var value = new float[768]; value[0] = 1; return value;
    }
}

public sealed class TestConnectionFactory(string path) : IDbConnectionFactory
{
    public DbConnection CreateConnection<TDb>() where TDb : IDbMarker => new SqliteConnection($"Data Source={path};Default Timeout=10");
    public DbContextOptions<TContext> CreateDbContextOptions<TDb, TContext>() where TDb : IDbMarker where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>().UseSqlite($"Data Source={path};Default Timeout=10").Options;
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
    public TestSchemeProvider(IOptions<AuthenticationOptions> options) : base(options) => RemoveScheme("Negotiate");
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
    public int DelayMs { get; set; } = 30;
    public bool Fail { get; set; }
    public bool NeverFinish { get; set; }
    public int Calls;
    public int Concurrent;
    public int MaxConcurrent;
    public IReadOnlyList<InferenceMessage> LastMessages { get; private set; } = [];
    public GenerationParameters? LastParameters { get; private set; }
    public string? LastModel { get; private set; }
    public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model", "not-approved" });
    public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        var concurrent = Interlocked.Increment(ref Concurrent);
        MaxConcurrent = Math.Max(MaxConcurrent, concurrent);
        LastMessages = messages;
        LastParameters = parameters;
        LastModel = model;
        try
        {
            if (Fail) throw new HttpRequestException("fixture failure");
            foreach (var text in new[] { "這是", "測試", "回答。" })
            {
                await Task.Delay(DelayMs, ct);
                yield return new InferenceChunk(text);
            }
            if (NeverFinish) await Task.Delay(Timeout.Infinite, ct);
            yield return new InferenceChunk("", true, 123, 6);
        }
        finally { Interlocked.Decrement(ref Concurrent); }
    }
}
