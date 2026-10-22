using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// 匯入前的預覽（<c>identity.users.import</c>）：依 SID 重新讀目錄（不信任前端帶來的姓名與信箱），逐人回報會新建、綁定、更新哪些欄位或為何略過。
/// 只讀、不寫稽核；確認時 <see cref="ImportDirectoryUsers"/> 以同一套規則重算，所以預覽與結果一致（預覽後資料被改的人回 <c>directory_changed</c>）。
/// </summary>
internal sealed class PreviewDirectoryImport(IAdDirectory directory, DirectoryProvisioning provisioning)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/directory-imports/preview", (DirectoryImportRequest body, PreviewDirectoryImport handler, CancellationToken ct) => handler.HandleAsync(body, ct).ToHttpResultAsync())
        .RequireRateLimiting(IdentityModule.DirectoryWriteRateLimit)
        .WithName("PreviewDirectoryImport");

    public async Task<Result<DirectoryOutcomesDto>> HandleAsync(DirectoryImportRequest request, CancellationToken ct)
    {
        var people = (await directory.FindAsync(request.Sids, ct)).ToDictionary(x => x.Sid, StringComparer.OrdinalIgnoreCase);
        var items = new List<DirectoryOutcomeDto>();
        foreach (var sid in request.Sids)
            items.Add(people.TryGetValue(sid, out var person) ? await provisioning.PreviewAsync(person, request.IncludeDisabled, ct) : DirectoryProvisioning.Missing(sid));
        return new DirectoryOutcomesDto(items);
    }
}
