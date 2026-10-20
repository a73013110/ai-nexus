using System.Globalization;
using AiNexus.Data.App.Mail;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Mail;

/// <summary>寄信測試的參數說明：名稱＝模型建構子的參數名，<paramref name="Type"/> 為 <c>text</c>、<c>number</c>、<c>flag</c>、<c>time</c>。</summary>
public sealed record MailSampleParameter(string Name, string Type, bool Required, string Description, string? Example);

/// <summary>
/// 樣本渲染時可用的環境：目前時間與系統設定（傳輸、寄件地址），供範例值使用。刻意不含操作者或任何使用者的資料：
/// 範例值會顯示在管理頁並被複製進程式碼，人名、信箱一律用 <see cref="ExampleName"/> 這類虛構值。
/// </summary>
public sealed record MailSampleContext(DateTimeOffset Now, string Transport, string From)
{
    /// <summary>範例用的虛構人名。</summary>
    public const string ExampleName = "王小明";

    /// <summary>範例用的虛構信箱（遮罩後），保留網域 example.com 不會誤寄。</summary>
    public const string ExampleMaskedEmail = "w***@example.com";
}

/// <summary>
/// 寄信測試可選的一種系統信。各模組在自己的 <c>AddServices</c> 以 <see cref="MailSampleRegistration.AddMailSample{TModel}"/> 登記，
/// Mail 模組因此不必引用上層模組（避免循環依賴）。模型由同一個委派建立：宣告參數時會被記錄成說明，送出時套用填寫的值。
/// </summary>
public abstract class MailSample
{
    public abstract string Category { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract MailPriority Priority { get; }
    public abstract bool Sensitive { get; }
    /// <summary>範本類別名，給開發者照抄 <c>new XxxMail()</c>。</summary>
    public abstract string TemplateType { get; }
    public abstract string ModelType { get; }

    /// <summary>參數說明；範例值取自 <paramref name="context"/>。</summary>
    public abstract IReadOnlyList<MailSampleParameter> Describe(MailSampleContext context);

    /// <summary>以填寫的值渲染；沒填的參數用範例值，型別不符或有未知參數回 <see cref="MailErrors.SampleInvalid"/>。</summary>
    public abstract Result<RenderedMail> Render(MailRenderer renderer, MailSampleContext context, IReadOnlyDictionary<string, string?> values);
}

internal sealed class MailSample<TModel>(MailTemplate<TModel> template, string name, string description, Func<MailSampleValues, TModel> model) : MailSample
{
    public override string Category => template.Category;
    public override string Name => name;
    public override string Description => description;
    public override MailPriority Priority => template.Priority;
    public override bool Sensitive => template.Sensitive;
    public override string TemplateType => template.GetType().Name;
    public override string ModelType => typeof(TModel).Name;

    public override IReadOnlyList<MailSampleParameter> Describe(MailSampleContext context)
    {
        var values = new MailSampleValues(context, new Dictionary<string, string?>());
        model(values);
        return values.Parameters;
    }

    public override Result<RenderedMail> Render(MailRenderer renderer, MailSampleContext context, IReadOnlyDictionary<string, string?> values)
    {
        var supplied = new MailSampleValues(context, values);
        var built = model(supplied);
        if (!supplied.Valid) return MailErrors.SampleInvalid;
        return renderer.Render(template, built);
    }
}

/// <summary>
/// 樣本模型的參數來源：每個方法宣告一個參數並回傳填寫的值（沒填則回範例值）。委派要無條件依序呼叫，說明才會完整；
/// 值不合法時回傳範例值並標記 <see cref="Valid"/> 為 false，由呼叫端回錯誤，不在委派裡 throw。
/// </summary>
public sealed class MailSampleValues
{
    private readonly IReadOnlyDictionary<string, string?> values;
    private readonly List<MailSampleParameter> parameters = [];
    private readonly HashSet<string> used = new(StringComparer.Ordinal);
    private bool invalid;

    internal MailSampleValues(MailSampleContext context, IReadOnlyDictionary<string, string?> values)
    {
        Context = context;
        this.values = values;
    }

    public MailSampleContext Context { get; }
    internal IReadOnlyList<MailSampleParameter> Parameters => parameters;
    internal bool Valid => !invalid && values.Keys.All(used.Contains);

    public string Text(string name, string description, string example)
        => Read(name, "text", true, description, example, x => x.Length > 0 ? x : null) ?? example;

    /// <summary>可省略的文字；填空白代表 null。</summary>
    public string? OptionalText(string name, string description, string? example)
    {
        Declare(name, "text", false, description, example);
        if (!values.TryGetValue(name, out var value)) return example;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public int Number(string name, string description, int example)
        => Read(name, "number", true, description, example.ToString(CultureInfo.InvariantCulture),
            x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : (int?)null) ?? example;

    public bool Flag(string name, string description, bool example)
        => Read(name, "flag", true, description, example ? "true" : "false", x => bool.TryParse(x, out var b) ? b : (bool?)null) ?? example;

    public DateTimeOffset Time(string name, string description, DateTimeOffset example)
        => Read(name, "time", true, description, example.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            x => DateTimeOffset.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : (DateTimeOffset?)null) ?? example;

    private T? Read<T>(string name, string type, bool required, string description, string example, Func<string, T?> parse)
    {
        Declare(name, type, required, description, example);
        if (!values.TryGetValue(name, out var value)) return default;
        var parsed = value is null ? default : parse(value.Trim());
        if (parsed is null) invalid = true;
        return parsed;
    }

    private void Declare(string name, string type, bool required, string description, string? example)
    {
        if (!used.Add(name)) throw new InvalidOperationException($"Mail sample parameter '{name}' is declared twice.");
        parameters.Add(new(name, type, required, description, example));
    }
}

public static class MailSampleRegistration
{
    /// <summary>
    /// 登記一種系統信供寄信測試選用。<paramref name="model"/> 用 <see cref="MailSampleValues"/> 依模型建構子的參數順序宣告每個參數，
    /// 名稱與建構子參數同名（測試會檢查），開發者看到的用法範例才對得上。
    /// </summary>
    public static IServiceCollection AddMailSample<TModel>(this IServiceCollection services, MailTemplate<TModel> template, string name, string description,
        Func<MailSampleValues, TModel> model)
        => services.AddSingleton<MailSample>(new MailSample<TModel>(template, name, description, model));
}
