using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AiNexus.Tests;

/// <summary>Rules for the SQL Server model that the migrations are generated from.</summary>
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

    // AuditEvent lives in Persistence because NexusDbContext masks and writes it for every module; its table belongs to Audit.
    private static string ExpectedSchema(Type type)
        => type == typeof(AuditEvent) ? "audit" : type.Namespace!.Split('.')[2].ToLowerInvariant();

    private static IEnumerable<IEntityType> EntityTypes()
    {
        using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer("Server=.;Database=AiNexus").Options);
        // Comments are design-time metadata; the runtime model drops them.
        return db.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
    }
}
