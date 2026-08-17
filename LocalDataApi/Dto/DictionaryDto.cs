namespace LocalDataApi.Dto;

/// <summary>字典类型传输模型。</summary>
public class DictionaryTypeDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public byte Status { get; set; } = 1;
    public int Sort { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>字典项传输模型。</summary>
public class DictionaryItemDto
{
    public long Id { get; set; }
    public long DictionaryId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Sort { get; set; }
    public byte Status { get; set; } = 1;
}

/// <summary>按字典编码返回的字典数据(含类型信息与项列表)。</summary>
public class DictionaryDataDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<DictionaryItemDto> Items { get; set; } = new();
}

/// <summary>创建字典类型请求。</summary>
public class DictionaryTypeCreateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sort { get; set; }
}

/// <summary>更新字典类型请求。</summary>
public class DictionaryTypeUpdateDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Sort { get; set; }
    public byte? Status { get; set; }
}

/// <summary>创建字典项请求。</summary>
public class DictionaryItemCreateDto
{
    public long DictionaryId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Sort { get; set; }
}

/// <summary>更新字典项请求。</summary>
public class DictionaryItemUpdateDto
{
    public string? Label { get; set; }
    public int? Sort { get; set; }
    public byte? Status { get; set; }
}
