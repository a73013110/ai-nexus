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
            var (schema, name) = StoreObject(entity);
            // 實體型別可能來自 Platform（日誌管線的 DiagnosticEvent），那時只檢查組態的位置。
            var types = configurations[entity.ClrType].Append(entity.ClrType).Where(t => t.Assembly == Assemblies.Data);
            return types.Where(t => Folder(t) != schema).Select(t => $"{t.FullName} → {schema}.{name}");
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

    /// <summary>每張表與檢視都有 <see cref="AppDbContext"/> 屬性，名稱＝資料庫物件名（檢視去掉 <c>vw_</c>），知道表名就知道怎麼查；沒有獨立查詢的附屬與函式對應除外。</summary>
    [Fact]
    public void EveryTableHasADbSetNamedAfterIt()
    {
        var properties = typeof(AppDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToDictionary(p => p.PropertyType.GetGenericArguments()[0], p => p.Name);
        var expected = EntityTypes().Where(e => !e.IsOwned() && (e.GetTableName() ?? e.GetViewName()) is not null)
            .ToDictionary(e => e.ClrType, e => (e.GetTableName() ?? e.GetViewName())!.Replace("vw_", "", StringComparison.Ordinal));
        var wrong = expected.Where(e => properties.GetValueOrDefault(e.Key) != e.Value)
            .Select(e => $"public DbSet<{e.Key.Name}> {e.Value} => Set<{e.Key.Name}>();").Order(StringComparer.Ordinal).ToList();
        Assert.True(wrong.Count == 0, "Declare these properties in AppDbContext:\n" + string.Join("\n", wrong));
    }

    /// <summary>實體＝資料庫物件名的單數。模組命名空間與實體撞名時改用複數的模組名與 schema（<c>Projects/Project</c>），不是替實體另外取名。</summary>
    [Fact]
    public void EveryEntityIsNamedAfterItsTable()
    {
        var wrong = EntityTypes().Where(e => !e.IsOwned() && (e.GetTableName() ?? e.GetViewName()) is not null)
            .Select(e => (Entity: e.ClrType.Name, Table: (e.GetTableName() ?? e.GetViewName())!.Replace("vw_", "", StringComparison.Ordinal)))
            .Where(x => x.Entity != Singular(x.Table)).Select(x => $"{x.Table} → {Singular(x.Table)} (is {x.Entity})").Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.True(wrong.Count == 0, "Name each entity after its table:\n" + string.Join("\n", wrong));
    }

    // 向量表的維度在字尾（ChunkEmbeddings768 → ChunkEmbedding768）。
    private static string Singular(string table)
    {
        var name = table.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        var suffix = table[name.Length..];
        if (name.EndsWith("ies", StringComparison.Ordinal)) return name[..^3] + "y" + suffix;
        if (name.EndsWith("ches", StringComparison.Ordinal) || name.EndsWith("ses", StringComparison.Ordinal) || name.EndsWith("xes", StringComparison.Ordinal)) return name[..^2] + suffix;
        return (name.EndsWith('s') ? name[..^1] : name) + suffix;
    }

    /// <summary>實體對應的資料庫物件：表，沒有表時是檢視（AttributedPunch）或函式（UsageEntry）。</summary>
    private static (string Schema, string? Name) StoreObject(IEntityType entity)
    {
        if (entity.GetTableName() is { } table) return (entity.GetSchema() ?? "dbo", table);
        if (entity.GetViewName() is { } view) return (entity.GetViewSchema() ?? "dbo", view);
        var function = entity.GetFunctionMappings().FirstOrDefault()?.StoreFunction;
        return (function?.Schema ?? "dbo", function?.Name);
    }

    private static string? Folder(Type type)
        => type.Namespace is { } ns && ns.StartsWith(AppNamespace, StringComparison.Ordinal) ? ns[AppNamespace.Length..].Split('.')[0].ToLowerInvariant() : null;

    private static IReadOnlyList<IEntityType> EntityTypes()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=.;Database=AiNexus").Options);
        return [.. db.Model.GetEntityTypes()];
    }
}
