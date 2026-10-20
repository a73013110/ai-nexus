namespace AiNexus.Data.App.Identity;

/// <summary><c>identity.VerificationChallenges.Purpose</c> 的值（資料庫 CHECK 約束同一組）。</summary>
public static class VerificationPurposes
{
    public const string EmailChange = "email_change";
    public const string Reauthentication = "reauthentication";
}
