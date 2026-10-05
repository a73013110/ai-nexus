using AiNexus.Modules.Inference;
using AiNexus.Modules.Integrations;
using AiNexus.Modules.Knowledge;
using Microsoft.Data.SqlClient;

namespace AiNexus.BuildingBlocks;

/// <summary>Deployment schema is separated from the domain options used by services.</summary>
public static class NexusSettings
{
    public const int Version = 2;
    public static void Inference(IConfiguration config, InferenceOptions options)
    {
        var section = config.GetSection("Inference");
        options.Provider = section["Provider"] ?? options.Provider;
        section.GetSection("Execution").Bind(options);
        section.GetSection("ModelPolicy").Bind(options);
        var providers = section.GetSection("Providers");
        options.BaseUrl = providers["Ollama:Endpoint"] ?? options.BaseUrl;
        options.GoogleApiKey = providers["Google:ApiKey"] ?? "";
        options.SystemPrompt = config["Prompts:DefaultSystemInstruction"] ?? options.SystemPrompt;
        var provider = providers.GetSection(options.Provider == "ollama" ? "Ollama" : "Google");
        options.DefaultModelId = provider["DefaultModelId"];
        options.Models = provider.GetSection("Models").GetChildren().Select(x => x.Get<ModelProfile>()!).ToList();
        // v1 deployments continue to work while operators run Migrate-Settings.ps1.
        section.Bind(options);
    }

    public static void Knowledge(IConfiguration config, KnowledgeOptions options)
    {
        var section = config.GetSection("Knowledge");
        var embedding = section.GetSection("Embedding");
        options.EmbeddingProvider = embedding["Provider"] ?? options.EmbeddingProvider;
        options.EmbeddingModel = embedding["Model"] ?? options.EmbeddingModel;
        options.Dimensions = embedding.GetValue("Dimensions", options.Dimensions);
        options.InputFormat = embedding["InputFormat"] ?? options.InputFormat;
        options.QueryInstruction = embedding["QueryInstruction"] ?? options.QueryInstruction;
        options.Revision = embedding["Revision"] ?? options.Revision;
        options.TimeoutSeconds = embedding.GetValue("TimeoutSeconds", options.TimeoutSeconds);
        options.MaxDailyEmbeddingRequests = embedding.GetValue("MaxDailyRequests", options.MaxDailyEmbeddingRequests);
        section.GetSection("Indexing").Bind(options);
        section.GetSection("Retrieval").Bind(options);
        section.Bind(options);
    }

    public static void Integrations(IConfiguration config, IntegrationsOptions options)
    {
        config.GetSection("Integrations:Sources").Bind(options);
        config.GetSection("Integrations").Bind(options);
    }

    public static void SourceConnections(ConfigurationManager config)
    {
        foreach (var (source, key) in new[] { ("Gdweb", "LegacyGdweb"), ("Meiho", "LegacyMeiho") })
        {
            if (!string.IsNullOrWhiteSpace(config.GetConnectionString(key))) continue;
            var sql = config.GetSection($"Integrations:Sources:{source}:Database");
            if (string.IsNullOrWhiteSpace(sql["User"]) || string.IsNullOrEmpty(sql["Password"])) continue;
            if (string.IsNullOrWhiteSpace(sql["Server"]) || string.IsNullOrWhiteSpace(sql["Name"]))
                throw new InvalidOperationException($"Integrations:Sources:{source}:Database requires Server and Name.");
            config[$"ConnectionStrings:{key}"] = new SqlConnectionStringBuilder
            {
                DataSource = sql["Server"], InitialCatalog = sql["Name"], UserID = sql["User"], Password = sql["Password"],
                Encrypt = true, TrustServerCertificate = sql.GetValue<bool>("TrustServerCertificate"),
                ConnectTimeout = sql.GetValue("ConnectTimeoutSeconds", 10), PersistSecurityInfo = false,
                ApplicationIntent = ApplicationIntent.ReadOnly
            }.ConnectionString;
        }
    }
}
