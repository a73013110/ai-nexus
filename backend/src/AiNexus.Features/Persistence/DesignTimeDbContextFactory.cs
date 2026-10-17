using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiNexus.Features.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NexusDbContext>
{
    public NexusDbContext CreateDbContext(string[] args)
    {
        // 產生 migration 與模型時不會真的連線，這裡只需要一組合法的連線字串，不需要真實帳密。
        var options = new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer("Server=localhost;Database=AiNexus;Integrated Security=True;Encrypt=True").Options;
        return new NexusDbContext(options);
    }
}
