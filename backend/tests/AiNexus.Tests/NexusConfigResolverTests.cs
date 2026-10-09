using AiNexus.Platform.Configuration;
using AiNexus.Features.Configuration;
using AiNexus.Features.Inference;
using AiNexus.Features.Knowledge;
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
        var app = Path.Combine(root, "backend", "src", "AiNexus.Api");
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
    public void ProviderCatalogsAndSecretsAreIndependentFromEmbeddingSelection()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Inference:Providers:Ollama:Enabled"] = "true", ["Inference:Providers:Ollama:Endpoint"] = "http://local-ai:11434/",
            ["Inference:ModelPolicy:DefaultModelId"] = "ollama/qwen3:8b", ["Inference:Providers:Ollama:Models:default:Id"] = "qwen3:8b",
            ["Inference:Providers:Google:Models:default:Id"] = "google-only", ["Inference:Providers:Google:ApiKey"] = "fixture",
            ["Inference:Execution:TimeoutSeconds"] = "300", ["Inference:ModelPolicy:ShowModelNames"] = "false",
            ["Prompts:DefaultSystemInstruction"] = "local instruction", ["Knowledge:Embedding:Provider"] = "none",
            ["Knowledge:Retrieval:TopK"] = "4"
        });
        var inference = new InferenceOptions(); var knowledge = new KnowledgeOptions();
        NexusSettings.Inference(config, inference); NexusSettings.Knowledge(config, knowledge);
        Assert.Equal("ollama/qwen3:8b", Assert.Single(inference.Models).Id); Assert.Equal("qwen3:8b", inference.Models[0].NativeId); Assert.Equal("ollama", inference.Models[0].Provider); Assert.Equal("fixture", inference.GoogleApiKey);
        Assert.Equal("http://local-ai:11434/", inference.BaseUrl); Assert.False(inference.ShowModelNames);
        Assert.Equal(300, inference.TimeoutSeconds); Assert.Equal("local instruction", inference.SystemPrompt);
        Assert.Equal("none", knowledge.EmbeddingProvider); Assert.Equal(4, knowledge.TopK);
    }
    [Fact]
    public void SourceSqlSettingsPreserveEncryptionTrustAndTimeoutWithoutLeakingIntoNexus()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Integrations:Sources:Gdweb:Database:Server"] = "fixture",
            ["Integrations:Sources:Gdweb:Database:Name"] = "source",
            ["Integrations:Sources:Gdweb:Database:User"] = "reader",
            ["Integrations:Sources:Gdweb:Database:Password"] = "fixture",
            ["Integrations:Sources:Gdweb:Database:TrustServerCertificate"] = "true",
            ["Integrations:Sources:Gdweb:Database:ConnectTimeoutSeconds"] = "7"
        });
        NexusSettings.SourceConnections(config);
        var sql = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(AiNexus.Features.Integrations.LegacyGdwebDatabase.ConnectionString(config));
        Assert.Equal(Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Mandatory, sql.Encrypt);
        Assert.True(sql.TrustServerCertificate); Assert.Equal(7, sql.ConnectTimeout);
        Assert.Equal(Microsoft.Data.SqlClient.ApplicationIntent.ReadOnly, sql.ApplicationIntent);
        Assert.Null(config.GetConnectionString("Nexus"));
    }
}
