using AiNexus.Platform.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiNexus.UnitTests.Platform;

public sealed class LocalDatabaseSettingsTests
{
    [Fact]
    public void DatabaseCredentialsUseConnectionStringBuilderAndRemainOutOfErrors()
    {
        var config = new ConfigurationManager();
        config["Database:Server"] = "sql.fixture.test";
        config["Database:User"] = "login"; config["Database:Password"] = "fixture;\"password";
        LocalDatabaseSettings.Apply(config);
        var connection = new SqlConnectionStringBuilder(config.GetConnectionString("Nexus"));
        Assert.Equal("AiNexus", connection.InitialCatalog); Assert.Equal("sql.fixture.test", connection.DataSource);
        Assert.Equal("fixture;\"password", connection.Password); Assert.False(connection.PersistSecurityInfo);
    }

    [Fact]
    public void ExplicitSelfSignedSqlCertificatesRemainEncryptedInProduction()
    {
        var config = new ConfigurationManager(); config["Database:User"] = "fixture"; config["Database:Password"] = "fixture"; config["Database:TrustServerCertificate"] = "true";
        LocalDatabaseSettings.Apply(config);
        var connection = new SqlConnectionStringBuilder(config.GetConnectionString("Nexus"));
        Assert.True(connection.TrustServerCertificate);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, connection.Encrypt);
    }

    [Theory]
    [InlineData("Encrypt=False;TrustServerCertificate=False")]
    [InlineData("Encrypt=False;TrustServerCertificate=True")]
    public void FullConnectionStringCannotBypassProductionTls(string settings)
    {
        var config = new ConfigurationManager();
        config["ConnectionStrings:Nexus"] = "Server=fixture;Database=AiNexus;User ID=fixture;Password=fixture;" + settings;
        Assert.Throws<InvalidOperationException>(() => LocalDatabaseSettings.Apply(config));
    }
}
