using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data.Configurations;

public sealed class JoinRequestConfiguration : IEntityTypeConfiguration<JoinRequest>
{
    public void Configure(EntityTypeBuilder<JoinRequest> builder)
    {
        builder.ToTable("join_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).HasMaxLength(128).IsRequired();
        // No duplicate request for the same user and circle.
        builder.HasIndex(r => new { r.CircleId, r.UserId }).IsUnique();
        builder.HasOne<Circle>().WithMany().HasForeignKey(r => r.CircleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
