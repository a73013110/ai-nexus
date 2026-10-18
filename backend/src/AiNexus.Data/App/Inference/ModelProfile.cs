using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Inference;

/// <summary>核准模型的規格快照；由設定檔的模型清單在啟動時同步寫入。</summary>
public sealed class ModelProfile
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "google";
    public string ProviderModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int ContextTokens { get; set; } = 8192;
    public int MaxOutputTokens { get; set; } = 2048;
    public bool SupportsStreaming { get; set; } = true;
    public bool SupportsUsage { get; set; } = true;
}

internal sealed class ModelProfileConfiguration : IEntityTypeConfiguration<ModelProfile>
{
    public void Configure(EntityTypeBuilder<ModelProfile> profile)
    {
        profile.ToTable("ModelProfiles", "inference");
        profile.HasKey(x => x.Id);
        profile.Property(x => x.Id).HasMaxLength(160);
        profile.Property(x => x.DisplayName).HasMaxLength(120);
        profile.Property(x => x.Provider).HasMaxLength(32);
        profile.Property(x => x.ProviderModelId).HasMaxLength(150);
    }
}
