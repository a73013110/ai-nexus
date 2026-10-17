using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.ArchitectureTests;

/// <summary>SQL Server 模型的規則；migrations 是由這個模型產生的。</summary>
public sealed class DatabaseModelTests
{
    [Fact]
    public void EveryTableAndColumnHasDescription()
    {
        var missing = EntityTypes()
            .SelectMany(entity => entity.GetProperties().Where(p => string.IsNullOrWhiteSpace(p.GetComment())).Select(p => $"{entity.GetTableName()}.{p.Name}")
                .Prepend(string.IsNullOrWhiteSpace(entity.GetComment()) ? entity.GetTableName() : null))
            .OfType<string>().ToArray();
        Assert.True(missing.Length == 0, "Add [Comment] (or HasComment for shadow properties) to: " + string.Join(", ", missing));
    }

    [Fact]
    public void SchemaMatchesOwningModule()
    {
        var mismatched = EntityTypes()
            .Where(entity => entity.GetSchema() != ExpectedSchema(entity.ClrType))
            .Select(entity => $"{entity.ClrType.Name} → {entity.GetSchema()}.{entity.GetTableName()} (expected {ExpectedSchema(entity.ClrType)})")
            .ToArray();
        Assert.True(mismatched.Length == 0, string.Join(", ", mismatched));
    }

    // AuditEvent 放在 Persistence，是因為 NexusDbContext 會替每個模組遮罩並寫入它；但資料表屬於 audit 結構描述，所以這裡特別對應。
    private static string ExpectedSchema(Type type)
        => type == typeof(AuditEvent) ? "audit" : type.Namespace!.Split('.')[2].ToLowerInvariant();

    private static IEnumerable<IEntityType> EntityTypes()
    {
        using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer("Server=.;Database=AiNexus").Options);
        // [Comment] 只存在於設計期模型，執行期模型會丟棄，所以必須改用 IDesignTimeModel 才讀得到。
        return db.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
    }

    [Fact]
    public void SqlServerModelMatchesMigrationsAndEnforcesOwnerRunUniqueness()
    {
        using var db = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.False(db.Database.HasPendingModelChanges());
        var run = db.Model.FindEntityType(typeof(GenerationRun))!;
        Assert.Contains(run.GetIndexes(), x => x.IsUnique && x.Properties.Count == 1 && x.Properties[0].Name == "ActiveOwnerId");
        Assert.Contains(run.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "OwnerId", "IdempotencyKey" }));
    }
}
