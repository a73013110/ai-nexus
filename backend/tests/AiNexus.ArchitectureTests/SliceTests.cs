using AiNexus.Platform.Modules;

namespace AiNexus.ArchitectureTests;

/// <summary>一個 use case 只有一種形狀：endpoint lambda 直接接收 slice 本身，由 <see cref="EndpointHandlers"/> 註冊。</summary>
public sealed class SliceTests
{
    [Fact]
    public void EverySliceIsAnInternalSealedClass()
    {
        var slices = EndpointHandlers.Slices(Assemblies.Features).ToList();
        Assert.NotEmpty(slices);
        var other = slices.Where(t => t.IsPublic || t.IsNestedPublic || !t.IsSealed || t.IsAbstract).Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(other.Count == 0, "Slices must be internal sealed (non-static) classes: " + string.Join(", ", other));
    }
}
