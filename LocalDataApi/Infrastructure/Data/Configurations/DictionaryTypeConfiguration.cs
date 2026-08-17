using LocalDataApi.Domain.Dictionary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalDataApi.Infrastructure.Data.Configurations;

/// <summary>数据字典类型表映射(sys_dictionary_type)。</summary>
public sealed class DictionaryTypeConfiguration : IEntityTypeConfiguration<DictionaryType>
{
    public void Configure(EntityTypeBuilder<DictionaryType> builder)
    {
        builder.ToTable("sys_dictionary_type", "dbo");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(512);
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UX_sys_dictionary_type_Code");
        builder.HasMany(entity => entity.Items)
            .WithOne(item => item.Dictionary)
            .HasForeignKey(item => item.DictionaryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
