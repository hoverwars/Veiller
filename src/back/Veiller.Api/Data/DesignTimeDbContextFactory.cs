using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Veiller.Api.Data;

/// <summary>Lets `dotnet ef` build the context without running Program (and without Firebase credentials).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VeillerDbContext>
{
    public VeillerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<VeillerDbContext>()
            .UseNpgsql("Host=localhost;Database=veiller")
            .Options);
}
