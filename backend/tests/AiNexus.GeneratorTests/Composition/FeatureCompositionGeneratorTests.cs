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
            private static partial void AddDeclaredServices(IServiceCollection services);
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

            public sealed class BetaModule : IFeatureModule;

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
        Assert.DoesNotContain("BetaModule", run.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("SkippedModule", run.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void SlicesAreScopedAndRequestValidatorsAreSingletons()
    {
        const string slices = """
            using AiNexus.Platform.Modules;
            using AiNexus.Platform.Validation;
            using FluentValidation;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;

            namespace Sample;

            internal sealed class ListItems : IEndpointSlice
            {
                public static void MapEndpoints(RouteGroupBuilder group) => group.MapGet("/items", () => "items");
            }

            internal static class Outer
            {
                internal sealed class SaveItem : IEndpointSlice
                {
                    public static void MapEndpoints(RouteGroupBuilder group) { }
                }
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

            internal sealed record ItemRequest(string Name);

            internal sealed class ItemRequestValidator : RequestValidator<ItemRequest>;

            internal abstract class BaseValidator<T> : RequestValidator<T>;

            internal sealed record OtherRequest(string Name);

            internal sealed class DerivedValidator : BaseValidator<OtherRequest>;

            internal sealed class GenericValidator<T> : RequestValidator<T>;

            internal sealed class ChildValidator : AbstractValidator<ItemRequest>;
            """;

        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Target, slices);

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.Errors);
        Assert.Contains("TryAddScoped<global::Sample.ListItems>(services);", run.Source, StringComparison.Ordinal);
        Assert.Contains("TryAddScoped<global::Sample.Outer.SaveItem>(services);", run.Source, StringComparison.Ordinal);
        Assert.Contains("Singleton<global::AiNexus.Platform.Validation.IRequestValidator, global::Sample.ItemRequestValidator>());", run.Source, StringComparison.Ordinal);
        Assert.Contains("Singleton<global::AiNexus.Platform.Validation.IRequestValidator, global::Sample.DerivedValidator>());", run.Source, StringComparison.Ordinal);
        // 單純的 AbstractValidator<T> 是子驗證器，不套用到端點，也不註冊。
        foreach (var excluded in new[] { "Sample.AbstractSlice", "Sample.NotASlice", "Sample.BaseValidator", "Sample.GenericValidator", "Sample.ChildValidator" })
            Assert.DoesNotContain(excluded, run.Source, StringComparison.Ordinal);
    }

    /// <summary>契約與註冊方法都是範例自己宣告的：產生器只看介面上的 [RegisterImplementations]，不認得 Platform 的哪個介面。</summary>
    internal const string Contracts = """
        using AiNexus.Platform.Modules;
        using Microsoft.Extensions.DependencyInjection;

        namespace Sample;

        [RegisterImplementations(typeof(SourceRegistration), nameof(SourceRegistration.AddSource))]
        public interface ISource;

        [RegisterImplementations(typeof(SourceRegistration), nameof(SourceRegistration.AddHandler))]
        public interface IHandler<in TEvent>;

        public static class SourceRegistration
        {
            public static IServiceCollection AddSource<TSource>(this IServiceCollection services) where TSource : class, ISource => services;
            public static IServiceCollection AddHandler<TEvent, THandler>(this IServiceCollection services) where THandler : class, IHandler<TEvent> => services;
        }

        public interface IClock;
        """;

    [Fact]
    public void ServicesAreRegisteredByTheirContractsAndLifetimeMarkers()
    {
        const string services = """
            using System.Threading;
            using System.Threading.Tasks;
            using AiNexus.Platform.Modules;
            using Microsoft.Extensions.Hosting;

            namespace Sample;

            public sealed record Created;

            public sealed record Deleted;

            internal abstract class SourceBase : ISource;

            internal sealed class ItemSource : SourceBase;

            internal sealed class ItemHandler : IHandler<Created>, IHandler<Deleted>;

            internal sealed class Worker : BackgroundService
            {
                protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
            }

            [Scoped]
            internal sealed class Reader;

            [Singleton, Singleton<IClock>]
            internal sealed partial class Clock;

            internal sealed partial class Clock : IClock;

            [Scoped<IClock>]
            internal sealed class ScopedClock : IClock;

            internal sealed class Plain;
            """;

        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Target, Contracts, services);

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.Errors);
        string[] expected =
        [
            // 標了多個：類別本身註冊一次，介面轉接到它，共用同一個實例。
            "global::AiNexus.Platform.Modules.DeclaredServiceRegistration.AddDeclaredSingleton<global::Sample.Clock>(services);",
            "global::AiNexus.Platform.Modules.DeclaredServiceRegistration.AddForwardedSingleton<global::Sample.IClock, global::Sample.Clock>(services);",
            // 以不同型別引數實作同一個泛型契約兩次，各註冊一次，型別引數排在實作前面。
            "global::Sample.SourceRegistration.AddHandler<global::Sample.Created, global::Sample.ItemHandler>(services);",
            "global::Sample.SourceRegistration.AddHandler<global::Sample.Deleted, global::Sample.ItemHandler>(services);",
            // 契約由基底類別實作也算；抽象的基底類別本身不註冊。
            "global::Sample.SourceRegistration.AddSource<global::Sample.ItemSource>(services);",
            "global::AiNexus.Platform.Modules.DeclaredServiceRegistration.AddDeclaredScoped<global::Sample.Reader>(services);",
            // 只標一個介面：直接以介面註冊，類別本身不註冊。
            "global::AiNexus.Platform.Modules.DeclaredServiceRegistration.AddDeclaredScoped<global::Sample.IClock, global::Sample.ScopedClock>(services);",
            "global::Microsoft.Extensions.DependencyInjection.ServiceCollectionHostedServiceExtensions.AddHostedService<global::Sample.Worker>(services);",
        ];
        var positions = expected.Select(line => run.Source.IndexOf(line, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
        // partial 類別的每個宣告都會轉換一次，註冊只產生一次。
        Assert.Single(run.Source.Split('\n'), line => line.Trim() == expected[0]);
        foreach (var excluded in new[] { "Sample.SourceBase", "Sample.Plain", "AddDeclaredScoped<global::Sample.ScopedClock>" })
            Assert.DoesNotContain(excluded, run.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void LifetimeMarkersThatCannotBeRegisteredAreReported()
    {
        const string invalid = """
            using AiNexus.Platform.Modules;

            namespace Sample;

            [Scoped]
            internal abstract class Broken;

            [Singleton<IClock>]
            internal sealed class NotAClock;

            [Scoped, Singleton<IClock>]
            internal sealed class MixedClock : IClock;
            """;

        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Target, Contracts, invalid);

        Assert.Equal(["AINX1006", "AINX1006", "AINX1006"], run.Diagnostics.Select(x => x.Id));
        var messages = run.Diagnostics.Select(x => x.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Contains(messages, x => x.Contains("'Sample.NotAClock' 標了 [Singleton<IClock>]，但沒有實作 'Sample.IClock'", StringComparison.Ordinal));
        Assert.Contains(messages, x => x.Contains("'Sample.MixedClock' 標了 [Singleton<IClock>]，但也標了 [Scoped]", StringComparison.Ordinal));
        Assert.Empty(run.Errors);
        foreach (var excluded in new[] { "Broken", "NotAClock", "MixedClock" })
            Assert.DoesNotContain(excluded, run.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void TypesTheGeneratedCodeCannotSeeAreReportedInsteadOfRegistered()
    {
        const string hidden = """
            using AiNexus.Platform.Modules;
            using Microsoft.AspNetCore.Routing;

            namespace Sample;

            internal sealed class Outer
            {
                private sealed class Hidden : IEndpointSlice
                {
                    public static void MapEndpoints(RouteGroupBuilder group) { }
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
    public void TwoValidatorsForTheSameRequestAreReported()
    {
        const string validators = """
            using AiNexus.Platform.Validation;

            namespace Sample;

            internal sealed record ItemRequest(string Name);

            internal sealed class ItemRequestValidator : RequestValidator<ItemRequest>;

            internal sealed class ItemRulesValidator : RequestValidator<ItemRequest>;
            """;

        var run = GeneratorHarness.Run(new FeatureCompositionGenerator(), Target, validators);

        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("AINX1005", diagnostic.Id);
        Assert.Contains("Sample.ItemRulesValidator", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Equal(8, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
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
            FeatureCompositionGenerator.ValidatorsStep, FeatureCompositionGenerator.ServicesStep, FeatureCompositionGenerator.CompositionStep];
        var recomputed = steps.SelectMany(step => result.TrackedSteps.GetValueOrDefault(step, []).SelectMany(x => x.Outputs).Select(output => (step, output.Reason)))
            .Concat(result.TrackedOutputSteps.SelectMany(x => x.Value).SelectMany(x => x.Outputs).Select(output => (step: "Output", output.Reason)))
            .Where(x => x.Reason is not (IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged)).ToList();
        Assert.Empty(recomputed);
    }
}
