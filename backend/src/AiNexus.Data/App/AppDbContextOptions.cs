using AiNexus.Data.App.Audit;
using AiNexus.Platform.Data;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Data.App;

/// <summary><see cref="AppDbContext"/> 的攔截器設定；正式註冊與測試替換連線時共用。</summary>
public static class AppDbContextOptions
{
    /// <summary>加入 SaveChanges 攔截器。</summary>
    /// <remarks>
    /// 順序不可調換：<see cref="DomainEventInterceptor"/> 先分派 domain event，handler 新增的業務異動與 <see cref="AuditEvent"/>
    /// 才會進入 ChangeTracker；<see cref="AuditEventInterceptor"/> 接著對所有新增的稽核列補追蹤資訊並遮罩。
    /// </remarks>
    public static DbContextOptionsBuilder AddAppInterceptors(this DbContextOptionsBuilder options, IServiceProvider services)
    {
        if (services.GetService<DomainEvents>() is { } events)
        {
            options.AddInterceptors(new DomainEventInterceptor(events));
        }

        return options.AddInterceptors(new AuditEventInterceptor(services.GetRequiredService<AuditContextSource>()));
    }
}
