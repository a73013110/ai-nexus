namespace AiNexus.Platform.Configuration;

/// <summary>解析完成的組態路徑：工作區根目錄、本機設定、機密設定與資料保護金鑰圈的路徑。</summary>
public sealed record ResolvedConfigPaths(string WorkspaceRoot, string? LocalConfigPath, string? SecretsConfigPath, string KeyRingPath);

/// <summary>組態路徑解析器：依環境（開發、測試、正式）決定各設定檔與機密檔的位置。</summary>
public static class AppConfigResolver
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
        // 正式環境絕不自動尋找版控庫中 .local 底下的本機認證檔。
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
