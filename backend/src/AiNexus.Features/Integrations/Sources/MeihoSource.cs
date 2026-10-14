using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Integrations.Sources;

public sealed class MeihoSource(ISqlDatabase<LegacyMeihoDatabase> db) : AuthorizedSqlSource<LegacyMeihoDatabase>(db) { public override string Id => "meiho"; }
