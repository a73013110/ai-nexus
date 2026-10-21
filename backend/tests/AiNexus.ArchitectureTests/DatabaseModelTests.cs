using AiNexus.Data.App;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 資料表只在 <c>AiNexus.Data</c> 定義，且依「資料庫 → 模組（schema）→ 資料表」放置（ADR 0013）：
/// 知道 <c>identity.Users</c> 就知道檔案是 <c>AiNexus.Data/App/Identity/User.cs</c>。
/// </summary>
public sealed class DatabaseModelTests
{
    private const string AppNamespace = "AiNexus.Data.App.";

    /// <summary>use case 只引用資料表；在 Features 或 Host 宣告組態會讓同一張表出現兩個對應位置。</summary>
    [Fact]
    public void OnlyTheDataProjectConfiguresTables()
    {
        var offending = Assemblies.Features.GetTypes().Concat(Assemblies.Host.GetTypes())
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(offending.Count == 0, "Move these configurations to AiNexus.Data/App/<Module>/<Entity>.cs:\n" + string.Join("\n", offending));
    }

    /// <summary>實體與其組態所在的資料夾（namespace）就是資料表的 schema。</summary>
    [Fact]
    public void FoldersMatchTableSchemas()
    {
        var configurations = Assemblies.Data.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                .Select(i => (Entity: i.GetGenericArguments()[0], Configuration: t)))
            .ToLookup(x => x.Entity, x => x.Configuration);
        var mismatched = EntityTypes().SelectMany(entity =>
        {
            // 只經由函式讀取的實體（UsageEntry）沒有表，以函式的 schema 為準。
            var function = entity.GetTableName() is null ? entity.GetFunctionMappings().FirstOrDefault()?.StoreFunction : null;
            var schema = (function is null ? entity.GetSchema() : function.Schema) ?? "dbo";
            // 實體型別可能來自 Platform（日誌管線的 DiagnosticEvent），那時只檢查組態的位置。
            var types = configurations[entity.ClrType].Append(entity.ClrType).Where(t => t.Assembly == Assemblies.Data);
            return types.Where(t => Folder(t) != schema).Select(t => $"{t.FullName} → {schema}.{entity.GetTableName()}");
        }).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.True(mismatched.Count == 0, "Put each entity and its configuration in AiNexus.Data/App/<Module>/ whose lowercase name is the schema:\n" + string.Join("\n", mismatched));
    }

    /// <summary>每個由 Data 組態對應的實體都要有組態類別，否則它是靠慣例被發現、沒有人看得到它的對應。</summary>
    [Fact]
    public void EveryEntityHasAConfiguration()
    {
        var configured = Assemblies.Data.GetTypes()
            .SelectMany(t => t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .Select(i => i.GetGenericArguments()[0]).ToHashSet();
        var missing = EntityTypes().Where(e => !e.IsOwned() && !configured.Contains(e.ClrType)).Select(e => e.ClrType.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Add an IEntityTypeConfiguration for:\n" + string.Join("\n", missing));
    }

    private static string? Folder(Type type)
        => type.Namespace is { } ns && ns.StartsWith(AppNamespace, StringComparison.Ordinal) ? ns[AppNamespace.Length..].Split('.')[0].ToLowerInvariant() : null;

    private static IReadOnlyList<IEntityType> EntityTypes()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=.;Database=AiNexus").Options);
        return [.. db.Model.GetEntityTypes()];
    }
}
