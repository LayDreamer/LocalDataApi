using LocalDataApi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalDataApi.Infrastructure.Data.Configurations;

public sealed class MenuPermissionConfiguration : IEntityTypeConfiguration<MenuPermission>
{
    public void Configure(EntityTypeBuilder<MenuPermission> builder)
    {
        builder.ToTable("Sys_MenuPermission");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.PermissionCode).HasMaxLength(128).IsRequired();
        builder.HasIndex(permission => new { permission.MenuId, permission.PermissionCode }).IsUnique();
        builder.HasIndex(permission => permission.PermissionCode);
        builder.HasOne(permission => permission.Menu)
            .WithMany(menu => menu.Permissions)
            .HasForeignKey(permission => permission.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
