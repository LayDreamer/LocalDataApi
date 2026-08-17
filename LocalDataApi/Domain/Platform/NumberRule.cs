namespace LocalDataApi.Domain.Platform;

/// <summary>
/// 统一业务编码规则(表 Sys_NumberRule)。
/// 支撑销售订单号/工单号/计划号/入库单号/出库单号等制造单据编号的统一生成，
/// 避免各业务模块自行生成单据号。
/// </summary>
public sealed class NumberRule
{
    /// <summary>主键(bigint 自增)</summary>
    public long Id { get; set; }

    /// <summary>规则编码(唯一,业务引用标识,如 SalesOrder / WorkOrder / DeliveryReview)</summary>
    public string RuleCode { get; set; } = string.Empty;

    /// <summary>规则名称(展示用)</summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>前缀(可空,如 "SO-")</summary>
    public string? Prefix { get; set; }

    /// <summary>日期格式(可空,如 "yyyyMMdd";为空则不输出日期段)</summary>
    public string? DateFormat { get; set; }

    /// <summary>流水号长度(右对齐补零,如 5 → 00001)</summary>
    public int SequenceLength { get; set; } = 5;

    /// <summary>当前流水号(自增计数,从 0 起;生成时先 +1)</summary>
    public long CurrentSequence { get; set; }

    /// <summary>重置周期:0=不重置 / 1=按日 / 2=按月 / 3=按年</summary>
    public int PeriodType { get; set; }

    /// <summary>上次重置日期(跨周期时流水号归零)</summary>
    public DateTime? LastResetDate { get; set; }

    /// <summary>状态:1=启用,0=停用(停用后不可再生成)</summary>
    public byte Status { get; set; } = 1;

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>更新时间</summary>
    public DateTime? UpdateTime { get; set; }
}
