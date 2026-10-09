using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Integrations;

internal static class SourceConnection
{
    /// <summary>External sources are read-only and always encrypted, whatever the configured connection string says.</summary>
    public static string ReadOnly(IConfiguration configuration, string sourceId)
    {
        var connection = new SqlConnectionStringBuilder(configuration.GetConnectionString(IntegrationsOptions.ConnectionKey(sourceId)) ?? "")
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };
        if (connection.Encrypt == SqlConnectionEncryptOption.Optional) connection.Encrypt = SqlConnectionEncryptOption.Mandatory;
        return connection.ConnectionString;
    }
}
