using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Integrations.Sources;

public sealed class GdwebSource(ISqlDatabase<GdwebDatabase> db) : AuthorizedSqlSource<GdwebDatabase>(db) { public override string Id => "gdweb"; }
