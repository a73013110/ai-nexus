namespace AiNexus.Platform.Configuration;

public sealed record ResolvedConfigPaths(string WorkspaceRoot, string? LocalConfigPath, string? SecretsConfigPath, string KeyRingPath);

public static class NexusConfigResolver
{
    public static string Absolute(string root, string path) => Path.GetFullPath(path, root);

    public static ResolvedConfigPaths Resolve(string contentRootPath, string environmentName,
        string? explicitLocalPath = null, string? explicitSecretsPath = null, string? explicitKeyRingPath = null,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var root = Path.GetFullPath(contentRootPath);
        var development = environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase);
        var directory = new DirectoryInfo(root);
        if (development)
            while (directory.Parent is not null && !fileExists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        var workspace = development && fileExists(Path.Combine(directory.FullName, "global.json")) ? directory.FullName : root;
        var sibling = Path.GetFullPath("..", root);
        // Production never discovers the repository's .local credentials.
        var localCandidates = development
            ? new[] { Path.Combine(workspace, ".local", "config", "appsettings.Local.json"), Path.Combine(root, "appsettings.Local.json") }
            : new[] { Path.Combine(sibling, "config", $"appsettings.{environmentName}.json"), Path.Combine(root, "config", $"appsettings.{environmentName}.json"), Path.Combine(root, "appsettings.Local.json") };
        var secretCandidates = development
            ? new[] { Path.Combine(workspace, ".local", "secrets", "appsettings.Secrets.json"), Path.Combine(root, "appsettings.Secrets.json") }
            : new[] { Path.Combine(sibling, "config", "appsettings.Secrets.json"), Path.Combine(root, "secrets", "appsettings.Secrets.json"), Path.Combine(root, "appsettings.Secrets.json") };
        var local = string.IsNullOrWhiteSpace(explicitLocalPath) ? localCandidates.FirstOrDefault(fileExists) : Absolute(root, explicitLocalPath);
        var secrets = string.IsNullOrWhiteSpace(explicitSecretsPath) ? secretCandidates.FirstOrDefault(fileExists) : Absolute(root, explicitSecretsPath);
        var keys = string.IsNullOrWhiteSpace(explicitKeyRingPath)
            ? development ? Path.Combine(workspace, ".local", "keys") : Path.Combine(sibling, "keys")
            : Absolute(root, explicitKeyRingPath);
        return new(workspace, local, secrets, keys);
    }
}
