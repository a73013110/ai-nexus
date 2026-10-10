using AiNexus.Platform.Configuration;
using AiNexus.Features.Integrations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiNexus.Tests;

public sealed class NexusConfigResolverTests
{
    private static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nexus-config-fixture"));
    [Fact]
    public void DevelopmentFindsRepositoryAndKeepsSecretsOutsideApp()
    {
        var root = Root;
        var app = Path.Combine(root, "backend", "src", "AiNexus.Host");
        var files = new HashSet<string> { Path.Combine(root, "global.json"), Path.Combine(root, ".local", "config", "appsettings.Local.json"), Path.Combine(root, ".local", "secrets", "appsettings.Secrets.json") };
        var paths = NexusConfigResolver.Resolve(app, "Development", fileExists: files.Contains);
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
        var paths = NexusConfigResolver.Resolve(app, "Production", fileExists: files.Contains);
        Assert.Equal(production, paths.LocalConfigPath); Assert.Equal(secret, paths.SecretsConfigPath);
        Assert.Equal(Path.Combine(Root, "keys"), paths.KeyRingPath);
        var empty = NexusConfigResolver.Resolve(app, "Production", fileExists: p => p.Contains(".local"));
        Assert.Null(empty.LocalConfigPath); Assert.Null(empty.SecretsConfigPath);
    }
    [Fact]
    public void ExplicitRelativePathsResolveAgainstContentRoot()
    {
        var app = Path.Combine(Root, "app");
        var paths = NexusConfigResolver.Resolve(app, "Production", "../config/site.json", "../config/private.json", "../keys", fileExists: _ => false);
        Assert.Equal(Path.Combine(Root, "config", "site.json"), paths.LocalConfigPath);
        Assert.Equal(Path.Combine(Root, "config", "private.json"), paths.SecretsConfigPath);
        Assert.Equal(Path.Combine(Root, "keys"), paths.KeyRingPath);
    }
    [Fact]
    public void SourceSqlSettingsPreserveEncryptionTrustAndTimeoutWithoutLeakingIntoNexus()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Integrations:Gdweb:Database:Server"] = "fixture",
            ["Integrations:Gdweb:Database:Name"] = "source",
            ["Integrations:Gdweb:Database:User"] = "reader",
            ["Integrations:Gdweb:Database:Password"] = "fixture",
            ["Integrations:Gdweb:Database:TrustServerCertificate"] = "true",
            ["Integrations:Gdweb:Database:ConnectTimeoutSeconds"] = "7"
        });
        IntegrationsSettings.SourceConnections(config);
        var sql = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(AiNexus.Features.Integrations.LegacyGdwebDatabase.ConnectionString(config));
        Assert.Equal(Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Mandatory, sql.Encrypt);
        Assert.True(sql.TrustServerCertificate); Assert.Equal(7, sql.ConnectTimeout);
        Assert.Equal(Microsoft.Data.SqlClient.ApplicationIntent.ReadOnly, sql.ApplicationIntent);
        Assert.Null(config.GetConnectionString("Nexus"));
    }
}
