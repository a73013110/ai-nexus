using AiNexus.GeneratorTests.Support;
using AiNexus.Generators.Catalogs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AiNexus.GeneratorTests.Catalogs;

public sealed class FieldCatalogGeneratorTests
{
    private const string Target = """
        using System.Collections.Generic;
        using AiNexus.Platform.Modules;

        namespace Sample;

        public sealed record Action(string Name);

        public static partial class Actions
        {
            [FieldCatalog]
            private static partial Action[] All();

            [FieldCatalog]
            internal static partial IReadOnlyList<string> Names();
        }
        """;

    private const string Declarations = """
        namespace Sample.Work
        {
            public static class ZetaActions
            {
                public static readonly Sample.Action Second = new("zeta.b");
                public static readonly Sample.Action First = new("zeta.a");
                public static readonly string Name = "zeta";
                internal static readonly Sample.Action Internal = new("zeta.internal");
                public static Sample.Action Property { get; } = new("zeta.property");

                public static void Touch() { }
            }
        }

        namespace Sample.Admin
        {
            public static class AlphaActions
            {
                public static readonly Sample.Action Only = new("alpha");

                public static class Nested
                {
                    public static readonly Sample.Action Inner = new("alpha.nested");
                }
            }

            public sealed class NotStatic
            {
                public static readonly Sample.Action Skipped = new("skipped");
            }
        }
        """;

    [Fact]
    public void FieldsOfTheElementTypeAreListedByClassNameThenDeclarationOrder()
    {
        var run = GeneratorHarness.Run(new FieldCatalogGenerator(), Target, Declarations);

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.Errors);
        string[] expected = ["Sample.Admin.AlphaActions.Only,", "Sample.Admin.AlphaActions.Nested.Inner,", "Sample.Work.ZetaActions.Second,", "Sample.Work.ZetaActions.First,"];
        var positions = expected.Select(x => run.Source.IndexOf("global::" + x, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
        Assert.Contains("internal static partial global::System.Collections.Generic.IReadOnlyList<string> Names() =>", run.Source, StringComparison.Ordinal);
        Assert.Contains("global::Sample.Work.ZetaActions.Name,", run.Source, StringComparison.Ordinal);
        foreach (var excluded in new[] { "Internal", "Property", "Skipped" })
            Assert.DoesNotContain(excluded, run.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void FieldsTheGeneratedCodeCannotSeeAreReportedInsteadOfListed()
    {
        const string hidden = """
            namespace Sample;

            public static class Outer
            {
                private static class Hidden
                {
                    public static readonly Action Secret = new("secret");
                }
            }
            """;

        var run = GeneratorHarness.Run(new FieldCatalogGenerator(), Target, hidden);

        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal("AINX2002", diagnostic.Id);
        Assert.Equal("Source1.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.Empty(run.Errors);
        Assert.DoesNotContain("Secret", run.Source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private static partial Action[] All(int count);")]
    [InlineData("private static partial List<Action> All();")]
    [InlineData("private static partial Action[] All<T>();")]
    public void MethodsOfTheWrongShapeAreReported(string declaration)
    {
        var target = $$"""
            using System.Collections.Generic;
            using AiNexus.Platform.Modules;

            namespace Sample;

            public sealed record Action(string Name);

            public static partial class Actions
            {
                [FieldCatalog]
                {{declaration}}
            }
            """;

        var run = GeneratorHarness.Run(new FieldCatalogGenerator(), target);

        Assert.Equal("AINX2001", Assert.Single(run.Diagnostics).Id);
        Assert.Empty(run.Source);
    }

    /// <summary>每次按鍵都會重跑 pipeline；改方法內容不影響任何欄位，各步驟與輸出都要沿用快取。</summary>
    [Fact]
    public void EditsThatDoNotChangeFieldsReuseCachedResults()
    {
        var compilation = GeneratorHarness.Compile(Target, Declarations);
        var driver = GeneratorHarness.Driver(new FieldCatalogGenerator()).RunGenerators(compilation);
        var original = compilation.SyntaxTrees.Last();
        var edited = original.WithChangedText(SourceText.From(
            original.ToString().Replace("public static void Touch() { }", "public static void Touch() { _ = First; }", StringComparison.Ordinal)));

        var result = driver.RunGenerators(compilation.ReplaceSyntaxTree(original, edited)).GetRunResult().Results.Single();

        string[] steps = [FieldCatalogGenerator.TargetsStep, FieldCatalogGenerator.FieldsStep];
        var recomputed = steps.SelectMany(step => result.TrackedSteps.GetValueOrDefault(step, []).SelectMany(x => x.Outputs).Select(output => (step, output.Reason)))
            .Concat(result.TrackedOutputSteps.SelectMany(x => x.Value).SelectMany(x => x.Outputs).Select(output => (step: "Output", output.Reason)))
            .Where(x => x.Reason is not (IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged)).ToList();
        Assert.Empty(recomputed);
    }
}
