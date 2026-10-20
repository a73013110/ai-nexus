using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>郵件測試的範本與入列、寄送捷徑。</summary>
public static class MailFixtures
{
    public sealed class Greeting : MailTemplate<string>
    {
        public override string Category => "test.greeting";
        public override string Subject(string model) => $"你好 {model}";
        public override void Body(string model, MailBody body) => body.Heading($"你好 {model}").Paragraph("這是一封測試信。");
    }

    /// <summary>敏感信：內文加密保存、寄出後刪除。</summary>
    public sealed class Code : MailTemplate<string>
    {
        public override string Category => "test.code";
        public override MailPriority Priority => MailPriority.Security;
        public override bool Sensitive => true;
        public override string Subject(string model) => "驗證碼";
        public override void Body(string model, MailBody body) => body.Paragraph("你的驗證碼：").Code(model);
    }

    public static async Task<Guid> EnqueueAsync<T>(AppFactory factory, MailTemplate<T> template, T model, string to, MailEnqueueOptions? options = null)
    {
        using var scope = factory.Services.CreateScope();
        var enqueued = await scope.ServiceProvider.GetRequiredService<MailOutbox>().EnqueueAsync(template, model, new(to), options, CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        return enqueued.Value!.MessageId;
    }

    public static MailDispatcher Dispatcher(AppFactory factory) => ActivatorUtilities.CreateInstance<MailDispatcher>(factory.Services);

    public static async Task<OutboundMessage> MessageAsync(AppFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>().AsNoTracking().SingleAsync(x => x.Id == id);
    }

    public static async Task<List<DeliveryAttempt>> AttemptsAsync(AppFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<DeliveryAttempt>().AsNoTracking().Where(x => x.MessageId == id).OrderBy(x => x.Attempt).ToListAsync();
    }

    public static async Task<OutboundMessageContent?> ContentAsync(AppFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessageContent>().AsNoTracking().SingleOrDefaultAsync(x => x.MessageId == id);
    }
}
