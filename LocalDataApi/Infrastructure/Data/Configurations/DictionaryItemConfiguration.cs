using LocalDataApi.Domain.Dictionary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocalDataApi.Infrastructure.Data.Configurations;

/// <summary>数据字典项表映射(sys_dictionary_item)。</summary>
public sealed class DictionaryItemConfiguration : IEntityTypeConfiguration<DictionaryItem>
{
    public void Configure(EntityTypeBuilder<DictionaryItem> builder)
    {
        builder.ToTable("sys_dictionary_item", "dbo");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Value).HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Label).HasMaxLength(128).IsRequired();
        builder.HasIndex(entity => new { entity.DictionaryId, entity.Value })
            .IsUnique()
            .HasDatabaseName("UX_sys_dictionary_item_DictionaryId_Value");
    }
}
