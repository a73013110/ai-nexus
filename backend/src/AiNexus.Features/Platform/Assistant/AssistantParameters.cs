using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>
/// 工具的參數宣告：交給模型的 JSON Schema 與讀取模型填的值用同一份，名稱與型別不會兩處各寫一套。
/// 只有三種型別（日期、文字、選項），全部可省略：工具在參數沒填時用畫面的條件或預設值。
/// </summary>
public sealed class AssistantParameters
{
    private enum Kind { Date, Text, Choice }

    private sealed record Parameter(Kind Kind, string Description, IReadOnlyList<string> Values);

    private readonly Dictionary<string, Parameter> parameters = new(StringComparer.Ordinal);

    public static AssistantParameters None => new();

    /// <summary>日期，格式 yyyy-MM-dd。</summary>
    public AssistantParameters Date(string name, string description) => Add(name, new(Kind.Date, description, []));

    /// <summary>短文字（人名、帳號、關鍵字），最多 100 字。</summary>
    public AssistantParameters Text(string name, string description) => Add(name, new(Kind.Text, description, []));

    /// <summary>從 <paramref name="values"/> 選一個。</summary>
    public AssistantParameters Choice(string name, string description, params string[] values) => Add(name, new(Kind.Choice, description, values));

    /// <summary>交給模型的 JSON Schema。</summary>
    public JsonObject Schema()
    {
        var properties = new JsonObject();
        foreach (var (name, parameter) in parameters)
        {
            var property = new JsonObject { ["type"] = "string", ["description"] = parameter.Description };
            if (parameter.Kind == Kind.Date) property["format"] = "date";
            if (parameter.Kind == Kind.Choice) property["enum"] = new JsonArray([.. parameter.Values.Select(x => JsonValue.Create(x))]);
            properties[name] = property;
        }
        return new JsonObject { ["type"] = "object", ["properties"] = properties };
    }

    /// <summary>
    /// 讀模型填的參數：沒宣告的忽略，空字串與 null 視同沒填；型別不對時回傳說明（交回給模型改正），不拋出。
    /// </summary>
    public (AssistantArguments? Arguments, string? Problem) Read(JsonObject values)
    {
        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, parameter) in parameters)
        {
            if (values[name] is not JsonValue value || value.GetValueKind() != JsonValueKind.String) continue;
            var text = value.GetValue<string>().Trim();
            if (text.Length == 0) continue;
            var valid = parameter.Kind switch
            {
                Kind.Date => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                Kind.Choice => parameter.Values.Contains(text),
                _ => text.Length <= 100
            };
            if (!valid) return (null, $"參數 {name} 不正確：{parameter.Description}");
            read[name] = text;
        }
        return (new(read), null);
    }

    private AssistantParameters Add(string name, Parameter parameter)
    {
        parameters.Add(name, parameter);
        return this;
    }
}

/// <summary>已依 <see cref="AssistantParameters"/> 驗證過的參數，沒填的是 null。</summary>
public sealed class AssistantArguments(IReadOnlyDictionary<string, string> values)
{
    public static AssistantArguments Empty { get; } = new(new Dictionary<string, string>());

    /// <summary>一個都沒填：工具改用畫面的條件。</summary>
    public bool IsEmpty => values.Count == 0;

    public string? Text(string name) => values.GetValueOrDefault(name);

    public DateOnly? Date(string name)
        => values.TryGetValue(name, out var text) ? DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

    /// <summary>稽核記的條件：只有參數名稱與值（日期、選項、短文字）。</summary>
    public IReadOnlyDictionary<string, string> Values => values;
}
