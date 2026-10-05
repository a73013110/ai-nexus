namespace AiNexus.BuildingBlocks;

public static class NexusConfiguration
{
    public static void Load(WebApplicationBuilder builder, string[] args)
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
            NexusSettings.SourceConnections(builder.Configuration);
        }
        var version = builder.Configuration.GetValue("ConfigurationVersion", 1);
        if (version != NexusSettings.Version) throw new InvalidOperationException("Unsupported ConfigurationVersion. Use the templates matching this release.");
    }
}
