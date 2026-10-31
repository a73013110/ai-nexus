using AiNexus.GeneratorTests.Support;
using AiNexus.Generators.Composition;

namespace AiNexus.GeneratorTests.Composition;

public sealed class UnmappedSliceAnalyzerTests
{
    private const string Slices = """
        using AiNexus.Platform.Modules;
        using Microsoft.AspNetCore.Routing;
        using Microsoft.Extensions.Hosting;

        namespace Sample;

        internal sealed class SaveItem
        {
            public static void MapCreate(RouteGroupBuilder routes) { }
            public static void MapUpdate(RouteGroupBuilder routes) { }
        }

        internal sealed class ListItems
        {
            public static void Map(RouteGroupBuilder routes) { }
        }

        public sealed class ItemsModule : IFeatureModule
        {
            public static void AddServices(IHostApplicationBuilder builder) { }

            public static void MapEndpoints(RouteGroupBuilder api)
            {
                SaveItem.MapCreate(api);
                System.Action<RouteGroupBuilder> map = ListItems.Map;
                map(api);
            }
        }
        """;

    [Fact]
    public async Task MapMethodsNobodyCallsAreReportedWhereTheyAreDeclared()
    {
        var diagnostics = await GeneratorHarness.AnalyzeAsync(new UnmappedSliceAnalyzer(), Slices);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AINX1004", diagnostic.Id);
        Assert.Contains("Sample.SaveItem.MapUpdate", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Equal("MapUpdate", diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));
    }

    /// <summary>規則與產生器註冊切片的規則相同：模組本身、static 類別的擴充方法與不收 RouteGroupBuilder 的方法都不是切片。</summary>
    [Fact]
    public async Task MethodsThatAreNotSliceMapsAreIgnored()
    {
        const string others = """
            using AiNexus.Platform.Modules;
            using Microsoft.AspNetCore.Routing;
            using Microsoft.Extensions.Hosting;

            namespace Sample;

            public sealed class EmptyModule : IFeatureModule
            {
                public static void AddServices(IHostApplicationBuilder builder) { }
                public static void MapEndpoints(RouteGroupBuilder api) { }
            }

            internal static class Extensions
            {
                public static void MapHelpers(this RouteGroupBuilder api) { }
            }

            internal sealed class NotASlice
            {
                public static void Map(IEndpointRouteBuilder api) { }
                public void MapInstance(RouteGroupBuilder api) { }
            }
            """;

        Assert.Empty(await GeneratorHarness.AnalyzeAsync(new UnmappedSliceAnalyzer(), others));
    }
}
