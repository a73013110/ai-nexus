using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Integrations;

public sealed class GdwebSource(ISqlDatabase<LegacyGdwebDatabase> db) : AuthorizedSqlSource<LegacyGdwebDatabase>(db) { public override string Id => "gdweb"; }
