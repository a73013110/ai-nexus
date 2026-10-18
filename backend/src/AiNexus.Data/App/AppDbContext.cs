using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Inference;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Data.App;

/// <summary>主資料庫的 DbContext；結構以 <c>database/App</c> 為準，這裡只做對應。</summary>
/// <remarks>
/// 實體組態由 <c>ApplyConfigurationsFromAssembly</c> 自動載入，一張表一個檔案（<c>App/&lt;Module&gt;/&lt;Entity&gt;.cs</c>）。
/// 所有時間戳都必須經由 <see cref="Clock"/> 取得，測試才能控制時間。
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider? clock = null) : DbContext(options)
{
    /// <summary>設定檔 <c>Databases</c> 節點中的連線名稱。</summary>
    public const string DatabaseName = "App";

    public TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<GenerationRun> Runs => Set<GenerationRun>();
    public DbSet<RunEvent> RunEvents => Set<RunEvent>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
