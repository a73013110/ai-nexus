using System.Text.RegularExpressions;
using AiNexus.Data.App.Scheduling;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Scheduling.Engine;

/// <summary>一個任務實際生效的排程：程式宣告加上 <c>Scheduling:Tasks</c> 的覆寫。</summary>
public sealed record ScheduledTaskPlan(ScheduledTaskRegistration Registration, Schedule Schedule, TimeSpan Timeout, bool Enabled)
{
    public ScheduledTaskDefinition Definition => Registration.Definition;
    public string Key => Registration.Definition.Key;

    /// <summary>不能執行的原因；可執行時為 null。設定停用優先，其次是任務自己宣告的條件。</summary>
    public string? UnavailableReason(IServiceProvider services) =>
        !Enabled ? $"設定已停用（Scheduling:Tasks:{Key}:Enabled）" : Definition.Unavailable?.Invoke(services);
}

/// <summary>
/// 已註冊的排程任務。建立時檢查 Key 的格式與唯一性、設定覆寫的鍵都是已註冊的任務，任何一項不符就讓啟動失敗
/// （<see cref="SchedulingModule.VerifyStartup"/>），不會等到排程時才默默略過。
/// </summary>
[Singleton]
public sealed partial class ScheduledTaskCatalog
{
    private readonly Dictionary<string, ScheduledTaskPlan> byKey;

    public ScheduledTaskCatalog(IEnumerable<ScheduledTaskRegistration> registrations, IOptions<SchedulingOptions> options)
    {
        var settings = options.Value.Tasks;
        var all = registrations.ToList();
        if (all.FirstOrDefault(x => !KeyFormat().IsMatch(x.Definition.Key) || x.Definition.Key.Length > ScheduledTask.KeyMaxLength) is { } invalid)
            throw new InvalidOperationException($"Scheduled task key '{invalid.Definition.Key}' must be lower-case words separated by dots, at most {ScheduledTask.KeyMaxLength} characters.");
        var duplicates = all.GroupBy(x => x.Definition.Key, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException("Each scheduled task key can be registered only once: " + string.Join(", ", duplicates));
        var unknown = settings.Keys.Where(key => all.All(x => x.Definition.Key != key)).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException("Scheduling:Tasks names unknown tasks: " + string.Join(", ", unknown));

        byKey = all.Select(registration => Plan(registration, settings.GetValueOrDefault(registration.Definition.Key)))
            .ToDictionary(x => x.Key, StringComparer.Ordinal);
        All = [.. byKey.Values.OrderBy(x => x.Definition.Category, StringComparer.Ordinal).ThenBy(x => x.Definition.Name, StringComparer.Ordinal)];
    }

    /// <summary>依分類、名稱排序，管理頁直接沿用這個順序。</summary>
    public IReadOnlyList<ScheduledTaskPlan> All { get; }

    public ScheduledTaskPlan? Find(string key) => byKey.GetValueOrDefault(key);

    private static ScheduledTaskPlan Plan(ScheduledTaskRegistration registration, ScheduledTaskSettings? settings)
    {
        var definition = registration.Definition;
        var schedule = settings switch
        {
            { Interval: { } interval } => Schedule.Every(interval),
            { Cron: { } cron } => Schedule.Cron(cron, "cron：" + cron),
            _ => definition.Schedule,
        };
        return new(registration, schedule, settings?.Timeout ?? definition.Timeout, settings?.Enabled ?? true);
    }

    [GeneratedRegex("^[a-z0-9-]+(\\.[a-z0-9-]+)+$")]
    private static partial Regex KeyFormat();
}
