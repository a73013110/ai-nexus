using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

/// <summary>
/// <see cref="NexusDbContext"/> 專用選項與攔截器管線設定擴充。
/// </summary>
public static class NexusDbContextOptions
{
    /// <summary>
    /// 為 <see cref="NexusDbContext"/> 注入核心生命週期攔截器管線。
    /// </summary>
    /// <remarks>
    /// 攔截器順序性保證（極為關鍵）：
    /// 1. <see cref="DomainEventInterceptor"/> 必須先執行：在保存事務開始前分發領域事件，事件處理常式（Handlers）中
    ///    若有產生新的業務異動或追加 <see cref="AuditEvent"/>，ChangeTracker 能即時捕捉。
    /// 2. <see cref="AuditEventInterceptor"/> 接續執行：在所有業務實體（包括上述領域事件引發的異動）準備就緒後，
    ///    統一對所有新增的審計日誌進行欄位補全（TraceId、ActorId）與高敏資料脫敏。
    /// </remarks>
    /// <param name="options">DbContext 選項建置器。</param>
    /// <param name="services">解析攔截器依賴之 DI 服務範圍。</param>
    public static DbContextOptionsBuilder AddNexusInterceptors(this DbContextOptionsBuilder options, IServiceProvider services)
    {
        if (services.GetService<DomainEvents>() is { } events)
        {
            options.AddInterceptors(new DomainEventInterceptor(events));
        }

        return options.AddInterceptors(new AuditEventInterceptor(services.GetService<IHttpContextAccessor>()));
    }
}
