using AiNexus.Platform.Modules;

namespace AiNexus.ArchitectureTests;

/// <summary>One use case, one shape: the endpoint lambda receives the slice itself, which <see cref="EndpointHandlers"/> registers.</summary>
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
