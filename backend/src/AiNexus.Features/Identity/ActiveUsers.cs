using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;

namespace AiNexus.Features.Identity;

internal sealed class ActiveUsers(NexusDbContext db) : IActiveUsers
{
    public IQueryable<Guid> Ids => db.Users.Where(x => x.Enabled && x.DeletedAt == null).Select(x => x.Id);
}
