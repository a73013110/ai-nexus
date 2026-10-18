using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Admin.Integrations.Sources;

public sealed class MeihoSource(ISqlDatabase<MeihoDatabase> db) : AuthorizedSqlSource<MeihoDatabase>(db) { public override string Id => "meiho"; }
