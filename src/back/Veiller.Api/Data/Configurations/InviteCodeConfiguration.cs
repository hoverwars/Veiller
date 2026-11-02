using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data.Configurations;

public sealed class InviteCodeConfiguration : IEntityTypeConfiguration<InviteCode>
{
    public void Configure(EntityTypeBuilder<InviteCode> builder)
    {
        builder.ToTable("invite_codes");
        builder.HasKey(c => c.CircleId);
        builder.Property(c => c.Code).HasMaxLength(8).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasOne<Circle>().WithMany().HasForeignKey(c => c.CircleId).OnDelete(DeleteBehavior.Cascade);
    }
}
