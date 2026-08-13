using System.Diagnostics;
using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Infrastructure.Data;

namespace LocalDataApi.Application.Identity;

public interface ILoginLogService
{
    Task WriteAsync(LoginLogEntry entry, CancellationToken cancellationToken = default);
}

public sealed record LoginLogEntry(
    string? UserId,
    string? UserName,
    string LoginType,
    bool Success,
    string? FailReasonCode = null,
    string? FailReason = null,
    Guid? AuthSessionId = null,
    int? DurationMs = null);

/// <summary>以独立作用域持久化登录日志，日志故障不会改变登录结果。</summary>
public sealed class LoginLogService(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LoginLogService> logger) : ILoginLogService
{
    public async Task WriteAsync(LoginLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = httpContextAccessor.HttpContext;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userAgent = AuditSanitizer.Truncate(context?.Request.Headers.UserAgent.ToString(), 512);
            db.LoginLogs.Add(new LoginLog
            {
                Id = Guid.NewGuid(),
                UserId = AuditSanitizer.Truncate(entry.UserId, 450),
                UserName = AuditSanitizer.Truncate(entry.UserName, 128),
                LoginTimeUtc = DateTimeOffset.UtcNow,
                LoginType = AuditSanitizer.Truncate(entry.LoginType, 32) ?? "Unknown",
                Success = entry.Success,
                FailReasonCode = AuditSanitizer.Truncate(entry.FailReasonCode, 64),
                FailReason = AuditSanitizer.MaskMessage(entry.FailReason, 256),
                IpAddress = AuditSanitizer.Truncate(context?.Connection.RemoteIpAddress?.ToString(), 128),
                UserAgent = userAgent,
                ClientType = GetClientType(context, userAgent),
                Device = GetDevice(userAgent),
                AuthSessionId = entry.AuthSessionId,
                TraceId = AuditSanitizer.Truncate(context?.TraceIdentifier, 64),
                DurationMs = entry.DurationMs
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to write login audit log. TraceId: {TraceId}", httpContextAccessor.HttpContext?.TraceIdentifier);
        }
    }

    private static string GetClientType(HttpContext? context, string? userAgent)
        => context?.Request.Path.StartsWithSegments("/api/Auth/login-by-wechatwork") == true
            ? "WeChatWork"
            : string.IsNullOrWhiteSpace(userAgent) ? "Unknown" : "Web";

    private static string? GetDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)) return "Mobile";
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows";
        if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) return "Mac";
        return "Other";
    }
}
