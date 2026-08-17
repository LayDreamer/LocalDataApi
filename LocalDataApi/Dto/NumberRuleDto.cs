namespace LocalDataApi.Dto;

/// <summary>编码规则传输模型(管理列表/详情)。</summary>
public class NumberRuleDto
{
    public long Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? DateFormat { get; set; }
    public int SequenceLength { get; set; } = 5;
    public long CurrentSequence { get; set; }
    public int PeriodType { get; set; }
    public DateTime? LastResetDate { get; set; }
    public byte Status { get; set; } = 1;
    public string? Description { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? UpdateTime { get; set; }
}

/// <summary>创建编码规则请求。</summary>
public class NumberRuleCreateDto
{
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? DateFormat { get; set; }
    public int SequenceLength { get; set; } = 5;
    public int PeriodType { get; set; }
    public string? Description { get; set; }
}

/// <summary>更新编码规则请求(仅更新传入字段)。</summary>
public class NumberRuleUpdateDto
{
    public string? RuleName { get; set; }
    public string? Prefix { get; set; }
    public string? DateFormat { get; set; }
    public int? SequenceLength { get; set; }
    public int? PeriodType { get; set; }
    public byte? Status { get; set; }
    public string? Description { get; set; }
}

/// <summary>手动重置流水号请求。</summary>
public class NumberRuleResetDto
{
    /// <summary>重置后的起始计数(默认 0,下一次生成即为 1)</summary>
    public long StartFrom { get; set; }
}
