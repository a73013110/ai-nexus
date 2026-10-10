using AiNexus.Features.Persistence;
using AiNexus.Platform.Data.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Platform;

public sealed class SqlDatabaseTests
{
    [Fact]
    public async Task SqlBindsParametersAndJoinsTheEfTransaction()
    {
        await using var factory = new NexusFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var sql = scope.ServiceProvider.GetRequiredService<ISqlDatabase<NexusDbContext>>();
        Assert.Equal("'; DROP TABLE Users;--", await sql.QuerySingleAsync<string>("SELECT @Value", new { Value = "'; DROP TABLE Users;--" }));
        await sql.ExecuteAsync("CREATE TABLE SqlProbe (Value TEXT NOT NULL)");
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            Assert.Equal(1, await sql.ExecuteAsync("INSERT INTO SqlProbe VALUES (@Value)", new { Value = "rollback" }));
            Assert.Equal(["rollback"], await sql.QueryAsync<string>("SELECT Value FROM SqlProbe"));
        }
        Assert.Equal(0, await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM SqlProbe"));
        Assert.Null(await sql.QuerySingleOrDefaultAsync<string>("SELECT Value FROM SqlProbe"));
    }
}
