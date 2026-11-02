using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data;

public sealed class VeillerDbContext(DbContextOptions<VeillerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VeillerDbContext).Assembly);
}
