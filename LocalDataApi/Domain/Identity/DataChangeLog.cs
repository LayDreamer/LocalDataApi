namespace LocalDataApi.Domain.Identity;

/// <summary>平台主数据的字段级变更审计记录。</summary>
public sealed class DataChangeLog
{
    public Guid Id { get; set; }
    public DateTimeOffset ChangeTimeUtc { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? ChangedProperties { get; set; }
    public string? OperatorUserId { get; set; }
    public long? PlatformUserId { get; set; }
    public string? OperatorUserName { get; set; }
    public string? TraceId { get; set; }
    public string Source { get; set; } = string.Empty;

    /// <summary>业务追溯类型(WP05)。取值见 <see cref="BusinessTypes"/>；历史/未关联行可为 NULL。</summary>
    public string? BusinessType { get; set; }

    /// <summary>业务对象字符串化稳定主键(WP05)；对普通实体通常等于 PK，对 Attachment 取其所属业务 BusinessId；可为 NULL。</summary>
    public string? BusinessId { get; set; }
}
