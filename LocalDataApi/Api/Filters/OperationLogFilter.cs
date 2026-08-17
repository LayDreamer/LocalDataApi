using System.Diagnostics;
using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LocalDataApi.Api.Filters;

/// <summary>统一记录 MVC 控制器操作；日志失败不影响原接口响应。</summary>
public sealed class OperationLogFilter(
    IServiceScopeFactory scopeFactory,
    CurrentUserService currentUser,
    ILogger<OperationLogFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (ShouldSkip(context))
        {
            await next();
            return;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;
        ActionExecutedContext? executed = null;
        try
        {
            executed = await next();
            exception = executed.Exception;
        }
        finally
        {
            stopwatch.Stop();
            await WriteAsync(context, startedAt, stopwatch.ElapsedMilliseconds, executed, exception);
        }
    }

    private static bool ShouldSkip(ActionExecutingContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<NoOperationLogAttribute>().Any()) return true;
        var path = context.HttpContext.Request.Path;
        return HttpMethods.IsOptions(context.HttpContext.Request.Method)
            || path.StartsWithSegments("/swagger")
            || path.StartsWithSegments("/openapi")
            || path.StartsWithSegments("/api/Auth/login")
            || path.StartsWithSegments("/api/Auth/login-by-wechatwork");
    }

    private async Task WriteAsync(ActionExecutingContext context, DateTimeOffset startedAt, long durationMs,
        ActionExecutedContext? executed, Exception? exception)
    {
        try
        {
            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var http = context.HttpContext;
            var statusCode = executed?.HttpContext.Response.StatusCode ?? http.Response.StatusCode;
            var path = descriptor?.AttributeRouteInfo?.Template is { Length: > 0 } route ? $"/{route}" : http.Request.Path.Value ?? string.Empty;
            var parameters = BuildParameters(context.ActionArguments);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OperationLogs.Add(new OperationLog
            {
                Id = Guid.NewGuid(),
                OperationTimeUtc = startedAt,
                UserId = null,
                PlatformUserId = currentUser.UserId,
                UserName = AuditSanitizer.Truncate(currentUser.UserName, 128),
                Module = AuditSanitizer.Truncate(descriptor?.ControllerName, 64) ?? "Unknown",
                Action = AuditSanitizer.Truncate(descriptor?.ActionName, 128) ?? "Unknown",
                HttpMethod = http.Request.Method,
                ApiPath = AuditSanitizer.Truncate(path, 256) ?? string.Empty,
                Parameters = parameters,
                Success = exception is null && statusCode < StatusCodes.Status400BadRequest,
                StatusCode = statusCode,
                ExceptionType = exception?.GetType().FullName,
                ExceptionMessage = AuditSanitizer.MaskMessage(exception?.Message),
                DurationMs = (int)Math.Min(durationMs, int.MaxValue),
                TraceId = AuditSanitizer.Truncate(http.TraceIdentifier, 64) ?? string.Empty,
                IpAddress = AuditSanitizer.Truncate(http.Connection.RemoteIpAddress?.ToString(), 128),
                UserAgent = AuditSanitizer.Truncate(http.Request.Headers.UserAgent.ToString(), 512)
            });
            await db.SaveChangesAsync(http.RequestAborted);
        }
        catch (Exception writeException)
        {
            logger.LogError(writeException, "Failed to write operation audit log. TraceId: {TraceId}", context.HttpContext.TraceIdentifier);
        }
    }

    private static string? BuildParameters(IDictionary<string, object?> values)
    {
        if (values.Count == 0) return null;
        var summary = values.ToDictionary(
            pair => pair.Key,
            pair => pair.Key.Contains("password", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Contains("token", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Contains("code", StringComparison.OrdinalIgnoreCase)
                ? "***"
                : pair.Value is null ? null : pair.Value.GetType().IsPrimitive || pair.Value is string || pair.Value is Guid
                    ? pair.Value : $"<{pair.Value.GetType().Name}>");
        return AuditSanitizer.Serialize(summary);
    }
}
