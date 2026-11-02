using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data.Configurations;

public sealed class HealthSyncStatusConfiguration : IEntityTypeConfiguration<HealthSyncStatus>
{
    public void Configure(EntityTypeBuilder<HealthSyncStatus> builder)
    {
        builder.ToTable("health_sync_status");
        builder.HasKey(s => s.ElderId);
        builder.Property(s => s.ElderId).HasMaxLength(128);
        builder.HasOne<User>().WithOne().HasForeignKey<HealthSyncStatus>(s => s.ElderId).OnDelete(DeleteBehavior.Cascade);
    }
}
