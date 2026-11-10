namespace AiNexus.Platform.Scheduling;

/// <summary>常駐服務在這個程序的狀態；<see cref="Reason"/> 是未啟用的原因或中止時的例外型別。</summary>
public sealed record ResidentServiceStatus(ResidentServiceDefinition Definition, string State, string? Reason);

public static class ResidentServiceStates
{
    public const string Running = "running";

    /// <summary>依設定不做事（<see cref="ResidentServiceDefinition.Inactive"/>）。</summary>
    public const string Inactive = "inactive";

    /// <summary>已結束卻沒有宣告原因：啟動條件不符（例如資料庫連不上）或正在停站。</summary>
    public const string Stopped = "stopped";

    /// <summary>執行迴圈因未處理的例外中止。</summary>
    public const string Failed = "failed";
}

/// <summary>
/// 已註冊的常駐服務與它們在這個程序的狀態。狀態只在記憶體，各程序各有一份：管理頁看到的是回應請求的那個站台。
/// Key 重複時建立失敗（由排程模組在啟動時檢查）。
/// </summary>
public sealed class ResidentServices
{
    private readonly IServiceProvider services;
    private readonly IReadOnlyList<ResidentServiceRegistration> registrations;
    private Dictionary<Type, BackgroundService>? instances;

    public ResidentServices(IEnumerable<ResidentServiceRegistration> registrations, IServiceProvider services)
    {
        this.services = services;
        this.registrations = [.. registrations.OrderBy(x => x.Definition.Category, StringComparer.Ordinal).ThenBy(x => x.Definition.Name, StringComparer.Ordinal)];
        var duplicates = this.registrations.GroupBy(x => x.Definition.Key, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException("Resident service keys are registered more than once: " + string.Join(", ", duplicates));
    }

    /// <summary>每個服務此刻的狀態，依分類、名稱排序，管理頁直接沿用這個順序。</summary>
    public IReadOnlyList<ResidentServiceStatus> Read()
    {
        // 第一次讀取才向容器要：建構時就要會在主機啟動前把所有 hosted service 建出來。之後主機啟動的就是同一批 singleton。
        instances ??= services.GetServices<IHostedService>().OfType<BackgroundService>().DistinctBy(x => x.GetType()).ToDictionary(x => x.GetType());
        return [.. registrations.Select(x => Status(x.Definition, instances.GetValueOrDefault(x.Type)?.ExecuteTask))];
    }

    private ResidentServiceStatus Status(ResidentServiceDefinition definition, Task? execution)
    {
        if (execution is { IsFaulted: true })
            return new(definition, ResidentServiceStates.Failed, execution.Exception.GetBaseException().GetType().Name);
        if (definition.Inactive?.Invoke(services) is { } reason) return new(definition, ResidentServiceStates.Inactive, reason);
        return new(definition, execution is { IsCompleted: false } ? ResidentServiceStates.Running : ResidentServiceStates.Stopped, null);
    }
}
