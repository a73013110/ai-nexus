using System.Text.RegularExpressions;
using AiNexus.Data.App.Scheduling;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Scheduling.Engine;

/// <summary>
/// 一個任務實際生效的設定：排程（設定或任務固定的）、逾時與開關。<see cref="ExecutesHere"/> 是這個程序是否執行它
/// （<c>Scheduling:Execute</c> 與 <c>Scheduling:ExecuteOnly</c>）；不執行時其他程序照樣可以執行。
/// </summary>
public sealed record ScheduledTaskPlan(ScheduledTaskRegistration Registration, Schedule Schedule, TimeSpan Timeout, bool Enabled, bool ExecutesHere)
{
    public ScheduledTaskDefinition Definition => Registration.Definition;
    public string Key => Registration.Definition.Key;

    /// <summary>不能執行的原因；可執行時為 null。設定停用優先，其次是任務自己宣告的條件。</summary>
    public string? UnavailableReason(IServiceProvider services) =>
        !Enabled ? $"設定已停用（Scheduling:Tasks:{Key}:Enabled）" : Definition.Unavailable?.Invoke(services);
}

/// <summary>
/// 已註冊的排程任務與它們的設定。建立時檢查 Key 的格式與唯一性、每個任務都有完整設定、設定與 <c>ExecuteOnly</c> 只指向已註冊的任務，
/// 任何一項不符就列出全部問題讓啟動失敗（<see cref="SchedulingModule.VerifyStartup"/>），不會等到排程時才默默略過。
/// </summary>
[Singleton]
public sealed partial class ScheduledTaskCatalog
{
    private readonly Dictionary<string, ScheduledTaskPlan> byKey;

    public ScheduledTaskCatalog(IEnumerable<ScheduledTaskRegistration> registrations, IOptions<SchedulingOptions> options)
    {
        var scheduling = options.Value;
        var all = registrations.ToList();
        var problems = new List<string>();
        problems.AddRange(all.Where(x => !KeyFormat().IsMatch(x.Definition.Key) || x.Definition.Key.Length > ScheduledTask.KeyMaxLength)
            .Select(x => $"Key '{x.Definition.Key}' must be lower-case words separated by dots, at most {ScheduledTask.KeyMaxLength} characters."));
        problems.AddRange(all.GroupBy(x => x.Definition.Key, StringComparer.Ordinal).Where(g => g.Count() > 1)
            .Select(g => $"Key '{g.Key}' is registered more than once."));
        var keys = all.Select(x => x.Definition.Key).ToHashSet(StringComparer.Ordinal);
        problems.AddRange(scheduling.Tasks.Keys.Where(key => !keys.Contains(key)).Select(key => $"Scheduling:Tasks:{key} is not a registered task."));
        problems.AddRange(scheduling.ExecuteOnly.Where(key => !keys.Contains(key)).Select(key => $"Scheduling:ExecuteOnly names '{key}', which is not a registered task."));

        var plans = new List<ScheduledTaskPlan>();
        foreach (var registration in all)
        {
            var key = registration.Definition.Key;
            var settings = scheduling.Tasks.GetValueOrDefault(key);
            var fixedSchedule = registration.Definition.FixedSchedule;
            // 格式已由 SchedulingOptions 的驗證把關，這裡只檢查有沒有填、該不該填。
            var schedule = fixedSchedule ?? (settings?.Schedule is { } value ? Schedule.Parse(value) : null);
            if (settings is null) problems.Add($"Scheduling:Tasks:{key} is missing; list every task in appsettings.json.");
            else if (fixedSchedule is not null && settings.Schedule is not null) problems.Add($"Scheduling:Tasks:{key}:Schedule cannot be set; the task runs {fixedSchedule.Description}.");
            else if (schedule is null) problems.Add($"Scheduling:Tasks:{key}:Schedule is required.");
            if (settings is not null && settings.Timeout is null) problems.Add($"Scheduling:Tasks:{key}:Timeout is required.");
            if (settings is { Timeout: { } timeout } && schedule is not null)
                plans.Add(new(registration, schedule, timeout, settings.Enabled, scheduling.ExecutesTask(key)));
        }
        if (problems.Count > 0)
            throw new InvalidOperationException("Scheduled task configuration is invalid:\n" + string.Join('\n', problems));

        byKey = plans.ToDictionary(x => x.Key, StringComparer.Ordinal);
        All = [.. plans.OrderBy(x => x.Definition.Category, StringComparer.Ordinal).ThenBy(x => x.Definition.Name, StringComparer.Ordinal)];
    }

    /// <summary>依分類、名稱排序，管理頁直接沿用這個順序。</summary>
    public IReadOnlyList<ScheduledTaskPlan> All { get; }

    public ScheduledTaskPlan? Find(string key) => byKey.GetValueOrDefault(key);

    [GeneratedRegex("^[a-z0-9-]+(\\.[a-z0-9-]+)+$")]
    private static partial Regex KeyFormat();
}
