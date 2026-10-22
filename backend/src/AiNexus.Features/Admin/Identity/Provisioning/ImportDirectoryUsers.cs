using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>
/// 管理員從 AD 匯入選取的人（<c>identity.users.import</c>）：新建的帳號與首次登入相同（member 角色、只開 AD 登入、姓名與信箱來自目錄），
/// 已在平台的人以目錄資料更新，預建的同名帳號綁定 SID。AD 已停用的人除非 <see cref="DirectoryImportRequest.IncludeDisabled"/> 否則不新建。
/// 每人一筆 <c>admin.user_import</c> 稽核；目錄連不上時整批不寫入（503）。
/// </summary>
internal sealed class ImportDirectoryUsers(IAdDirectory directory, DirectoryProvisioning provisioning)
{
    public const string Action = "admin.user_import";

    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/directory-imports", (DirectoryImportRequest body, ICurrentUser user, ImportDirectoryUsers handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(IdentityModule.DirectoryWriteRateLimit)
        .WithName("ImportDirectoryUsers");

    public async Task<Result<DirectoryOutcomesDto>> HandleAsync(Guid actor, DirectoryImportRequest request, CancellationToken ct)
    {
        // 目錄在鎖與交易之外先讀完：外部呼叫不佔住身分鎖，也不會讓交易等在網路上。
        var people = (await directory.FindAsync(request.Sids, ct)).ToDictionary(x => x.Sid, StringComparer.OrdinalIgnoreCase);
        var items = new List<DirectoryOutcomeDto>();
        foreach (var sid in request.Sids)
        {
            items.Add(people.TryGetValue(sid, out var person)
                ? await provisioning.ApplyAsync(actor, FeatureCatalog.IdentityUsersImport, Action, person, request.IncludeDisabled, allowCreate: true, ct)
                : DirectoryProvisioning.Missing(sid));
        }
        provisioning.Completed("import", items);
        return new DirectoryOutcomesDto(items);
    }
}
