using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using System.Net.Http.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Administration;
using AiNexus.Features.Attachments;
using AiNexus.Features.Chat.Runs;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Inference;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Knowledge.Embeddings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 每個測試一個應用程式 host，使用從範本複製出的獨立 LocalDB 資料庫（<see cref="TestDatabase"/>）。只執行生成工作者；
/// 其他 hosted worker 需透過 <c>workers</c> 選擇加入，測試則直接呼叫來驅動週期性工作。
/// 這些替換只存在於測試組件中，正式環境沒有假使用者 header。
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    /// <summary>遠低於正式環境的工作量，讓密碼相關測試不會把時間花在雜湊運算上。</summary>
    public static readonly Argon2Cost PasswordCost = new(1024, 1);
    private readonly bool ldap;
    private readonly Action<InferenceOptions>? configureInference;
    private readonly Action<AttachmentOptions>? configureAttachments;
    private readonly string[] bootstrapAdministrators;
    private readonly Type[] workers;
    private readonly TimeProvider? clock;
    private readonly Action<IServiceCollection>? configureServices;
    private readonly string? webRoot;
    private readonly TestDatabase database;
    // 附件與日誌檔放在 host 自己的暫存資料夾，釋放時一起刪除。
    private readonly string files = Path.Combine(Path.GetTempPath(), $"ainexus-test-{Guid.NewGuid():N}");
    public TestProvider Provider { get; } = new();
    public TestEmbeddings Embeddings { get; } = new();
    public AppFactory(Action<AppDbContext>? seed = null, bool ldap = false, Action<InferenceOptions>? inference = null, Action<AttachmentOptions>? attachments = null, string[]? administrators = null,
        Type[]? workers = null, TimeProvider? clock = null, Action<IServiceCollection>? services = null, string? webRoot = null)
    {
        this.ldap = ldap;
        configureInference = inference;
        configureAttachments = attachments;
        bootstrapAdministrators = administrators ?? [];
        this.workers = [typeof(GenerationWorker), .. workers ?? []];
        this.clock = clock;
        configureServices = services;
        this.webRoot = webRoot;
        database = TestDatabase.Create();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(database.ConnectionString).Options);
        seed?.Invoke(db);
        foreach (var user in db.Users.AsNoTracking().ToList())
            if (!db.Set<UserRole>().Any(x => x.UserId == user.Id)) db.Set<UserRole>().Add(new() { UserId = user.Id, RoleId = BuiltInAccess.MemberRole });
        db.SaveChanges();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (webRoot is not null) builder.UseWebRoot(webRoot);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>((scope, options) => options.UseSqlServer(database.ConnectionString).AddAppInterceptors(scope));
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
            services.RemoveAll<IEmbeddingClient>();
            services.AddSingleton<IEmbeddingClient>(Embeddings);
            services.PostConfigure<KnowledgeOptions>(x => { x.Embedding.Provider = "ollama"; x.Embedding.Dimensions = 768; });
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
                options.Models = [new ModelDefinition { Id = "test-model", DisplayName = "測試模型", ContextTokens = 8192, MaxOutputTokens = 512 }];
                configureInference?.Invoke(options);
                foreach (var model in options.Models)
                    if (model.ProviderModelId.Length == 0) model.ProviderModelId = model.Id;
            });
            services.PostConfigure<AttachmentOptions>(options => { options.StoragePath = Path.Combine(files, "attachments"); configureAttachments?.Invoke(options); });
            services.PostConfigure<AiNexus.Platform.Diagnostics.DiagnosticOptions>(options => { options.Directory = Path.Combine(files, "logs"); options.FlushIntervalMs = 10; options.RetrySeconds = 1; options.OtlpEnabled = false; });
            services.PostConfigure<AdministrationOptions>(options => options.BootstrapAdministrators = bootstrapAdministrators);
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
        client.DefaultRequestHeaders.Add("X-CSRF-Token", me!.CsrfToken);
        await Services.GetRequiredService<GenerationScheduler>().WhenFirstReady.WaitAsync(TimeSpan.FromSeconds(10));
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { database.Dispose(); DeleteFiles(); }
    }

    // UseKestrel 時另一個 host 的日誌 flush 可能在 Dispose 後才放開 .jsonl；Windows 上刪除會短暫失敗，重試到 2 秒仍失敗才拋出。
    private void DeleteFiles()
    {
        for (var attempt = 1; Directory.Exists(files); attempt++)
        {
            try { Directory.Delete(files, recursive: true); }
            catch (IOException) when (attempt < 20) { Thread.Sleep(100); }
        }
    }
}
