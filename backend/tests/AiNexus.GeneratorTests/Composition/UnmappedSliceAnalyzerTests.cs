using AiNexus.GeneratorTests.Support;
using AiNexus.Generators.Composition;

namespace AiNexus.GeneratorTests.Composition;

public sealed class UnmappedSliceAnalyzerTests
{
    private const string Slices = """
        using AiNexus.Platform.Modules;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Routing;

        namespace Sample;

        internal sealed class CreateItem : IEndpointSlice
        {
            public static void MapEndpoints(RouteGroupBuilder group) { }
        }

        internal sealed class UpdateItem : IEndpointSlice
        {
            public static void MapEndpoints(RouteGroupBuilder group) { }
        }

        internal sealed class ListItems : IEndpointSlice
        {
            public static void MapEndpoints(RouteGroupBuilder group) { }
        }

        public sealed class ItemsModule : IFeatureModule
        {
            public static void MapEndpoints(RouteGroupBuilder api)
            {
                api.MapGroup("/items").MapSlice<CreateItem>();
                EndpointSlices.MapSlice<ListItems>(api);
                // 直接呼叫切片的 MapEndpoints 不算掛載：只有 MapSlice 一種寫法。
                UpdateItem.MapEndpoints(api);
            }
        }
        """;

    [Fact]
    public async Task SlicesNobodyMountsAreReportedWhereTheyAreDeclared()
    {
        var diagnostics = await GeneratorHarness.AnalyzeAsync(new UnmappedSliceAnalyzer(), Slices);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AINX1004", diagnostic.Id);
        Assert.Contains("Sample.UpdateItem", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Equal("UpdateItem", diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));
    }

    /// <summary>規則與產生器註冊切片的規則相同：模組本身、抽象類別與只是方法形狀相同的類別都不是切片。</summary>
    [Fact]
    public async Task TypesThatAreNotSlicesAreIgnored()
    {
        const string others = """
            using AiNexus.Platform.Modules;
            using Microsoft.AspNetCore.Routing;

            namespace Sample;

            public sealed class EmptyModule : IFeatureModule
            {
                public static void MapEndpoints(RouteGroupBuilder api) { }
            }

            internal abstract class AbstractSlice : IEndpointSlice
            {
                public static void MapEndpoints(RouteGroupBuilder group) { }
            }

            internal sealed class NotASlice
            {
                public static void Map(RouteGroupBuilder api) { }
                public static void MapEndpoints(RouteGroupBuilder api) { }
            }
            """;

        Assert.Empty(await GeneratorHarness.AnalyzeAsync(new UnmappedSliceAnalyzer(), others));
    }
}
