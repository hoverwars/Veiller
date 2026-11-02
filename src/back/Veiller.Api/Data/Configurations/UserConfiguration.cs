using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veiller.Api.Data.Entities;

namespace Veiller.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasMaxLength(128);
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.Phone).HasMaxLength(30);
        builder.Property(u => u.Relation).HasMaxLength(50);
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
