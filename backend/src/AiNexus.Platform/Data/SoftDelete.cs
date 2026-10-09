namespace AiNexus.Platform.Data;

/// <summary>
/// The named EF Core query filter that hides soft-deleted rows. Entities declare it in their configuration with
/// <c>HasQueryFilter(SoftDelete.Filter, x =&gt; !x.IsDeleted)</c>; a query that must still see deleted rows (admin
/// reads, cleanup, background jobs finishing work on a row deleted meanwhile) opts out by name with
/// <c>IgnoreQueryFilters([SoftDelete.Filter])</c>, so any other filter stays on.
/// </summary>
public static class SoftDelete
{
    public const string Filter = "SoftDelete";
}
