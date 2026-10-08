using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiNexus.Features.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NexusDbContext>
{
    public NexusDbContext CreateDbContext(string[] args)
    {
        // Schema generation needs no real SQL credentials or connection.
        var options = new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer("Server=localhost;Database=AiNexus;Integrated Security=True;Encrypt=True").Options;
        return new NexusDbContext(options);
    }
}
