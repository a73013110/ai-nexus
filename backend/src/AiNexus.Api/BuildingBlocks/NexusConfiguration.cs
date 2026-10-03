namespace AiNexus.BuildingBlocks;

public static class NexusConfiguration
{
    public static void Load(WebApplicationBuilder builder, string[] args)
    {
        // Test hosts never load machine credentials. Production uses explicit paths or environment.
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            var directory = new DirectoryInfo(builder.Environment.ContentRootPath);
            if (builder.Environment.IsDevelopment())
                while (directory.Parent is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
            var workspace = File.Exists(Path.Combine(directory.FullName, "global.json")) ? directory.FullName : builder.Environment.ContentRootPath;
            builder.Configuration["LocalWorkspaceRoot"] = workspace;
            var local = builder.Configuration["LocalConfigPath"] ?? Path.Combine(workspace, ".local", "config", "appsettings.Local.json");
            var secrets = builder.Configuration["SecretsConfigPath"] ?? Path.Combine(workspace, ".local", "secrets", "appsettings.Secrets.json");
            builder.Configuration.AddJsonFile(local, optional: true, reloadOnChange: false).AddJsonFile(secrets, optional: true, reloadOnChange: false);
        }
        builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
    }
}
