namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>One schema object whose definition differs between two SqlPackage per-object extracts.</summary>
/// <param name="Status">Added, removed or modified by the release.</param>
/// <param name="Path">The object's script in the extract, with forward slashes: <c>knowledge/Tables/Chunks.sql</c>.</param>
/// <param name="Type">SqlPackage's folder for the object kind: <c>Tables</c>, <c>Views</c>, <c>Security</c> (schemas), …</param>
/// <param name="Name">The object, <c>schema.Name</c> for schema-scoped objects.</param>
/// <param name="Diff">Unified diff of the object's script, from before to after.</param>
public sealed record SchemaObjectChange(SchemaObjectStatus Status, string Path, string Type, string Name, string Diff)
{
    /// <summary>The diff's file name: the path with its folders joined by dots, for example <c>knowledge.Tables.Chunks.diff</c>.</summary>
    public string FileName => System.IO.Path.ChangeExtension(Path, null).Replace('/', '.') + ".diff";
}
