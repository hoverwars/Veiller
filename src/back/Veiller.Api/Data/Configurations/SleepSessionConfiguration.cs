using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;
using Veiller.Api.Health;

namespace Veiller.Api.Data.Configurations;

public sealed class SleepSessionConfiguration : IEntityTypeConfiguration<SleepSession>
{
    public void Configure(EntityTypeBuilder<SleepSession> builder)
    {
        builder.ToTable("sleep_sessions");
        builder.HasKey(s => new { s.ElderId, s.HcRecordId });
        builder.Property(s => s.ElderId).HasMaxLength(128);
        builder.Property(s => s.HcRecordId).HasMaxLength(HealthLimits.MaxIdLength);
        builder.Property(s => s.Origin).HasMaxLength(HealthLimits.MaxOriginLength);
        builder.Property(s => s.ZoneOffset).HasMaxLength(6);
        builder.HasIndex(s => new { s.ElderId, s.End });
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.ElderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Stages).WithOne().HasForeignKey(s => new { s.ElderId, s.HcRecordId }).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SleepStageConfiguration : IEntityTypeConfiguration<SleepStage>
{
    public void Configure(EntityTypeBuilder<SleepStage> builder)
    {
        builder.ToTable("sleep_stages");
        builder.HasKey(s => new { s.ElderId, s.HcRecordId, s.Ordinal });
        builder.Property(s => s.ElderId).HasMaxLength(128);
        builder.Property(s => s.HcRecordId).HasMaxLength(HealthLimits.MaxIdLength);
        builder.Property(s => s.Stage).HasMaxLength(20);
    }
}
