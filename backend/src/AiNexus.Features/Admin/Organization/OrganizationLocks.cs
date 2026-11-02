using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.Features.Admin.Organization;

/// <summary>組織模組的行政寫入鎖（<see cref="AuditTarget.Locks"/>）。</summary>
internal static class OrganizationLocks
{
    /// <summary>單位樹的結構規則（成環、深度、同層重名、首長單位）跨整棵樹；首長單位會改上層成員的主管勾選，所以成員的新增與修改也排在這裡。</summary>
    public const string Tree = "organization:tree";
}
