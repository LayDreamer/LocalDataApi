namespace LocalDataApi.Domain.Identity;

/// <summary>服务端保存的可撤销登录会话，用于 JWT 有效性校验和强制退出。</summary>
public sealed class AuthSession
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastActivityAtUtc { get; set; }
    public DateTimeOffset IdleExpiresAtUtc { get; set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; set; }
    public bool RememberMe { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
