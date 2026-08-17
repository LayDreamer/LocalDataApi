namespace LocalDataApi.Domain.Dictionary;

/// <summary>
/// 数据字典项(表 sys_dictionary_item)。
/// 归属于某个字典类型,Value 为存储值、Label 为展示名称。
/// </summary>
public sealed class DictionaryItem
{
    /// <summary>主键(bigint 自增)</summary>
    public long Id { get; set; }

    /// <summary>所属字典类型 ID(外键 sys_dictionary_type.Id)</summary>
    public long DictionaryId { get; set; }

    /// <summary>值(存储用,如 10)</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示名称(如 新建)</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>排序(同级升序)</summary>
    public int Sort { get; set; }

    /// <summary>状态:1=启用,0=停用</summary>
    public byte Status { get; set; } = 1;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    public DictionaryType Dictionary { get; set; } = null!;
}
