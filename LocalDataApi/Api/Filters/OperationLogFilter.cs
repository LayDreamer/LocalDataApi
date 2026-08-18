using System.Diagnostics;
using System.Reflection;
using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Audit;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Domain.Platform;
using LocalDataApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LocalDataApi.Api.Filters;

/// <summary>统一记录 MVC 控制器操作；日志失败不影响原接口响应。WP05 通过集中映射器写入 BusinessType/BusinessId。</summary>
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

        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var spec = descriptor is not null ? BusinessTraceResolver.Resolve(descriptor.ControllerName, descriptor.ActionName) : null;

        // AttachmentLookup 场景需在 Action 删除记录前预读取其所属业务键。
        (string? BusinessType, string? BusinessId)? preResolved = null;
        if (spec is not null && spec.Source == BusinessIdSource.AttachmentLookup && spec.ArgumentName is not null
            && context.ActionArguments.TryGetValue(spec.ArgumentName, out var idObj) && idObj is long attachmentId)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var attachment = await db.Attachments.FindAsync(attachmentId);
                if (attachment is not null) preResolved = (attachment.BusinessType, attachment.BusinessId);
            }
            catch (Exception lookupException)
            {
                logger.LogWarning(lookupException, "BusinessTrace: failed to pre-lookup Attachment {Id}", attachmentId);
            }
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
            await WriteAsync(context, startedAt, stopwatch.ElapsedMilliseconds, executed, exception, spec, preResolved);
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
        ActionExecutedContext? executed, Exception? exception, BusinessTraceSpec? spec,
        (string? BusinessType, string? BusinessId)? preResolved)
    {
        try
        {
            var traces = ResolveTrace(context, executed, spec, preResolved);
            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var http = context.HttpContext;
            var statusCode = executed?.HttpContext.Response.StatusCode ?? http.Response.StatusCode;
            var path = descriptor?.AttributeRouteInfo?.Template is { Length: > 0 } route ? $"/{route}" : http.Request.Path.Value ?? string.Empty;
            var parameters = BuildParameters(context.ActionArguments);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // 批量操作(Collection)对每个业务对象各写一行,使每个业务对象均可被 OperationLog 独立追踪
            foreach (var (businessType, businessId) in traces)
            {
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
                    UserAgent = AuditSanitizer.Truncate(http.Request.Headers.UserAgent.ToString(), 512),
                    BusinessType = AuditSanitizer.Truncate(businessType, 64),
                    BusinessId = AuditSanitizer.Truncate(businessId, 64)
                });
            }
            await db.SaveChangesAsync(http.RequestAborted);
        }
        catch (Exception writeException)
        {
            logger.LogError(writeException, "Failed to write operation audit log. TraceId: {TraceId}", context.HttpContext.TraceIdentifier);
        }
    }

    /// <summary>解析本 Action 应写入的 (BusinessType, BusinessId) 集合:单对象为 1 行,批量(Collection)为每业务对象 1 行。</summary>
    private static List<(string? BusinessType, string? BusinessId)> ResolveTrace(
        ActionExecutingContext context, ActionExecutedContext? executed, BusinessTraceSpec? spec,
        (string? BusinessType, string? BusinessId)? preResolved)
    {
        var traces = new List<(string? BusinessType, string? BusinessId)>();
        if (spec is null) return traces;
        var businessType = ResolveBusinessType(context, spec);
        switch (spec.Source)
        {
            case BusinessIdSource.AttachmentLookup:
                traces.Add(preResolved ?? (null, null));
                break;
            case BusinessIdSource.Argument:
                traces.Add((businessType, ExtractFromArgument(context, spec)));
                break;
            case BusinessIdSource.Result:
                traces.Add((businessType, ExtractFromResult(executed, spec)));
                break;
            case BusinessIdSource.Collection:
                var ids = ExtractBusinessIdsForCollection(context, spec).ToList();
                if (ids.Count == 0)
                    traces.Add((businessType, null)); // 空批量仍记录一次操作(业务键为空)
                else
                    traces.AddRange(ids.Select(id => (businessType, id)));
                break;
            default:
                traces.Add((businessType, null));
                break;
        }
        return traces;
    }

    /// <summary>批量操作:遍历入参集合,按 <see cref="BusinessTraceSpec.PropertyName"/> 提取每个业务对象的主键(元素为 string 时取自身)。</summary>
    internal static IEnumerable<string?> ExtractBusinessIdsForCollection(ActionExecutingContext context, BusinessTraceSpec spec)
    {
        if (spec.ArgumentName is null) yield break;
        if (!context.ActionArguments.TryGetValue(spec.ArgumentName, out var arg) || arg is null) yield break;
        if (arg is not System.Collections.IEnumerable enumerable || arg is string) yield break;
        foreach (var item in enumerable)
            yield return ReadValue(item, spec.PropertyName);
    }

    private static string? ResolveBusinessType(ActionExecutingContext context, BusinessTraceSpec spec)
    {
        if (spec.BusinessTypeArgument is not null
            && context.ActionArguments.TryGetValue(spec.BusinessTypeArgument, out var argValue))
            return argValue?.ToString();
        return spec.BusinessType;
    }

    private static string? ExtractFromArgument(ActionExecutingContext context, BusinessTraceSpec spec)
    {
        if (spec.ArgumentName is null) return null;
        return context.ActionArguments.TryGetValue(spec.ArgumentName, out var arg) ? ReadValue(arg, spec.PropertyName) : null;
    }

    private static string? ExtractFromResult(ActionExecutedContext? executed, BusinessTraceSpec spec)
    {
        object? result = executed?.Result;
        if (result is ObjectResult objectResult) result = objectResult.Value;
        if (result is null) return null;
        // 解包 ApiResponse<T>.Data
        var dataProp = result.GetType().GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
        if (dataProp is not null) result = dataProp.GetValue(result);
        return ReadValue(result, spec.PropertyName);
    }

    private static string? ReadValue(object? value, string? propertyName)
    {
        if (value is null) return null;
        if (propertyName is null)
        {
            if (value is System.Collections.IEnumerable enumerable && value is not string)
                return enumerable.Cast<object?>().FirstOrDefault()?.ToString();
            return value.ToString();
        }

        if (value is System.Collections.IEnumerable enumerable2 && value is not string)
        {
            var first = enumerable2.Cast<object?>().FirstOrDefault();
            return ReadProperty(first, propertyName);
        }

        return ReadProperty(value, propertyName);
    }

    private static string? ReadProperty(object? value, string propertyName)
    {
        if (value is null) return null;
        var prop = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(value)?.ToString();
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
