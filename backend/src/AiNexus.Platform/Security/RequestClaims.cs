namespace AiNexus.Platform.Security;

/// <summary>基礎設施也要讀的工作階段 claim 名稱；由身分模組寫入。</summary>
public static class RequestClaims
{
    /// <summary>身分測試時實際操作的管理者識別碼；稽核列以它補 <c>ActorId</c>。</summary>
    public const string ActorId = "actor_id";
}
