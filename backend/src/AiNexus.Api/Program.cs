using AiNexus.BuildingBlocks;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AiNexus.Database;
using EDoc.Core.Database.Interfaces;
using EDoc.Core.Database.Implementations;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;
using AiNexus.Modules.AccessControl;
using Microsoft.AspNetCore.DataProtection;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Library;
using AiNexus.Modules.Administration;
using AiNexus.Modules.Knowledge;

var builder = WebApplication.CreateBuilder(args);
NexusConfiguration.Load(builder, args);
LocalDatabaseSettings.Apply(builder.Configuration, builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));
var keyRing = builder.Configuration["DataProtection:KeyRingPath"];
if (keyRing is null && builder.Environment.IsDevelopment()) keyRing = Path.Combine(builder.Configuration["LocalWorkspaceRoot"]!, ".local", "keys");
var protection = builder.Services.AddDataProtection().SetApplicationName("AiNexus");
if (keyRing is not null)
{
    Directory.CreateDirectory(keyRing);
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyRing));
    if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
}
var authMode = builder.Configuration["AdAuthentication:Mode"] ?? "Ldap";
builder.Services.AddOptions<AdAuthenticationOptions>().BindConfiguration("AdAuthentication")
    .Validate(x => x.Mode is "Ldap" or "Windows", "AD Mode must be Ldap or Windows.").ValidateOnStart();
builder.Services.AddSingleton<IAdAuthenticator, LdapAuthenticator>();
builder.Services.AddAuthentication(authMode == "Ldap" ? AuthEndpoints.CookieScheme : NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate()
    .AddCookie(AuthEndpoints.CookieScheme, options =>
    {
        options.Cookie.Name = "Nexus.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context => Results.Problem(statusCode: 401, title: "請先使用 AD 帳號登入。", extensions: new Dictionary<string, object?> { ["code"] = "authentication_required" }).ExecuteAsync(context.HttpContext);
        options.Events.OnRedirectToAccessDenied = context => Results.Problem(statusCode: 403, title: "沒有存取此資料的權限。").ExecuteAsync(context.HttpContext);
    });
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ad-login", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.OnRejected = (context, ct) => new ValueTask(Results.Problem(statusCode: 429, title: "登入嘗試過於頻繁，請稍後再試。", extensions: new Dictionary<string, object?> { ["code"] = "login_rate_limited" }).ExecuteAsync(context.HttpContext));
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(BuiltInAccess.ChatPolicy, policy => policy.RequireAuthenticatedUser().AddRequirements(new FeatureRequirement(BuiltInAccess.ChatFeature)));
    options.AddPolicy(AdministrationConfiguration.Policy, policy => policy.RequireAuthenticatedUser().AddRequirements(new FeatureRequirement(AdministrationConfiguration.Feature)));
    foreach (var feature in new[] { "knowledge", "tasks" })
        options.AddPolicy("feature:" + feature, policy => policy.RequireAuthenticatedUser().AddRequirements(new FeatureRequirement(feature)));
    options.AddPolicy("feature:attachments", policy => policy.RequireAuthenticatedUser().AddRequirements(new FeatureRequirement("chat", "knowledge", "projects")));
});
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<IAuthorizationHandler, FeatureAuthorizationHandler>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-Nexus-CSRF";
    options.Cookie.Name = "Nexus.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
    options.SerializerOptions.MaxDepth = 32;
});
builder.Services.AddOpenApi();
builder.Services.AddDbContext<NexusDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Nexus") ?? ""));
builder.Services.AddSingleton<IDbConnectionFactory, NexusConnectionFactory>();
builder.Services.AddScoped<IDbHelper<INexusDatabase>, DbHelper<INexusDatabase>>();
builder.Services.AddScoped<IEfHelper<INexusDatabase>>(sp => new EfHelper<INexusDatabase>(sp.GetRequiredService<NexusDbContext>()));
builder.Services.AddScoped<IDbHelper<INexusBootstrapDatabase>, DbHelper<INexusBootstrapDatabase>>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<SqlVectorCapabilities>();
builder.Services.AddSingleton<StorageReadiness>();
builder.Services.AddSingleton<IdentityWriteLock>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<PersonalSettingsService>();
builder.Services.AddOptions<AdministrationOptions>().BindConfiguration("Administration")
    .Validate(x => x.BootstrapAdministrators.Length <= 20 && x.BootstrapAdministrators.All(a => a.Length is > 0 and <= 64 && a.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')), "Invalid bootstrap administrator accounts.").ValidateOnStart();
builder.Services.AddScoped<AdminBootstrap>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.AddScoped<ModelPolicyService>();
builder.Services.AddSingleton<ModelQuotaLock>();
builder.Services.AddScoped<ModelTaskService>();
builder.Services.AddSingleton<AiNexus.Modules.Collaboration.ResourceWriteLock>();
builder.Services.AddScoped<AiNexus.Modules.Collaboration.ResourceAccess>();
builder.Services.AddScoped<JobService>();
builder.Services.AddOptions<KnowledgeOptions>().BindConfiguration("Knowledge")
    .Validate(x => x.EmbeddingProvider is "google" or "ollama" or "none" && x.Dimensions == 768 && x.EmbeddingModel.Length is > 0 and <= 160 && x.EmbeddingModel.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.') && x.MaxDailyEmbeddingRequests is >= 1 and <= 100000 && x.PortableCandidateLimit is >= 100 and <= 10000 && x.MaxCollections is >= 1 and <= 100 && x.MaxDocumentsPerCollection is >= 1 and <= 1000 && x.ChunkCharacters is >= 200 and <= 1600 && x.ChunkOverlap >= 0 && x.ChunkOverlap < x.ChunkCharacters / 2 && x.TopK is >= 1 and <= 10 && x.ContextCharacters is >= 1000 and <= 12000, "Invalid knowledge limits or embedding configuration.").ValidateOnStart();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<NativeVectorStore>();
builder.Services.AddScoped<KnowledgeRetrieval>();
builder.Services.AddSingleton<IEmbeddingProvider, EmbeddingProvider>();
builder.Services.AddScoped<IBackgroundJobHandler, DocumentIngestHandler>();
builder.Services.AddHostedService<BackgroundJobWorker>();
builder.Services.AddSingleton<AdministrativeWriteLock>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<ConversationOrganization>();
builder.Services.AddScoped<PromptLibraryService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<DocumentExtractor>();
builder.Services.AddSingleton<AttachmentWriteLock>();
builder.Services.AddOptions<AttachmentOptions>().BindConfiguration("Attachments")
    .Validate(x => x.MaxFileBytes is >= 1024 and <= 8 * 1024 * 1024 && x.MaxFilesPerMessage is >= 1 and <= 8 && x.MaxMessageBytes >= x.MaxFileBytes && x.MaxMessageBytes <= 16 * 1024 * 1024 && x.MaxOwnerBytes >= x.MaxMessageBytes && x.MaxOwnerBytes <= 1024 * 1024 * 1024 && x.MaxExtractedCharacters is >= 1000 and <= 256000 && x.MaxPdfPages is >= 1 and <= 100 && x.ImageTokenEstimate is >= 1024 and <= 16384 && x.DraftRetentionDays is >= 1 and <= 365, "Invalid attachment limits.")
    .ValidateOnStart();
builder.Services.AddScoped<RunService>();
builder.Services.AddScoped<ContextBuilder>();
builder.Services.AddOptions<InferenceOptions>().BindConfiguration("Inference")
    .Validate(x => x.Provider is "google" or "ollama", "Inference Provider must be google or ollama.")
    .Validate(x => (x.DefaultModelId is null || x.Models.Any(m => m.Id == x.DefaultModelId)) && x.Models.All(m => m.ValidReasoning(x.Provider)), "Invalid default model or reasoning capabilities.")
    .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var url) && (url.Scheme is "http" or "https") && string.IsNullOrEmpty(url.UserInfo), "Inference BaseUrl must be a server-controlled HTTP endpoint.")
    .Validate(x => x.QueueCapacity is >= 1 and <= 64 && x.TimeoutSeconds is >= 5 and <= 600 && x.MaxInputCharacters is >= 100 and <= 32000, "Invalid inference capacity or limits.")
    .Validate(x => x.Models.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() == x.Models.Count && x.Models.All(m => !string.IsNullOrWhiteSpace(m.Id) && m.Id.Length <= 160 && m.ContextTokens is >= 1024 and <= 32768 && m.MaxOutputTokens >= 128 && m.MaxOutputTokens < m.ContextTokens && m.SupportsStreaming), "Invalid model profiles.")
    .ValidateOnStart();
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Inference:BaseUrl"] ?? "http://localhost:11434/");
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddHttpClient("GoogleAI", client => { client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"); client.Timeout = Timeout.InfiniteTimeSpan; });
builder.Services.AddSingleton<IInferenceProvider>(services => services.GetRequiredService<Microsoft.Extensions.Options.IOptions<InferenceOptions>>().Value.Provider == "google"
    ? new GoogleAiProvider(services.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleAI"), services.GetRequiredService<Microsoft.Extensions.Options.IOptions<InferenceOptions>>())
    : new OllamaProvider(services.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama")));
builder.Services.AddSingleton<ModelCatalog>();
builder.Services.AddSingleton<ModelPresentation>();
builder.Services.AddSingleton<GenerationScheduler>();
builder.Services.AddSingleton<SubscriptionLimits>();
builder.Services.AddHostedService<GenerationWorker>();
builder.Services.AddHostedService<RunRecoveryWorker>();
builder.Services.AddHostedService<EventRetentionWorker>();
// Upload/import endpoints need larger bodies. Ordinary JSON endpoints keep a small per-request limit.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = 10 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 9 * 1024 * 1024);

var app = builder.Build();
if (builder.Configuration.GetValue<bool>("InitializeDatabase"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        Console.WriteLine("AiNexus 資料庫與 migrations 初始化完成。");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex is ApiException api ? api.Message : LocalDatabaseSettings.Diagnose(ex));
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}
if (builder.Configuration.GetValue<bool>("VerifySqlCapabilities"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SqlVectorCapabilities>().VerifyAsync(builder.Configuration["VerificationOutput"] ?? "sql-capabilities.json", CancellationToken.None);
    }
    catch (Exception ex) { Console.Error.WriteLine(LocalDatabaseSettings.Diagnose(ex)); Environment.ExitCode = 1; }
    await app.DisposeAsync();
    return;
}
if (builder.Configuration.GetValue<bool>("VerifyConnections"))
{
    using var scope = app.Services.CreateScope();
    if (!await ConnectionVerifier.VerifyAsync(scope.ServiceProvider, builder.Configuration, builder.Environment.ContentRootPath, CancellationToken.None)) Environment.ExitCode = 1;
    await app.DisposeAsync();
    return;
}
app.Use(async (http, next) =>
{
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    http.Response.Headers["Referrer-Policy"] = "no-referrer";
    http.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self'; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
    http.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    try { await next(http); }
    catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested || http.Response.HasStarted) { }
    catch (Exception ex) when (!http.Response.HasStarted)
    {
        var error = ex as ApiException;
        var status = error?.Status ?? (ex is BadHttpRequestException bad ? bad.StatusCode : ex is AntiforgeryValidationException ? 403 : 503);
        var code = error?.Code ?? (status == 403 ? "csrf_invalid" : status == 413 ? "request_too_large" : status == 400 ? "invalid_request" : "service_unavailable");
        var detail = error?.Message ?? (status == 403 ? "安全驗證已失效，請重新載入頁面。" : status == 413 ? "上傳內容超過大小上限，請減少檔案或拆分內容。" : status == 400 ? "請求格式不正確。" : "服務暫時無法使用，請稍後重試。");
        if (error is null && status == 503)
        {
            app.Logger.LogWarning("Request failed ({ErrorType}), trace {TraceId}.", ex.GetType().Name, http.TraceIdentifier);
        }
        await Results.Problem(statusCode: status, title: detail, type: $"urn:ai-nexus:problem:{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = http.TraceIdentifier }).ExecuteAsync(http);
    }
});
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseDefaultFiles();
app.UseStaticFiles();
app.Use(async (http, next) =>
{
    // JSON-escaped UTF-16 characters can occupy six bytes. Keep prompt limits usable for Chinese clients too.
    var bodyLimit = http.Request.Path == "/api/v1/attachments" ? 9 * 1024 * 1024 : http.Request.Path == "/api/v1/conversations/import" ? 8 * 1024 * 1024 :
        http.Request.Path is var inputPath && (inputPath == "/api/v1/runs" || inputPath == "/api/v1/context") ? Math.Max(65536, http.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<InferenceOptions>>().Value.MaxInputCharacters * 6 + 8192) :
        http.Request.Path.StartsWithSegments("/api/v1/prompt-templates") ? 12000 * 6 + 8192 : 65536;
    if (http.Request.ContentLength > bodyLimit) throw new ApiException(413, "request_too_large", "上傳內容超過大小上限。");
    var bodySize = http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (bodySize is { IsReadOnly: false }) bodySize.MaxRequestBodySize = bodyLimit;
    if (http.Request.Path.StartsWithSegments("/api/v1") && !http.Request.Path.StartsWithSegments("/api/v1/auth")) http.RequestServices.GetRequiredService<StorageReadiness>().RequireConfigured();
    await next(http);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments("/api/v1") && http.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
    await next(http);
});
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi().AllowAnonymous();
app.MapNexusApi();
app.MapNexusAuthentication();
// Unknown API paths must never return the SPA's HTML document.
app.Map("/api/{**path}", () => Results.NotFound()).RequireAuthorization();
app.MapFallbackToFile("index.html").AllowAnonymous();
if (builder.Configuration.GetValue<bool>("Storage:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<StorageReadiness>().RequireConfigured();
    await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Database.MigrateAsync();
}
await app.RunAsync();
public partial class Program;
