using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data.Configurations;

public sealed class CircleMemberConfiguration : IEntityTypeConfiguration<CircleMember>
{
    public void Configure(EntityTypeBuilder<CircleMember> builder)
    {
        builder.ToTable("circle_members");
        builder.HasKey(m => new { m.CircleId, m.UserId });
        builder.Property(m => m.UserId).HasMaxLength(128);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        // A circle has at most one admin.
        builder.HasIndex(m => m.CircleId).IsUnique().HasFilter("\"Role\" = 'Admin'");
    }
}
