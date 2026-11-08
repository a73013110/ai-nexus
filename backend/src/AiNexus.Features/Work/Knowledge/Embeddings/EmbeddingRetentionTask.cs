using AiNexus.Platform.Scheduling;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

/// <summary>清除保留期（<c>Knowledge:Embedding:RetiredRetentionDays</c>）已過的退役向量。</summary>
internal sealed class EmbeddingRetentionTask(EmbeddingLifecycle lifecycle) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "knowledge.embedding-retention", "退役向量清理", "知識庫", "刪除換模型後超過保存期的舊向量。", Schedule.Every(TimeSpan.FromHours(1)));

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await lifecycle.CleanupExpiredAsync(ct), total => $"刪除 {total:N0} 筆向量");
}
