namespace LocalDataApi.Domain.Identity;

/// <summary>控制器操作审计记录，不记录认证头、Cookie 或完整请求正文。</summary>
public sealed class OperationLog
{
    public Guid Id { get; set; }
    public DateTimeOffset OperationTimeUtc { get; set; }
    public string? UserId { get; set; }
    public long? PlatformUserId { get; set; }
    public string? UserName { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string ApiPath { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public int DurationMs { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>业务追溯类型(WP05)。取值见 <see cref="BusinessTypes"/>；历史/未关联行可为 NULL。</summary>
    public string? BusinessType { get; set; }

    /// <summary>业务对象字符串化稳定主键(WP05)。与 WP04 Attachment.BusinessId 长度约定一致(nvarchar(64))；可为 NULL。</summary>
    public string? BusinessId { get; set; }
}
