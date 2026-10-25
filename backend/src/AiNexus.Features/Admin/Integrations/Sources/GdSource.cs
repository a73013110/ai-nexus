using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Admin.Integrations.Sources;

public sealed class GdSource(ISqlDatabase<GdDatabase> db) : AuthorizedSqlSource<GdDatabase>(db) { public override string Id => "gd"; }
