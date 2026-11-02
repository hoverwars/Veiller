using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data.Configurations;

public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(a => new { a.CircleId, a.OpenedAt });
        // One active episode per type and circle, whatever the number of concurrent evaluations.
        builder.HasIndex(a => new { a.CircleId, a.Type }).IsUnique().HasFilter("\"Active\" = TRUE");
        builder.HasOne<Circle>().WithMany().HasForeignKey(a => a.CircleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("alert_rules");
        builder.HasKey(r => new { r.CircleId, r.Type });
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Circle>().WithMany().HasForeignKey(r => r.CircleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(t => t.Token);
        builder.Property(t => t.Token).HasMaxLength(DeviceTokenLimits.MaxTokenLength);
        builder.Property(t => t.UserId).HasMaxLength(128);
        builder.Property(t => t.Platform).HasMaxLength(20);
        builder.HasIndex(t => t.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public static class DeviceTokenLimits
{
    public const int MaxTokenLength = 512;
}
