using System.Reflection;
using System.Text.Json.Nodes;
using AiNexus.Features;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Work.Knowledge;
using AiNexus.Features.Work.Repositories;
using AiNexus.Platform;
using AiNexus.Platform.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Host;

/// <summary>設定依模組區段綁定；未知的鍵或無效的值會阻止啟動，並在錯誤訊息中指出該鍵。</summary>
public sealed class SettingsTests
{
    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json"))) return directory.FullName;
        throw new InvalidOperationException("Repository root (global.json) was not found.");
    }

    private static readonly string Host = Path.Combine(RepositoryRoot(), "backend", "src", "AiNexus.Host");

    /// <summary>所有設定根：宣告了 <c>Section</c> 常數的型別，由反射找出。</summary>
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
        var gitea = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<GiteaOptions>>().Value);
        Assert.Contains("BaseUrl", gitea.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDatabaseEntryIsDeclaredByAModuleAndSourcesStayReadOnly()
    {
        using var services = Settings(new() { ["Databases:LegacyGdweb:Server"] = "sql", ["Databases:Gdweb:ReadOnly"] = "false" });
        var error = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<DatabasesOptions>>().Value);
        Assert.Contains("Databases:LegacyGdweb is not a database of this application. Known: App, Gdweb, Meiho.", error.Message, StringComparison.Ordinal);
        Assert.Contains("Databases:Gdweb:ReadOnly must be true", error.Message, StringComparison.Ordinal);
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
        // 範本中的 Windows 磁碟路徑在 Linux 測試機上不是有效根路徑，因此這裡改用暫存目錄覆寫。
        settings["Files:StoragePath"] = Path.GetTempPath();
        // Mail:Transport 在 Production 必須明確設定；Local／Secrets 範本給開發機用（Development 會補 Pickup），這裡以 Production 驗證時補上停用。
        if (string.IsNullOrEmpty(settings["Mail:Transport"])) settings["Mail:Transport"] = "Disabled";
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
                else if (value is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0 && IsSecretKey(key, path))
                    yield return (next, text);
            }
        }

        // Databases 底下的 User 也視為秘密：資料庫帳號通常與密碼成對使用，只留下帳號也會洩漏連線資訊。
        // 其他區段的 User 不算，避免把一般的使用者名稱誤判為秘密。
        static bool IsSecretKey(string key, string path) =>
            key is "Password" or "BindPassword" or "ApiKey"
            || (key == "User" && path.StartsWith("Databases:", StringComparison.Ordinal));
    }

    private static ServiceProvider Settings(Dictionary<string, string?> values)
    {
        var settings = new ConfigurationManager();
        settings.AddInMemoryCollection(values);
        settings["Files:StoragePath"] = Path.GetTempPath();
        return Services(settings);
    }

    /// <summary>以真實的模組註冊套用指定設定，但不啟動 Host。</summary>
    private static ServiceProvider Services(ConfigurationManager settings)
    {
        var builder = WebApplication.CreateEmptyBuilder(new() { EnvironmentName = "Production" });
        builder.Configuration.AddConfiguration(settings);
        builder.AddPlatform();
        builder.AddFeatures();
        return builder.Services.BuildServiceProvider();
    }
}
