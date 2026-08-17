namespace LocalDataApi.Domain.Dictionary;

/// <summary>
/// 数据字典类型(表 sys_dictionary_type)。
/// 统一管理业务状态/类型/分类的下拉选项来源,避免前后端硬编码。
/// </summary>
public sealed class DictionaryType
{
    /// <summary>主键(bigint 自增)</summary>
    public long Id { get; set; }

    /// <summary>字典编码(唯一,如 OrderStatus / ManufactureType)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>字典名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>状态:1=启用,0=停用</summary>
    public byte Status { get; set; } = 1;

    /// <summary>排序(同级升序)</summary>
    public int Sort { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    public ICollection<DictionaryItem> Items { get; set; } = new List<DictionaryItem>();
}
