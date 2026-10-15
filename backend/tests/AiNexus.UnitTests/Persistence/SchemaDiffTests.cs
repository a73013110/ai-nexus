using System.Text;
using AiNexus.Features.Persistence.ReleaseScripts;

namespace AiNexus.UnitTests.Persistence;

/// <summary>Per-object comparison of two SqlPackage <c>ExtractTarget=SchemaObjectType</c> folders.</summary>
public sealed class SchemaDiffTests : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("schema-diff-tests-");

    public void Dispose() => root.Delete(recursive: true);

    [Fact]
    public void BatchesAfterTheObjectAreSortedSoReorderedExtractsCompareEqual()
    {
        var first = Script("CREATE TABLE [s].[T] (\n    [Id] INT NOT NULL\n);", "EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'b'", "CREATE FULLTEXT INDEX ON [s].[T]", "CREATE NONCLUSTERED INDEX [IX_T] ON [s].[T]([Id] ASC);", "EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'a'");
        var second = Script("CREATE TABLE [s].[T] (\n    [Id] INT NOT NULL\n);", "EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'a'", "CREATE NONCLUSTERED INDEX [IX_T] ON [s].[T]([Id] ASC);", "EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'b'", "CREATE FULLTEXT INDEX ON [s].[T]");
        var normalized = SchemaDiff.Normalize(first);
        Assert.Equal(normalized, SchemaDiff.Normalize(second));
        var order = new[] { "CREATE TABLE", "CREATE NONCLUSTERED", "CREATE FULLTEXT", "@value = N'a'", "@value = N'b'" }.Select(x => normalized.IndexOf(x, StringComparison.Ordinal)).ToList();
        Assert.Equal(order.Order(), order);
    }

    [Fact]
    public void AlignmentIsCollapsedOutsideStringLiterals()
    {
        Assert.Equal("CREATE TABLE [s].[T] (\n    [Id] INT NOT NULL,\n    [Name] NVARCHAR (10) DEFAULT (N'a  b') NOT NULL\n);\n\n\nGO\n\n",
            SchemaDiff.Normalize("CREATE TABLE [s].[T] (\r\n    [Id]      INT            NOT NULL,\r\n    [Name]    NVARCHAR (10)  DEFAULT (N'a  b') NOT NULL\r\n);\r\n\r\n\r\nGO\r\n"));
    }

    [Fact]
    public void ChangesAreAddedRemovedOrModifiedObjectsOnly()
    {
        Write("before/s/Tables/Kept.sql", "CREATE TABLE [s].[Kept] ([Id] INT NOT NULL);");
        Write("after/s/Tables/Kept.sql", "CREATE TABLE [s].[Kept]   ([Id] INT NOT NULL);");
        Write("before/s/Tables/Changed.sql", "CREATE TABLE [s].[Changed] (\n    [Id] INT NOT NULL\n);");
        Write("after/s/Tables/Changed.sql", "CREATE TABLE [s].[Changed] (\n    [Id] INT NOT NULL,\n    [Name] NVARCHAR (10) NULL\n);");
        Write("before/s/Tables/Dropped.sql", "CREATE TABLE [s].[Dropped] ([Id] INT NOT NULL);");
        Write("after/Security/s2.sql", "CREATE SCHEMA [s2]\n    AUTHORIZATION [dbo];");

        var changes = SchemaDiff.Compare(Path.Combine(root.FullName, "before"), Path.Combine(root.FullName, "after"));

        Assert.Equal(["s/Tables/Changed.sql", "s/Tables/Dropped.sql", "Security/s2.sql"], changes.Select(x => x.Path));
        Assert.Equal([SchemaObjectStatus.Modified, SchemaObjectStatus.Removed, SchemaObjectStatus.Added], changes.Select(x => x.Status));
        Assert.Equal(["s.Changed", "s.Dropped", "s2"], changes.Select(x => x.Name));
        Assert.Equal(["Tables", "Tables", "Security"], changes.Select(x => x.Type));
        Assert.Equal("s.Tables.Changed.diff", changes[0].FileName);
        Assert.Contains("--- before/s/Tables/Changed.sql", changes[0].Diff, StringComparison.Ordinal);
        Assert.Contains("+    [Name] NVARCHAR (10) NULL", changes[0].Diff, StringComparison.Ordinal);
        Assert.StartsWith("--- /dev/null", changes[2].Diff, StringComparison.Ordinal);
        Assert.Contains("+++ /dev/null", changes[1].Diff, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyDatabaseExtractCountsEveryObjectAsAdded()
    {
        Write("after/s/Tables/T.sql", "CREATE TABLE [s].[T] ([Id] INT NOT NULL);");
        var change = Assert.Single(SchemaDiff.Compare(Path.Combine(root.FullName, "missing"), Path.Combine(root.FullName, "after")));
        Assert.Equal(SchemaObjectStatus.Added, change.Status);
    }

    [Fact]
    public void WrittenDiffListsEveryChangeInTheManifest()
    {
        var changes = new[]
        {
            new SchemaObjectChange(SchemaObjectStatus.Modified, "knowledge/Tables/EmbeddingProfiles.sql", "Tables", "knowledge.EmbeddingProfiles", "--- before\n+++ after\n"),
            new SchemaObjectChange(SchemaObjectStatus.Removed, "s/Tables/Dropped.sql", "Tables", "s.Dropped", "--- before\n+++ /dev/null\n"),
        };
        var diff = new SchemaDiff("20261014140318_NavigationGroups", "20261015031630_RemoveEmbeddingProfileChunkerConfiguration", ["20261015031630_RemoveEmbeddingProfileChunkerConfiguration"], "17.0", FullTextAvailable: false, changes);
        var output = Path.Combine(root.FullName, "diff");

        diff.WriteTo(output);

        Assert.Equal(["knowledge.Tables.EmbeddingProfiles.diff", "manifest.md", "s.Tables.Dropped.diff"], Directory.EnumerateFiles(output).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(Encoding.UTF8.Preamble.ToArray(), File.ReadAllBytes(Path.Combine(output, "s.Tables.Dropped.diff"))[..3]);
        var manifest = File.ReadAllText(Path.Combine(output, SchemaDiff.ManifestFileName));
        Assert.Contains("| 修改 | Tables | `knowledge.EmbeddingProfiles` | [`knowledge.Tables.EmbeddingProfiles.diff`](knowledge.Tables.EmbeddingProfiles.diff) |", manifest, StringComparison.Ordinal);
        Assert.Contains("| 刪除 | Tables | `s.Dropped` |", manifest, StringComparison.Ordinal);
        Assert.Contains("新增 0、刪除 1、修改 1 個物件", manifest, StringComparison.Ordinal);
        Assert.Contains("缺全文元件", manifest, StringComparison.Ordinal);
        Assert.Throws<ReleaseScriptException>(() => diff.WriteTo(output));
    }

    private static string Script(params string[] batches) => string.Concat(batches.Select(x => x + "\r\n\r\n\r\nGO\r\n\r\n"));

    private void Write(string path, string text)
    {
        var file = Path.Combine(root.FullName, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
