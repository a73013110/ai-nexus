using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Identity;

/// <summary>
/// 可呼叫 AiNexus API 的外部系統（例如公文系統以它驗證 AD 帳密）。一個系統一把金鑰，只存 SHA-256；
/// 最近使用時間不存在這裡，由稽核推得：每次呼叫都寫稽核，再多寫這一列只會讓管理員的修改一直撞版本。
/// </summary>
public sealed class ApiClient : IRowVersioned
{
    public const int NameMaxLength = 100;
    public const int KeyHintLength = 4;
    public const int ContactMaxLength = 100;

    public Guid Id { get; set; } = Guid.NewGuid();
    public long RowVersion { get; set; }
    public string Name { get; set; } = "";
    public byte[] KeyHash { get; set; } = [];
    /// <summary>金鑰的末四碼，讓管理員對照對方系統設定的是哪一把。</summary>
    public string KeyHint { get; set; } = "";
    /// <summary>目前這把金鑰的產生時間；重新產生時更新，舊金鑰同時失效。</summary>
    public DateTimeOffset KeyIssuedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>對方系統的聯絡人（姓名、分機等），重新產生金鑰或停用前要通知的人。</summary>
    public string? Contact { get; set; }
    /// <summary>停用時金鑰不被接受，但金鑰本身不變：重新啟用後對方不必改設定。</summary>
    public bool Enabled { get; set; } = true;
}

internal sealed class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
{
    public void Configure(EntityTypeBuilder<ApiClient> client)
    {
        client.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        client.ToTable("ApiClients", "identity");
        client.HasKey(x => x.Id);
        client.Property(x => x.RowVersion).IsRowVersion().HasConversion<byte[]>();
        client.Property(x => x.Name).HasMaxLength(ApiClient.NameMaxLength);
        client.Property(x => x.KeyHash).HasMaxLength(32).IsFixedLength();
        client.Property(x => x.KeyHint).HasMaxLength(ApiClient.KeyHintLength);
        client.Property(x => x.Contact).HasMaxLength(ApiClient.ContactMaxLength);
    }
}
