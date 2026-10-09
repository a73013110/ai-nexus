using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Integrations;

/// <summary>Reads the deployment settings (<c>Integrations</c>) into <see cref="IntegrationsOptions"/> and builds the read-only source connection strings.</summary>
public static class IntegrationsSettings
{
    public static void Bind(IConfiguration config, IntegrationsOptions options)
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
