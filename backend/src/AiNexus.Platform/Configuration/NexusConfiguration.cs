namespace AiNexus.Platform.Configuration;

public static class NexusConfiguration
{
    /// <param name="builder">The host whose configuration sources are replaced.</param>
    /// <param name="args">Command-line arguments; they override every other source.</param>
    /// <param name="normalize">Feature-owned derivations from machine settings, applied after all sources are loaded.</param>
    public static void Load(WebApplicationBuilder builder, string[] args, Action<ConfigurationManager>? normalize = null)
    {
        ResolvedConfigPaths? resolved = null;
        // Test hosts never load machine credentials. Production uses explicit paths or environment.
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
            normalize?.Invoke(builder.Configuration);
        }
    }
}
