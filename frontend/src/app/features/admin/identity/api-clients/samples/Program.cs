// Program.cs：註冊一次，需要的地方注入 CredentialVerificationClient。
// appsettings.json 放 "CredentialVerification": { "BaseUrl": "{{baseUrl}}" }；金鑰放 user-secrets、環境變數 CredentialVerification__ApiKey 或秘密庫，不進版控。
builder.Services.AddHttpClient<CredentialVerificationClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["CredentialVerification:BaseUrl"] ?? throw new InvalidOperationException("CredentialVerification:BaseUrl is required."));
    http.DefaultRequestHeaders.Authorization = new("Bearer", builder.Configuration["CredentialVerification:ApiKey"]);
    http.Timeout = TimeSpan.FromSeconds(15);
});

// 登入時：回傳要顯示給使用者的訊息，null 代表登入成功。
async Task<string?> LoginAsync(CredentialVerificationClient credentials, ILogger logger, string account, string password, CancellationToken cancellationToken)
{
    try
    {
        switch (await credentials.VerifyAsync(account, password, cancellationToken))
        {
            case CredentialVerificationResult.Verified verified:
                // 以 verified.User.Sid 對應自己的使用者（帳號改名也不變）；能不能登入、有哪些權限由自己的系統決定。
                return null;
            case CredentialVerificationResult.RateLimited limited:
                return $"登入失敗次數過多，請 {(limited.RetryAfter?.TotalSeconds ?? 60):0} 秒後再試";
            default:
                return "帳號或密碼不正確";
        }
    }
    catch (CredentialVerificationException error)
    {
        // 金鑰無效或 AD 連不上，不是密碼錯：記下查證代碼交給驗證服務的管理員。
        logger.LogError(error, "Credential verification unavailable, issue {IssueCode}", error.IssueCode);
        return "暫時無法驗證帳密，請聯絡系統管理員";
    }
    catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
    {
        // 連不上或逾時（HttpClient.Timeout）。
        logger.LogError(error, "Credential verification unreachable");
        return "暫時無法連線到驗證服務，請稍後再試";
    }
}
