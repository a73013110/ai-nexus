using AiNexus.Features.Attachments;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>
/// 本模組提供給其他模組（專案、儲存庫匯入）與匯入／嵌入工作使用的文件契約；模組自己的端點是本檔旁邊的各個 slice。
/// </summary>
public sealed class DocumentService
{
    private readonly DocumentAccess documents;
    private readonly AddKnowledgeDocument add;

    internal DocumentService(DocumentAccess documents, AddKnowledgeDocument add) => (this.documents, this.add) = (documents, add);

    /// <summary>把附件加入知識庫或專案；兩者皆為 null 時成為獨立文件。詳見 <see cref="AddKnowledgeDocument"/>。</summary>
    public Task<Result<DocumentDto>> AddAsync(Guid actor, Guid? collection, Guid attachment, CancellationToken ct, Guid? project = null, TextDocumentRequest? text = null)
        => add.HandleAsync(actor, collection, attachment, ct, project, text);

    /// <summary>以與文件端點相同的權限檢查取得追蹤中的文件。</summary>
    public Task<Result<KnowledgeDocument>> RequireAsync(Guid actor, Guid id, CancellationToken ct, bool write = false) => documents.FindAsync(actor, id, ct, write);

    public Task<Result<DocumentDto>> DetailAsync(Guid actor, Guid id, CancellationToken ct) => documents.DetailAsync(actor, id, ct);

    /// <summary><see cref="DetailAsync"/> 的批次版本，回傳順序與輸入 ids 相同，查詢次數固定。</summary>
    public Task<Result<IReadOnlyList<DocumentDto>>> DetailsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct) => documents.DetailsAsync(actor, ids, ct);

    public Task<Result<Attachment>> OriginalAsync(Guid actor, Guid id, CancellationToken ct) => documents.OriginalAsync(actor, id, ct);
}
