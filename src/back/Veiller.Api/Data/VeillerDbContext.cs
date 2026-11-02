using Microsoft.EntityFrameworkCore;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data;

public sealed class VeillerDbContext(DbContextOptions<VeillerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Circle> Circles => Set<Circle>();

    public DbSet<CircleMember> CircleMembers => Set<CircleMember>();

    public DbSet<Consent> Consents => Set<Consent>();

    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();

    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VeillerDbContext).Assembly);
}
