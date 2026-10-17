namespace AiNexus.Platform.Configuration;

/// <summary>統一的組態來源載入器，取代主機預設的組態來源，整合分層 JSON、環境變數與命令列參數。</summary>
public static class NexusConfiguration
{
    /// <param name="builder">要替換組態來源的主機建置器。</param>
    /// <param name="args">命令列參數，優先權高於所有其他來源。</param>
    public static void Load(WebApplicationBuilder builder, string[] args)
    {
        ResolvedConfigPaths? resolved = null;
        // 測試主機絕不載入實體機密檔；正式環境則使用明確指定的路徑或環境變數。
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
