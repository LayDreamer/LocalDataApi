namespace LocalDataApi.Dto;

public abstract class AuditLogQueryDtoBase : PagedRequestDtoBase
{
    public int PageIndex
    {
        get => Page;
        set => Page = value;
    }

    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string? TraceId { get; set; }
}

public sealed class LoginLogQueryDto : AuditLogQueryDtoBase
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public bool? LoginStatus { get; set; }
}

public sealed class OperationLogQueryDto : AuditLogQueryDtoBase
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? ApiPath { get; set; }
    public bool? Success { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessId { get; set; }
}

public sealed class DataChangeLogQueryDto : AuditLogQueryDtoBase
{
    public string? OperatorUserId { get; set; }
    public string? OperatorUserName { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? ChangeType { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessId { get; set; }
}

public sealed class LoginLogListItemDto
{
    public Guid Id { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public DateTimeOffset LoginTimeUtc { get; init; }
    public string LoginType { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? FailReasonCode { get; init; }
    public string? FailReason { get; init; }
    public string? IpAddress { get; init; }
    public string? ClientType { get; init; }
    public string? Device { get; init; }
    public string? TraceId { get; init; }
    public int? DurationMs { get; init; }
}

public sealed class OperationLogListItemDto
{
    public Guid Id { get; init; }
    public DateTimeOffset OperationTimeUtc { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string Module { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string HttpMethod { get; init; } = string.Empty;
    public string ApiPath { get; init; } = string.Empty;
    public string? Parameters { get; init; }
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
    public int DurationMs { get; init; }
    public string TraceId { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? BusinessType { get; init; }
    public string? BusinessId { get; init; }
}

public sealed class DataChangeLogListItemDto
{
    public Guid Id { get; init; }
    public DateTimeOffset ChangeTimeUtc { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
    public string? BeforeData { get; init; }
    public string? AfterData { get; init; }
    public string? ChangedProperties { get; init; }
    public string? OperatorUserId { get; init; }
    public string? OperatorUserName { get; init; }
    public string? TraceId { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? BusinessType { get; init; }
    public string? BusinessId { get; init; }
}
