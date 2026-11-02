using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;
using Veiller.Api.Health;

namespace Veiller.Api.Data.Configurations;

public sealed class HeartRateRecordConfiguration : IEntityTypeConfiguration<HeartRateRecord>
{
    public void Configure(EntityTypeBuilder<HeartRateRecord> builder)
    {
        builder.ToTable("heart_rate_records");
        builder.HasKey(r => new { r.ElderId, r.HcRecordId });
        builder.Property(r => r.ElderId).HasMaxLength(128);
        builder.Property(r => r.HcRecordId).HasMaxLength(HealthLimits.MaxIdLength);
        builder.Property(r => r.Origin).HasMaxLength(HealthLimits.MaxOriginLength);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ElderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Samples).WithOne().HasForeignKey(s => new { s.ElderId, s.HcRecordId }).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class HeartRateSampleConfiguration : IEntityTypeConfiguration<HeartRateSample>
{
    public void Configure(EntityTypeBuilder<HeartRateSample> builder)
    {
        builder.ToTable("heart_rate_samples");
        builder.HasKey(s => new { s.ElderId, s.HcRecordId, s.Time });
        builder.Property(s => s.ElderId).HasMaxLength(128);
        builder.Property(s => s.HcRecordId).HasMaxLength(HealthLimits.MaxIdLength);
        // "Latest sample" and the caregiver history read by time range, whatever the record.
        builder.HasIndex(s => new { s.ElderId, s.Time });
    }
}
