using EDoc.Core.Database.Interfaces;
using EDoc.Core.Database.Markers;
using Microsoft.EntityFrameworkCore;

namespace EDoc.Core.Database.Implementations;

// Adapted from EDoc_HL's EfHelper.cs: the scoped context is supplied by DI instead
// of constructing EDoc's private context. The host owns disposal and transactions.
public sealed class EfHelper<TDb>(DbContext context) : IEfHelper<TDb> where TDb : IDbMarker
{
    public DbSet<TEntity> Set<TEntity>() where TEntity : class => context.Set<TEntity>();
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
