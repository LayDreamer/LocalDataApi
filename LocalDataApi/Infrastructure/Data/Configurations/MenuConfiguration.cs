using LocalDataApi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalDataApi.Infrastructure.Data.Configurations;

public sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Sys_Menu");
        builder.HasKey(menu => menu.Id);
        builder.Property(menu => menu.Name).HasMaxLength(128).IsRequired();
        builder.Property(menu => menu.Path).HasMaxLength(256);
        builder.Property(menu => menu.Component).HasMaxLength(256);
        builder.Property(menu => menu.Icon).HasMaxLength(128);
        builder.Property(menu => menu.Type).HasMaxLength(32).IsRequired();
        builder.HasIndex(menu => menu.ParentId);
        builder.HasIndex(menu => new { menu.ParentId, menu.Sort });
        builder.HasIndex(menu => menu.Path);
        builder.HasOne(menu => menu.Parent)
            .WithMany(menu => menu.Children)
            .HasForeignKey(menu => menu.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
