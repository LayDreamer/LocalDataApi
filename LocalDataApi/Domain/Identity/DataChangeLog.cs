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
    public string? OperatorUserName { get; set; }
    public string? TraceId { get; set; }
    public string Source { get; set; } = string.Empty;
}
