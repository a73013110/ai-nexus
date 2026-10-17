using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Platform;

public sealed class DatabasesOptionsTests
{
    [Fact]
    public void SqlLoginsUseTheConnectionStringBuilderAlwaysEncryptedWithoutPersistedSecrets()
    {
        var databases = new DatabasesOptions { ["Nexus"] = new() { Server = "sql.fixture.test", Name = "AiNexus", User = "login", Password = "fixture;\"password", TrustServerCertificate = true, ConnectTimeoutSeconds = 7 } };
        var connection = new SqlConnectionStringBuilder(databases.ConnectionString("nexus"));
        Assert.Equal("sql.fixture.test", connection.DataSource); Assert.Equal("AiNexus", connection.InitialCatalog);
        Assert.Equal("login", connection.UserID); Assert.Equal("fixture;\"password", connection.Password);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, connection.Encrypt); Assert.True(connection.TrustServerCertificate);
        Assert.False(connection.PersistSecurityInfo); Assert.False(connection.IntegratedSecurity);
        Assert.Equal(7, connection.ConnectTimeout); Assert.Equal(ApplicationIntent.ReadWrite, connection.ApplicationIntent);
    }

    [Fact]
    public void IntegratedSecurityReadOnlyAndCatalogOverrideShapeTheSameEntry()
    {
        var database = new DatabaseOptions { Server = "sql.fixture.test", Name = "source", IntegratedSecurity = true, ReadOnly = true };
        Assert.True(database.Configured);
        var connection = new SqlConnectionStringBuilder(database.ConnectionString("master"));
        Assert.True(connection.IntegratedSecurity); Assert.Empty(connection.UserID); Assert.Empty(connection.Password);
        Assert.Equal(ApplicationIntent.ReadOnly, connection.ApplicationIntent);
        Assert.Equal("master", connection.InitialCatalog); Assert.Equal(SqlConnectionEncryptOption.Mandatory, connection.Encrypt);
    }

    [Theory]
    [InlineData("", "AiNexus", "login", "fixture", false, false)]
    [InlineData("sql", "", "login", "fixture", false, false)]
    [InlineData("sql", "AiNexus", "", "", false, false)]
    [InlineData("sql", "AiNexus", "login", "", false, false)]
    [InlineData("sql", "AiNexus", "login", "fixture", false, true)]
    [InlineData("sql", "AiNexus", "", "", true, true)]
    public void ConfiguredNeedsServerNameAndASignIn(string server, string name, string user, string password, bool integrated, bool expected)
    {
        var database = new DatabaseOptions { Server = server, Name = name, User = user, Password = password, IntegratedSecurity = integrated };
        Assert.Equal(expected, database.Configured);
        Assert.Equal(expected, database.ConnectionString().Length > 0);
    }

    [Fact]
    public void MissingEntryIsUnconfigured()
    {
        var databases = new DatabasesOptions();
        Assert.False(databases.For("Nexus").Configured);
        Assert.Empty(databases.ConnectionString("Nexus"));
    }

    [Fact]
    public void DeclaredEntriesBindAndValidate()
    {
        using var services = Services(new()
        {
            ["Databases:Nexus:Server"] = "sql", ["Databases:Nexus:Name"] = "AiNexus", ["Databases:Nexus:User"] = "login", ["Databases:Nexus:Password"] = "fixture",
            ["Databases:Source:Server"] = "sql", ["Databases:Source:Name"] = "views", ["Databases:Source:IntegratedSecurity"] = "true", ["Databases:Source:ReadOnly"] = "true"
        });
        var databases = services.GetRequiredService<IOptions<DatabasesOptions>>().Value;
        Assert.True(databases.For("Nexus").Configured);
        Assert.Equal(ApplicationIntent.ReadOnly, new SqlConnectionStringBuilder(databases.ConnectionString(SourceDatabase.Name)).ApplicationIntent);
    }

    [Theory]
    [InlineData("Databases:Nexsu:Server", "sql", "Databases:Nexsu is not a database")]
    [InlineData("Databases:Nexus:User", "login", "Databases:Nexus:Password is missing")]
    [InlineData("Databases:Nexus:ConnectTimeoutSeconds", "0", "Databases:Nexus:ConnectTimeoutSeconds must be")]
    [InlineData("Databases:Source:ReadOnly", "false", "Databases:Source:ReadOnly must be true")]
    public void InvalidEntriesFailAndNameTheKey(string key, string value, string message)
    {
        using var services = Services(new() { [key] = value });
        var error = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<DatabasesOptions>>().Value);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegratedSecurityRejectsASqlLoginAndNeedsTheServer()
    {
        using var services = Services(new() { ["Databases:Nexus:IntegratedSecurity"] = "true", ["Databases:Nexus:Password"] = "fixture" });
        var error = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<DatabasesOptions>>().Value);
        Assert.Contains("Databases:Nexus:IntegratedSecurity=true", error.Message, StringComparison.Ordinal);
        Assert.Contains("Databases:Nexus:Server is required", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownFieldFailsBinding()
    {
        using var services = Services(new() { ["Databases:Nexus:ConnectionString"] = "Server=x" });
        var error = Assert.ThrowsAny<InvalidOperationException>(() => services.GetRequiredService<IOptions<DatabasesOptions>>().Value);
        // 字典項目的綁定錯誤會被包裝一層，真正指出未知欄位的訊息在 InnerException。
        Assert.Contains("DatabaseOptions: 'ConnectionString'", error.InnerException?.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider Services(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddSettings<DatabasesOptions, DatabasesOptionsValidator>(DatabasesOptions.Section);
        services.AddDatabase("Nexus");
        services.AddSqlDatabase<SourceDatabase>();
        return services.BuildServiceProvider();
    }

    private sealed class SourceDatabase : ISqlDatabaseDefinition
    {
        public static string Name => "Source";
        public static bool ReadOnly => true;
    }
}
