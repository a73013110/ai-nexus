using AiNexus.Platform.Configuration;

namespace AiNexus.UnitTests.Platform;

public sealed class AppConfigResolverTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ainexus-config-fixture"));

    [Fact]
    public void DevelopmentFindsRepositoryAndKeepsSecretsOutsideApp()
    {
        var root = Root;
        var app = Path.Combine(root, "backend", "src", "AiNexus.Host");
        var files = new HashSet<string> { Path.Combine(root, "global.json"), Path.Combine(root, ".local", "config", "appsettings.Local.json"), Path.Combine(root, ".local", "secrets", "appsettings.Secrets.json") };
        var paths = AppConfigResolver.Resolve(app, "Development", fileExists: files.Contains);
        Assert.Equal(root, paths.WorkspaceRoot);
        Assert.Equal(Path.Combine(root, ".local", "config", "appsettings.Local.json"), paths.LocalConfigPath);
        Assert.Equal(Path.Combine(root, ".local", "keys"), paths.KeyRingPath);
    }

    [Fact]
    public void ProductionUsesSiblingConfigAndKeysAndIgnoresDevelopmentFiles()
    {
        var app = Path.Combine(Root, "app");
        var production = Path.Combine(Root, "config", "appsettings.Production.json");
        var secret = Path.Combine(Root, "config", "appsettings.Secrets.json");
        var files = new HashSet<string> { production, secret, Path.Combine(app, "appsettings.Local.json"), Path.Combine(app, ".local", "config", "appsettings.Local.json") };
        var paths = AppConfigResolver.Resolve(app, "Production", fileExists: files.Contains);
        Assert.Equal(production, paths.LocalConfigPath); Assert.Equal(secret, paths.SecretsConfigPath);
        Assert.Equal(Path.Combine(Root, "keys"), paths.KeyRingPath);
        var empty = AppConfigResolver.Resolve(app, "Production", fileExists: p => p.Contains(".local"));
        Assert.Null(empty.LocalConfigPath); Assert.Null(empty.SecretsConfigPath);
    }

    [Fact]
    public void ExplicitRelativePathsResolveAgainstContentRoot()
    {
        var app = Path.Combine(Root, "app");
        var paths = AppConfigResolver.Resolve(app, "Production", "../config/site.json", "../config/private.json", "../keys", fileExists: _ => false);
        Assert.Equal(Path.Combine(Root, "config", "site.json"), paths.LocalConfigPath);
        Assert.Equal(Path.Combine(Root, "config", "private.json"), paths.SecretsConfigPath);
        Assert.Equal(Path.Combine(Root, "keys"), paths.KeyRingPath);
    }
}
