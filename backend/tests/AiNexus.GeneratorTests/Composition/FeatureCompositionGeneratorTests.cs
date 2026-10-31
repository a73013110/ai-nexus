using AiNexus.GeneratorTests.Support;
using AiNexus.Generators.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AiNexus.GeneratorTests.Composition;

public sealed class FeatureCompositionGeneratorTests
{
    private const string Target = """
        using AiNexus.Platform.Modules;
        using Microsoft.AspNetCore.Routing;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;

        namespace Sample;

        [FeatureComposition]
        public static partial class Composition
        {
            private static partial void AddModules(IHostApplicationBuilder builder);
            private static partial void MapModules(RouteGroupBuilder api);
            private static partial void VerifyModules(System.IServiceProvider services);
            private static partial void AddEndpointHandlers(IServiceCollection services);
            private static partial void AddRequestValidators(IServiceCollection services);
        }
        """;

    private const string Modules = """
        using AiNexus.Platform.Modules;
        using Microsoft.AspNetCore.Routing;
        using Microsoft.Extensions.Hosting;

        namespace Sample.Work
        {
            public sealed class ZetaModule : IFeatureModule
            {
                public static void AddServices(IHostApplicationBuilder builder) { }
                public static void MapEndpoints(RouteGroupBuilder api) { }
                public static void VerifyStartup(System.IServiceProvider services) { }
            }
        }

        namespace Sample.Admin
        {
            public sealed class AlphaModule : IFeatureModule
            {
                public static void AddServices(IHostApplicationBuilder builder) { }
            }

            public abstract class SkippedModule : IFeatureModule
            {
                public static void AddServices(IHostApplicationBuilder builder) { }
            }
        }
        """;

    [Fact]
    public void ModulesAreAddedInNameOrderAndCalledOnlyForMembersTheyImplement()
    {
        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Target, Modules);

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.Errors);
        var alpha = run.Source.IndexOf("global::Sample.Admin.AlphaModule.AddServices(builder);", StringComparison.Ordinal);
        var zeta = run.Source.IndexOf("global::Sample.Work.ZetaModule.AddServices(builder);", StringComparison.Ordinal);
        Assert.InRange(alpha, 0, zeta - 1);
        Assert.Contains("global::Sample.Work.ZetaModule.MapEndpoints(", run.Source, StringComparison.Ordinal);
        Assert.Contains("MapGroup(api, \"\"), \"Zeta\"));", run.Source, StringComparison.Ordinal);
        Assert.Contains("global::Sample.Work.ZetaModule.VerifyStartup(services);", run.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("AlphaModule.MapEndpoints", run.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("AlphaModule.VerifyStartup", run.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("SkippedModule", run.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void SlicesAreScopedAndValidatorsAreSingletons()
    {
        const string slices = """
            using FluentValidation;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;

            namespace Sample;

            internal sealed class ListItems
            {
                public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api.MapGet("/items", () => "items");
            }

            internal static class Outer
            {
                internal sealed class SaveItem
                {
                    public static void MapCreate(RouteGroupBuilder api) { }
                }
            }

            internal static class Extensions
            {
                public static void MapHelpers(RouteGroupBuilder api) { }
            }

            internal sealed class NotASlice
            {
                public static void Map(IEndpointRouteBuilder api) { }
                public static void Configure(RouteGroupBuilder api) { }
            }

            internal sealed record ItemRequest(string Name);

            internal sealed class ItemRequestValidator : AbstractValidator<ItemRequest>;

            internal abstract class BaseValidator : AbstractValidator<ItemRequest>;

            internal sealed class GenericValidator<T> : AbstractValidator<T>;
            """;

        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Target, slices);

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.Errors);
        Assert.Contains("TryAddScoped<global::Sample.ListItems>(services);", run.Source, StringComparison.Ordinal);
        Assert.Contains("TryAddScoped<global::Sample.Outer.SaveItem>(services);", run.Source, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<global::FluentValidation.IValidator<global::Sample.ItemRequest>, global::Sample.ItemRequestValidator>(services);",
            run.Source, StringComparison.Ordinal);
        foreach (var excluded in new[] { "Sample.Extensions", "Sample.NotASlice", "Sample.BaseValidator", "Sample.GenericValidator" })
            Assert.DoesNotContain(excluded, run.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void TypesTheGeneratedCodeCannotSeeAreReportedInsteadOfRegistered()
    {
        const string hidden = """
            using Microsoft.AspNetCore.Routing;

            namespace Sample;

            internal sealed class Outer
            {
                private sealed class Hidden
                {
                    public static void Map(RouteGroupBuilder api) { }
                }
            }
            """;

        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Target, hidden);

        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("AINX1003", diagnostic.Id);
        Assert.Equal("Source1.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.Empty(run.Errors);
        Assert.DoesNotContain("Hidden", run.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyOneTopLevelStaticPartialClassCanBeTheTarget()
    {
        const string extra = """
            using AiNexus.Platform.Modules;

            namespace Sample;

            [FeatureComposition]
            public partial class Second;
            """;

        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Target, extra);

        Assert.Equal(["AINX1001", "AINX1002"], run.Diagnostics.Select(x => x.Id).Order(StringComparer.Ordinal));
        Assert.Contains("static partial class Composition", run.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutATargetNothingIsGenerated()
    {
        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Modules);

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.Source);
    }

    /// <summary>每次按鍵都會重跑 pipeline；改方法內容不影響任何註冊，各步驟與輸出都要沿用快取，IDE 才不會每次都重新產生。</summary>
    [Fact]
    public void EditsThatDoNotChangeRegistrationsReuseCachedResults()
    {
        var compilation = GeneratorHarness.Compile(Target, Modules);
        var driver = GeneratorHarness.Driver(new FeatureCompositionGenerator()).RunGenerators(compilation);
        var original = compilation.SyntaxTrees.Last();
        var edited = original.WithChangedText(SourceText.From(
            original.ToString().Replace("VerifyStartup(System.IServiceProvider services) { }", "VerifyStartup(System.IServiceProvider services) { _ = services; }", StringComparison.Ordinal)));

        var result = driver.RunGenerators(compilation.ReplaceSyntaxTree(original, edited)).GetRunResult().Results.Single();

        string[] steps = [FeatureCompositionGenerator.TargetsStep, FeatureCompositionGenerator.ModulesStep, FeatureCompositionGenerator.SlicesStep,
            FeatureCompositionGenerator.ValidatorsStep, FeatureCompositionGenerator.CompositionStep];
        var recomputed = steps.SelectMany(step => result.TrackedSteps.GetValueOrDefault(step, []).SelectMany(x => x.Outputs).Select(output => (step, output.Reason)))
            .Concat(result.TrackedOutputSteps.SelectMany(x => x.Value).SelectMany(x => x.Outputs).Select(output => (step: "Output", output.Reason)))
            .Where(x => x.Reason is not (IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged)).ToList();
        Assert.Empty(recomputed);
    }
}
