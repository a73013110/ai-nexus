using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Collaboration;

/// <summary>
/// 其他模組表達「不可刪除此容器」的理由。在刪除交易內、寫入任何資料之前詢問；由擁有阻擋紀錄的模組實作
/// （例如對話模組：仍在生成中的回答）。
/// </summary>
public interface IContainerDeletionCheck
{
    Task<Result> CheckAsync(string kind, Guid containerId, CancellationToken ct);
}
