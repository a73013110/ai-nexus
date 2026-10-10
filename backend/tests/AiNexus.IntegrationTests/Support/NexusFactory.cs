using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AiNexus.IntegrationTests.Support;

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
            services.AddDbContext<NexusDbContext>((scope, options) => options.UseSqlite(ConnectionString(databasePath)).AddNexusInterceptors(scope));
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestIdentityHandler>("Test", _ => { });
            services.PostConfigure<AuthenticationOptions>(options => { options.DefaultAuthenticateScheme = ldap ? AuthEndpoints.CookieScheme : "Test"; options.DefaultChallengeScheme = ldap ? AuthEndpoints.CookieScheme : "Test"; });
            services.AddSingleton<IAuthenticationSchemeProvider, TestSchemeProvider>();
            services.RemoveAll<IAdAuthenticator>();
            services.AddSingleton<IAdAuthenticator, FixtureAdAuthenticator>();
            services.PostConfigure<AdAuthenticationOptions>(options => { options.Mode = ldap ? "Ldap" : "Windows"; options.Domain = "fixture.internal"; options.BindUser = "fixture"; options.BindPassword = "fixture-only"; });
            services.RemoveAllKeyed<IInferenceProvider>("google");
            services.RemoveAllKeyed<IInferenceProvider>("ollama");
            services.AddKeyedSingleton<IInferenceProvider>("google", Provider);
            services.AddKeyedSingleton<IInferenceProvider>("ollama", Provider);
            services.RemoveAll<AiNexus.Features.Knowledge.Embeddings.IEmbeddingClient>();
            services.AddSingleton<AiNexus.Features.Knowledge.Embeddings.IEmbeddingClient>(Embeddings);
            services.PostConfigure<AiNexus.Features.Knowledge.KnowledgeOptions>(x => { x.Embedding.Provider = "ollama"; x.Embedding.Dimensions = 768; });
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
