namespace AiNexus.Platform.Configuration;

/// <summary>全域組態來源統一載入器。統一替換主機的組態來源，整合階層式 JSON、環境變數與命令列引數。</summary>
public static class NexusConfiguration
{
    /// <param name="builder">欲替換組態來源之主機應用程式建置器。</param>
    /// <param name="args">命令列參數；其優先權高於所有其他組態來源。</param>
    public static void Load(WebApplicationBuilder builder, string[] args)
    {
        ResolvedConfigPaths? resolved = null;
        // 測試主機絕不載入實體機密。正式環境則使用明確指定路徑或環境變數。
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            resolved = NexusConfigResolver.Resolve(
                builder.Environment.ContentRootPath,
                builder.Environment.EnvironmentName,
                builder.Configuration["LocalConfigPath"],
                builder.Configuration["SecretsConfigPath"],
                builder.Configuration["DataProtection:KeyRingPath"]
            );

            builder.Configuration["LocalWorkspaceRoot"] = resolved.WorkspaceRoot;
            if (resolved.LocalConfigPath is not null)
            {
                builder.Configuration.AddJsonFile(resolved.LocalConfigPath, optional: false, reloadOnChange: false);
            }
            if (resolved.SecretsConfigPath is not null)
            {
                builder.Configuration.AddJsonFile(resolved.SecretsConfigPath, optional: false, reloadOnChange: false);
            }
        }
        builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
        if (resolved is not null)
        {
            var keyRing = builder.Configuration["DataProtection:KeyRingPath"];
            builder.Configuration["DataProtection:KeyRingPath"] = string.IsNullOrWhiteSpace(keyRing)
                ? resolved.KeyRingPath : NexusConfigResolver.Absolute(builder.Environment.ContentRootPath, keyRing);
        }
    }
}
