using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Integrations;

/// <summary>Builds the read-only source connection strings from <c>Integrations:&lt;Source&gt;:Database</c> before services are registered.</summary>
public static class IntegrationsSettings
{
    public static void SourceConnections(ConfigurationManager config)
    {
        foreach (var (source, key) in new[] { ("Gdweb", "LegacyGdweb"), ("Meiho", "LegacyMeiho") })
        {
            if (!string.IsNullOrWhiteSpace(config.GetConnectionString(key))) continue;
            var sql = config.GetSection($"{IntegrationsOptions.Section}:{source}:Database");
            if (string.IsNullOrWhiteSpace(sql["User"]) || string.IsNullOrEmpty(sql["Password"])) continue;
            if (string.IsNullOrWhiteSpace(sql["Server"]) || string.IsNullOrWhiteSpace(sql["Name"]))
                throw new InvalidOperationException($"{IntegrationsOptions.Section}:{source}:Database requires Server and Name.");
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
