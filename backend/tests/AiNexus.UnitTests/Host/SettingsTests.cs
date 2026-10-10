using System.Reflection;
using System.Text.Json.Nodes;
using AiNexus.Features;
using AiNexus.Features.Inference;
using AiNexus.Features.Knowledge;
using AiNexus.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Host;

/// <summary>Settings are bound per module section; unknown keys and invalid values stop startup and name the key.</summary>
public sealed class SettingsTests
{
    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json"))) return directory.FullName;
        throw new InvalidOperationException("Repository root (global.json) was not found.");
    }

    private static readonly string Host = Path.Combine(RepositoryRoot(), "backend", "src", "AiNexus.Host");

    /// <summary>Every settings root: a type that declares the section it binds.</summary>
    private static readonly Type[] Roots = new[] { typeof(PlatformServices).Assembly, typeof(FeatureModules).Assembly }
        .SelectMany(x => x.GetTypes())
        .Where(x => x.GetField("Section", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true })
        .ToArray();

    [Fact]
    public void ProviderCatalogsAndSecretsAreIndependentFromEmbeddingSelection()
    {
        using var services = Settings(new()
        {
            ["Inference:Providers:Google:Enabled"] = "false", ["Inference:Providers:Google:ApiKey"] = "fixture",
            ["Inference:Providers:Ollama:Enabled"] = "true", ["Inference:Providers:Ollama:Endpoint"] = "http://local-ai:11434/",
            ["Inference:DefaultModelId"] = "ollama/qwen3:8b", ["Inference:Providers:Ollama:Models:default:Id"] = "qwen3:8b",
            ["Inference:TimeoutSeconds"] = "300", ["Inference:ShowModelNames"] = "false", ["Inference:SystemPrompt"] = "local instruction",
            ["Knowledge:Embedding:Provider"] = "none", ["Knowledge:Retrieval:TopK"] = "4"
        });
        var inference = services.GetRequiredService<IOptions<InferenceOptions>>().Value;
        var knowledge = services.GetRequiredService<IOptions<KnowledgeOptions>>().Value;
        var model = Assert.Single(inference.Models);
        Assert.Equal("ollama/qwen3:8b", model.Id); Assert.Equal("qwen3:8b", model.NativeId); Assert.Equal("ollama", model.Provider);
        Assert.Equal("ollama", Assert.Single(inference.ProviderConcurrency).Key);
        Assert.Equal("fixture", inference.Providers.Google.ApiKey); Assert.Equal("http://local-ai:11434/", inference.Providers.Ollama.Endpoint);
        Assert.False(inference.ShowModelNames); Assert.Equal(300, inference.TimeoutSeconds); Assert.Equal("local instruction", inference.SystemPrompt);
        Assert.Equal("none", knowledge.Embedding.Provider); Assert.Equal(4, knowledge.Retrieval.TopK);
    }

    [Fact]
    public void ImageCapabilityKeepsUnsetSeparateFromFalse()
    {
        using var services = Settings(new()
        {
            ["Inference:Providers:Ollama:Enabled"] = "true",
            ["Inference:Providers:Ollama:Models:default:Id"] = "qwen3:8b",
            ["Inference:Providers:Ollama:Models:vision:Id"] = "gemma3:4b", ["Inference:Providers:Ollama:Models:vision:SupportsImages"] = "false"
        });
        var models = services.GetRequiredService<IOptions<InferenceOptions>>().Value.Models.Where(x => x.Provider == "ollama").ToDictionary(x => x.NativeId);
        Assert.Null(models["qwen3:8b"].ImageCapabilityOverride);
        Assert.False(models["gemma3:4b"].ImageCapabilityOverride);
    }

    [Fact]
    public void UnknownKeyFailsAndNamesTheKey()
    {
        using var services = Settings(new() { ["Inference:Execution:TimeoutSeconds"] = "300" });
        var error = Assert.ThrowsAny<InvalidOperationException>(() => services.GetRequiredService<IOptions<InferenceOptions>>().Value);
        Assert.Contains("Execution", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidValueFailsAndNamesTheKey()
    {
        using var services = Settings(new() { ["Knowledge:Retrieval:TopK"] = "50", ["Repositories:Gitea:BaseUrl"] = "http://gitea.fixture.test/" });
        var knowledge = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<KnowledgeOptions>>().Value);
        Assert.Contains("Retrieval.TopK", knowledge.Message, StringComparison.Ordinal);
        var gitea = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<AiNexus.Features.Repositories.GiteaOptions>>().Value);
        Assert.Contains("BaseUrl", gitea.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySettingsRootIsValidatedOnStartByAGeneratedValidator()
    {
        Assert.True(Roots.Length >= 11, "Settings roots were not discovered.");
        using var services = Settings([]);
        foreach (var root in Roots)
        {
            var validators = ((IEnumerable<object>)services.GetServices(typeof(IValidateOptions<>).MakeGenericType(root))).ToArray();
            Assert.True(validators.Length == 1, $"{root.Name} needs exactly one validator.");
            Assert.True(validators[0].GetType().GetCustomAttribute<OptionsValidatorAttribute>() is not null
                || validators[0].GetType().GetMethod("Validate")!.GetCustomAttribute<System.CodeDom.Compiler.GeneratedCodeAttribute>() is not null,
                $"{root.Name} must use an [OptionsValidator] source-generated validator.");
        }
        Assert.NotEmpty(services.GetServices<IStartupValidator>());
    }

    [Theory]
    [InlineData("appsettings.Local.example.json")]
    [InlineData("appsettings.Production.example.json")]
    [InlineData("appsettings.Secrets.example.json")]
    public void DefaultsAndTemplatesBindAndValidate(string template)
    {
        var settings = new ConfigurationManager();
        settings.AddJsonFile(Path.Combine(Host, "appsettings.json")).AddJsonFile(Path.Combine(Host, template));
        // Windows drive paths in the templates are not rooted on Linux test machines.
        settings["Attachments:StoragePath"] = Path.GetTempPath();
        using var services = Services(settings);
        foreach (var root in Roots)
        {
            var options = typeof(IOptions<>).MakeGenericType(root);
            try { _ = options.GetProperty(nameof(IOptions<object>.Value))!.GetValue(services.GetRequiredService(options)); }
            catch (TargetInvocationException error) { Assert.Fail($"{template}, {root.Name}: {error.InnerException?.Message}"); }
        }
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Local.example.json")]
    [InlineData("appsettings.Production.example.json")]
    [InlineData("appsettings.Secrets.example.json")]
    public void PublicSettingsFilesHaveEmptySecrets(string file)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Host, file)))!;
        Assert.Empty(Secrets(json, "").Select(x => x.Path));

        static IEnumerable<(string Path, string Value)> Secrets(JsonNode node, string path)
        {
            if (node is not JsonObject values) yield break;
            foreach (var (key, value) in values)
            {
                var next = path.Length == 0 ? key : path + ":" + key;
                if (value is JsonObject) foreach (var secret in Secrets(value, next)) yield return secret;
                else if (value is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0
                    && (key is "Password" or "DnPass" or "ApiKey" || key == "User" && path.EndsWith("Database", StringComparison.Ordinal) || path == "ConnectionStrings"))
                    yield return (next, text);
            }
        }
    }

    private static ServiceProvider Settings(Dictionary<string, string?> values)
    {
        var settings = new ConfigurationManager();
        settings.AddInMemoryCollection(values);
        settings["Attachments:StoragePath"] = Path.GetTempPath();
        return Services(settings);
    }

    /// <summary>The real module registrations over the given configuration, without starting a host.</summary>
    private static ServiceProvider Services(ConfigurationManager settings)
    {
        var builder = WebApplication.CreateEmptyBuilder(new() { EnvironmentName = "Production" });
        builder.Configuration.AddConfiguration(settings);
        builder.AddPlatform();
        builder.AddFeatures();
        return builder.Services.BuildServiceProvider();
    }
}
