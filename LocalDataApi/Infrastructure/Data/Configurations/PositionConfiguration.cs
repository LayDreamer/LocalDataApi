using LocalDataApi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalDataApi.Infrastructure.Data.Configurations;

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Position", "dbo");
        builder.HasKey(position => position.Id);
        builder.Property(position => position.Code).HasMaxLength(64).IsRequired();
        builder.Property(position => position.Name).HasMaxLength(128).IsRequired();
        builder.Property(position => position.Description).HasMaxLength(512);
        builder.Property(position => position.IsActive).HasDefaultValue(true);
        builder.HasIndex(position => position.Code).IsUnique().HasDatabaseName("IX_Position_Code");
    }
}
