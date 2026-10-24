namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>使用者的登入方式；<paramref name="DirectoryBound"/> 表示已以 SID 綁定 AD（否則是預建或解除綁定後、等下次驗證或匯入才綁定）。</summary>
public sealed record UserAuthenticationDto(bool AdEnabled, bool LocalEnabled, string? AdAccount, string? LocalAccount, bool HasLocalPassword, bool DirectoryBound);
