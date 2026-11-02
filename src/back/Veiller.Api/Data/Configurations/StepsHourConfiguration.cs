using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data.Configurations;

public sealed class StepsHourConfiguration : IEntityTypeConfiguration<StepsHour>
{
    public void Configure(EntityTypeBuilder<StepsHour> builder)
    {
        builder.ToTable("steps_hourly");
        builder.HasKey(s => new { s.ElderId, s.Start });
        builder.Property(s => s.ElderId).HasMaxLength(128);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.ElderId).OnDelete(DeleteBehavior.Cascade);
    }
}
