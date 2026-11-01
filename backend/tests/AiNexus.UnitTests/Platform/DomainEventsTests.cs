using AiNexus.Platform.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.UnitTests.Platform;

public sealed class DomainEventsTests
{
    private sealed record Happened(int Value) : IDomainEvent;

    private sealed class Recorder : IDomainEventHandler<Happened>
    {
        public List<int> Seen { get; } = [];

        public Task HandleAsync(Happened e, CancellationToken ct)
        {
            Seen.Add(e.Value);
            return Task.CompletedTask;
        }
    }

    // 派送依引發時的型別建立；處理器怎麼註冊（AddDomainEventHandler、工廠或實例）都要收得到。
    [Fact]
    public async Task HandlersRegisteredAnyWayReceiveTheEvent()
    {
        var recorder = new Recorder();
        using var provider = new ServiceCollection().AddScoped<IDomainEventHandler<Happened>>(_ => recorder).BuildServiceProvider();
        var events = new DomainEvents(provider);

        events.Raise(new Happened(1));
        await events.DispatchAsync(CancellationToken.None);

        Assert.Equal([1], recorder.Seen);
        Assert.False(events.HasPending);
    }

    // 以介面型別引發時找不到具體型別的處理器；要在引發時就失敗，不能悄悄略過。
    [Fact]
    public void RaisingThroughTheInterfaceIsRejected()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var events = new DomainEvents(provider);
        IDomainEvent e = new Happened(1);

        Assert.Throws<ArgumentException>(() => events.Raise(e));
        Assert.False(events.HasPending);
    }
}
