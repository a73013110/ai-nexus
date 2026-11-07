using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Platform.Google;
using AiNexus.Features.Platform.Google.Calendar;
using AiNexus.Platform.Http;
using AiNexus.UnitTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Google;

/// <summary>經 DI 組出與正式站相同的鏈（服務帳戶 → 權杖快取 → 授權處理常式 → 日曆客戶端），只把網路換成假的回應。</summary>
public sealed class GoogleCalendarClientTests : IDisposable
{
    private const string TokenUri = "https://oauth2.example.test/token";
    private const string ClientEmail = "sync@example-project.iam.gserviceaccount.test";
    private const string CalendarId = "team@example.test";
    private readonly string root = Directory.CreateTempSubdirectory("ainexus-google-").FullName;
    private readonly RSA key = RSA.Create(2048);
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 7, 1, 0, 0, TimeSpan.Zero));
    private readonly List<string> assertions = [];
    private readonly List<string?> authorizations = [];
    private Func<HttpRequestMessage, HttpResponseMessage> api = _ => Json(HttpStatusCode.OK, """{"summary":"團隊行事曆","timeZone":"Asia/Taipei"}""");
    private Func<HttpResponseMessage>? token;

    public void Dispose()
    {
        key.Dispose();
        Directory.Delete(root, recursive: true);
    }

    private ServiceProvider Build(bool withKey = true)
    {
        if (withKey)
        {
            var file = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["type"] = "service_account",
                ["client_email"] = ClientEmail,
                ["private_key_id"] = "key-1",
                ["private_key"] = key.ExportPkcs8PrivateKeyPem(),
                ["token_uri"] = TokenUri,
            });
            File.WriteAllText(Path.Combine(root, "google.json"), file);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IHostEnvironment>(new EnvironmentFixture { ContentRootPath = root });
        services.AddSingleton(Options.Create(new GoogleOptions { ServiceAccountKeyFile = withKey ? "google.json" : "" }));
        services.AddSingleton<GoogleServiceAccount>();
        services.AddSingleton<GoogleAccessTokens>();
        services.AddSingleton<GoogleCalendarClient>();
        services.AddHttpClient(ControlledHttpClients.Tools).ConfigurePrimaryHttpMessageHandler(() => new StubHttpHandler(Token));
        services.AddGoogleApiClient(GoogleCalendarClient.HttpClientName, GoogleCalendarClient.Scope)
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpHandler(request =>
            {
                authorizations.Add(request.Headers.Authorization?.ToString());
                return api(request);
            }));
        return services.BuildServiceProvider();
    }

    private HttpResponseMessage Token(HttpRequestMessage request)
    {
        Assert.Equal(TokenUri, request.RequestUri!.AbsoluteUri);
        var form = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        var assertion = form.Split('&').Single(x => x.StartsWith("assertion=", StringComparison.Ordinal))["assertion=".Length..];
        assertions.Add(Uri.UnescapeDataString(assertion));
        return token?.Invoke() ?? Json(HttpStatusCode.OK, $$"""{"access_token":"token-{{assertions.Count}}","expires_in":3600}""");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Task<GoogleResponse<GoogleCalendarInfo>> ReadAsync(ServiceProvider provider)
        => provider.GetRequiredService<GoogleCalendarClient>().GetCalendarAsync(CalendarId, CancellationToken.None);

    [Fact]
    public async Task ASignedAssertionIsExchangedOnceAndTheTokenIsReused()
    {
        using var provider = Build();

        var first = await ReadAsync(provider);
        await ReadAsync(provider);

        Assert.True(first.IsOk);
        Assert.Equal("團隊行事曆", first.Value!.Summary);
        Assert.Equal(["Bearer token-1", "Bearer token-1"], authorizations);
        var parts = Assert.Single(assertions).Split('.');
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        Assert.Equal("key-1", header.RootElement.GetProperty("kid").GetString());
        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        Assert.Equal(ClientEmail, claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal(GoogleCalendarClient.Scope, claims.RootElement.GetProperty("scope").GetString());
        Assert.Equal(TokenUri, claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal(clock.GetUtcNow().AddHours(1).ToUnixTimeSeconds(), claims.RootElement.GetProperty("exp").GetInt64());
        Assert.False(claims.RootElement.TryGetProperty("sub", out _));
    }

    [Fact]
    public async Task TheTokenIsRenewedBeforeItExpires()
    {
        using var provider = Build();
        await ReadAsync(provider);
        clock.Advance(TimeSpan.FromMinutes(56));
        await ReadAsync(provider);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], authorizations);
    }

    [Fact]
    public async Task ARejectedTokenIsDroppedForTheNextRequest()
    {
        using var provider = Build();
        api = _ => Json(HttpStatusCode.Unauthorized, """{"error":{"code":401,"message":"Invalid Credentials"}}""");
        var rejected = await ReadAsync(provider);
        api = _ => Json(HttpStatusCode.OK, "{}");
        await ReadAsync(provider);

        Assert.Equal(GoogleStatus.CredentialRejected, rejected.Status);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], authorizations);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "rateLimitExceeded", GoogleStatus.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, "dailyLimitExceeded", GoogleStatus.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, "forbidden", GoogleStatus.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, "notFound", GoogleStatus.NotFound)]
    [InlineData(HttpStatusCode.Conflict, "duplicate", GoogleStatus.Conflict)]
    [InlineData(HttpStatusCode.Gone, "deleted", GoogleStatus.Gone)]
    [InlineData(HttpStatusCode.BadRequest, "invalid", GoogleStatus.Invalid)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "backendError", GoogleStatus.Unavailable)]
    public async Task FailuresAreClassifiedByStatusAndReason(HttpStatusCode status, string reason, GoogleStatus expected)
    {
        using var provider = Build();
        api = _ => Json(status, $$$"""{"error":{"code":{{{(int)status}}},"message":"Problem {{{reason}}}","errors":[{"reason":"{{{reason}}}"}]}}""");
        var response = await ReadAsync(provider);
        Assert.Equal(expected, response.Status);
        Assert.Equal($"Problem {reason}", response.Message);
    }

    [Fact]
    public async Task TooManyRequestsCarriesTheServerRetryAfter()
    {
        using var provider = Build();
        api = _ =>
        {
            var response = Json(HttpStatusCode.TooManyRequests, "{}");
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(7));
            return response;
        };
        var result = await ReadAsync(provider);
        Assert.Equal(GoogleStatus.RateLimited, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(7), result.RetryAfter);
    }

    [Fact]
    public async Task ARejectedServiceAccountIsReportedWithoutCallingTheApi()
    {
        using var provider = Build();
        token = () => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Invalid JWT Signature."}""");
        var result = await ReadAsync(provider);
        Assert.Equal(GoogleStatus.CredentialRejected, result.Status);
        Assert.Equal("Invalid JWT Signature.", result.Message);
        Assert.Empty(authorizations);
    }

    [Fact]
    public async Task AMissingKeyIsNotConfigured()
    {
        using var provider = Build(withKey: false);
        var result = await ReadAsync(provider);
        Assert.Equal(GoogleStatus.NotConfigured, result.Status);
        Assert.Null(provider.GetRequiredService<GoogleServiceAccount>().ClientEmail);
        Assert.Empty(assertions);
    }
}
