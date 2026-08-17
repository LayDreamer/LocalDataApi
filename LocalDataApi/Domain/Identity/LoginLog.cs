namespace LocalDataApi.Domain.Identity;

/// <summary>登录尝试审计记录。成功与失败均保留，不承载令牌或认证凭据。</summary>
public sealed class LoginLog
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public long? PlatformUserId { get; set; }
    public string? UserName { get; set; }
    public DateTimeOffset LoginTimeUtc { get; set; }
    public string LoginType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? FailReasonCode { get; set; }
    public string? FailReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ClientType { get; set; }
    public string? Device { get; set; }
    public Guid? AuthSessionId { get; set; }
    public string? TraceId { get; set; }
    public int? DurationMs { get; set; }
}
