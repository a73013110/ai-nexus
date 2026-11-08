using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// appsettings.json 是設定的目錄：每個設定類別（有 <c>Section</c> 常數）的每個屬性都要列出預設值，
/// 秘密則列在 appsettings.Secrets.example.json，讀設定檔就知道能調什麼，不必翻程式碼。
/// 排程任務的 <c>Scheduling:Tasks</c> 由排程目錄在啟動時要求完整，不在這裡檢查。
/// </summary>
public sealed class SettingsTests
{
    private static readonly string HostRoot = Path.Combine(SourceTree.Root, "backend", "src", "AiNexus.Host");
    private static readonly Lazy<JsonElement> Defaults = new(() => Load("appsettings.json"));
    private static readonly Lazy<JsonElement> Secrets = new(() => Load("appsettings.Secrets.example.json"));

    /// <summary>不從設定讀、由其他設定推得的屬性（<c>Inference:Providers</c> 攤平而成）。</summary>
    private static readonly HashSet<string> Derived = new(["Inference:Models", "Inference:ProviderConcurrency"], StringComparer.Ordinal);

    [Fact]
    public void EverySettingIsListedInAppSettings()
    {
        var types = new[] { Assemblies.Platform, Assemblies.Features, Assemblies.Host }
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => (Type: type, Section: type.GetField("Section", BindingFlags.Public | BindingFlags.Static)))
            .Where(x => x.Section is { IsLiteral: true } && x.Section.FieldType == typeof(string))
            .ToList();
        Assert.NotEmpty(types);

        var missing = types
            .SelectMany(x => Paths(x.Type, ((string)x.Section!.GetRawConstantValue()!).Split(':')))
            .Where(path => !Contains(Defaults.Value, path) && !Contains(Secrets.Value, path))
            .Select(path => string.Join(':', path))
            .Where(path => !Derived.Contains(path))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.True(missing.Count == 0, "Settings missing from appsettings.json (or appsettings.Secrets.example.json for secrets):\n" + string.Join('\n', missing));
    }

    /// <summary>設定類別的鍵路徑：純量與集合是葉節點，其他類別往下展開。</summary>
    private static IEnumerable<string[]> Paths(Type type, string[] prefix)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // 繼承自框架型別的屬性（例如設定類別本身是 Dictionary 時的 Keys、Values）不是設定鍵。
            if (property.GetIndexParameters().Length > 0 || property.GetMethod is null
                || property.DeclaringType?.Assembly.GetName().Name?.StartsWith("AiNexus.", StringComparison.Ordinal) != true) continue;
            var path = (string[])[.. prefix, property.Name];
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (IsLeaf(propertyType))
            {
                if (property.SetMethod?.IsPublic == true) yield return path;
            }
            else if (typeof(IEnumerable).IsAssignableFrom(propertyType))
            {
                yield return path;
            }
            else if (propertyType.IsClass)
            {
                foreach (var nested in Paths(propertyType, path)) yield return nested;
            }
        }
    }

    private static bool IsLeaf(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type == typeof(TimeSpan) || type == typeof(DateTimeOffset) || type == typeof(DateTime) || type == typeof(Guid) || type == typeof(Uri);

    /// <summary>設定鍵不分大小寫，比照設定系統。</summary>
    private static bool Contains(JsonElement root, string[] path)
    {
        var current = root;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object) return false;
            var match = current.EnumerateObject().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match.Value.ValueKind == JsonValueKind.Undefined) return false;
            current = match.Value;
        }
        return true;
    }

    private static JsonElement Load(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(HostRoot, file)), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        return document.RootElement.Clone();
    }
}
