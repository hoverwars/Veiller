using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data.Configurations;

public sealed class CircleConfiguration : IEntityTypeConfiguration<Circle>
{
    public void Configure(EntityTypeBuilder<Circle> builder)
    {
        builder.ToTable("circles");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.OwnerUserId).HasMaxLength(128).IsRequired();
        builder.Property(c => c.Plan).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(c => c.OwnerUserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Members).WithOne().HasForeignKey(m => m.CircleId).OnDelete(DeleteBehavior.Cascade);
    }
}
