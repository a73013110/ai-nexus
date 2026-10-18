using System.Diagnostics;
using AiNexus.Data.App;
using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Platform;

public sealed class ProcedureAuditTests
{
    private const string Procedure = "audit.usp_WriteAuditEvent";

    [Fact]
    public async Task WriteAuditEventRecordsTheCallersOwnerTraceAndTime()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var audits = new ProcedureAudits();
        await using var factory = new NexusFactory(clock: clock, services: audits.Register);
        var owner = Guid.NewGuid(); var resource = Guid.NewGuid();
        using (var activity = new Activity("procedure-audit").Start())
        using (var scope = factory.Services.CreateScope())
        {
            var sql = scope.ServiceProvider.GetRequiredService<ISqlDatabase<AppDbContext>>();
            await sql.ExecuteProcedureAsync<int>(Procedure, new { Action = "admin.probe", ResourceId = resource, Result = "saved", DetailsJson = """{"enabled":true,"count":2}""" }, auditOwner: owner);
        }
        var row = Assert.Single(await audits.AssertWrittenAsync(factory, Procedure, owner, clock.GetUtcNow(), "administration"));
        Assert.Equal((resource, "saved", (Guid?)null), (row.ResourceId!.Value, row.Result, row.ActorId));
    }

    [Fact]
    public async Task ContextIsResetOnEveryCallSoAWriteWithoutOwnerFailsOnTheSameConnection()
    {
        await using var factory = new NexusFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sql = scope.ServiceProvider.GetRequiredService<ISqlDatabase<AppDbContext>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await sql.ExecuteProcedureAsync<int>(Procedure, new { Action = "admin.probe", Result = "saved" }, auditOwner: Guid.NewGuid());
        // 同一條連線、同一個交易：上一筆的 owner 若殘留，這筆就會被記在別人名下。
        var error = await Assert.ThrowsAsync<SqlException>(() => sql.ExecuteProcedureAsync<int>(Procedure, new { Action = "admin.probe", Result = "saved" }));
        Assert.Equal(515, error.Number);
    }

    [Fact]
    public async Task MalformedDetailsAndUnsafeNamesAreRejected()
    {
        await using var factory = new NexusFactory();
        using var scope = factory.Services.CreateScope();
        var sql = scope.ServiceProvider.GetRequiredService<ISqlDatabase<AppDbContext>>();
        var malformed = await Assert.ThrowsAsync<SqlException>(() => sql.ExecuteProcedureAsync<int>(Procedure, new { Action = "admin.probe", Result = "saved", DetailsJson = "{" }, auditOwner: Guid.NewGuid()));
        Assert.Equal(51000, malformed.Number);
        Assert.Null(ProcedureError.Code(malformed));
        await Assert.ThrowsAsync<ArgumentException>(() => sql.ExecuteProcedureAsync<int>("audit.usp_WriteAuditEvent; DROP TABLE x", null, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => sql.QueryProcedureAsync<int>("dbo.sp_who"));
    }
}
