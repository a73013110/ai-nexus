namespace AiNexus.Data.App.Identity;

/// <summary><c>identity.Users.EmailSource</c> 的值（資料庫 CHECK 約束同一組）。</summary>
public static class EmailSources
{
    /// <summary>RFC 5321 的地址上限，也是 <c>Users.Email</c> 的欄位長度。</summary>
    public const int AddressMaxLength = 254;

    /// <summary>AD 的 <c>mail</c> 屬性；目錄可信，寫入即視為已驗證，本人不能修改。</summary>
    public const string Directory = "directory";

    /// <summary>本人以驗證碼驗證過的地址。</summary>
    public const string User = "user";

    /// <summary>管理者設定；本人驗證前 <c>EmailVerifiedAt</c> 為空。</summary>
    public const string Admin = "admin";
}
