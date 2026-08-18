using System.Collections.Concurrent;
using System.Text.Json;
using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Identity;
using LocalDataApi.Domain.Platform;
using LocalDataApi.Domain.Pmc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LocalDataApi.Infrastructure.Data;

/// <summary>仅记录平台主数据的 EF Core 变更，日志写入使用独立作用域以避免递归。</summary>
public sealed class DataChangeLogInterceptor(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DataChangeLogInterceptor> logger) : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<Guid, List<PendingChange>> _pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PersistAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await PersistAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Remove(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Remove(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null || context is not AppDbContext || _pending.ContainsKey(context.ContextId.InstanceId)) return;
        var candidates = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => GetEntityName(entry.Entity) is not null)
            .Select(CreatePending)
            .Where(change => change is not null)
            .Cast<PendingChange>()
            .ToList();
        if (candidates.Count > 0) _pending.TryAdd(context.ContextId.InstanceId, candidates);
    }

    private static PendingChange? CreatePending(EntityEntry entry)
    {
        var entityName = GetEntityName(entry.Entity);
        if (entityName is null) return null;
        var changed = entry.State == EntityState.Modified
            ? entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).ToArray()
            : entry.Properties.Select(property => property.Metadata.Name).ToArray();
        if (changed.Length == 0) return null;
        var before = entry.State == EntityState.Added ? null : BuildSnapshot(entry, changed, original: true);
        var after = entry.State == EntityState.Deleted ? null : BuildSnapshot(entry, changed, original: false);
        var entityId = entry.Properties.FirstOrDefault(property => property.Metadata.IsPrimaryKey())?.CurrentValue
            ?? entry.Properties.FirstOrDefault(property => property.Metadata.IsPrimaryKey())?.OriginalValue;
        var (businessType, businessId) = ResolveBusinessKeys(entry);
        return new PendingChange(entityName, entityId?.ToString() ?? string.Empty, entry.State.ToString(), before, after, changed, businessType, businessId);
    }

    /// <summary>
    /// 计算业务追溯键(WP05)。普通制造实体取 <see cref="BusinessTypes"/> 常量 + 主键字符串化；
    /// Attachment 取其所属业务对象(与 WP04 约定一致，不使用 Attachment.Id)。
    /// </summary>
    private static (string? BusinessType, string? BusinessId) ResolveBusinessKeys(EntityEntry entry) =>
        ResolveBusinessKeys(entry.Entity, PrimaryKeyValue(entry));

    /// <summary>
    /// 计算业务追溯键(WP05)。普通制造实体取 <see cref="BusinessTypes"/> 常量 + 主键字符串化;
    /// Attachment 取其所属业务对象(与 WP04 约定一致，不使用 Attachment.Id)。internal 以便单元测试。
    /// </summary>
    internal static (string? BusinessType, string? BusinessId) ResolveBusinessKeys(object entity, string? primaryKey) => entity switch
    {
        PMCDeliveryReview => (BusinessTypes.DeliveryReview, primaryKey),
        WorkOrderSalesControl => (BusinessTypes.WorkOrder, primaryKey),
        SchedulingAnalysis => (BusinessTypes.Scheduling, primaryKey),
        Attachment attachment => (attachment.BusinessType, attachment.BusinessId),
        _ => (null, null)
    };

    private static string? PrimaryKeyValue(EntityEntry entry) =>
        entry.Properties.FirstOrDefault(property => property.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

    private static string BuildSnapshot(EntityEntry entry, IEnumerable<string> propertyNames, bool original)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entry.Properties.Where(property => propertyNames.Contains(property.Metadata.Name)))
        {
            if (AuditSanitizer.IsSensitive(property.Metadata.Name)) continue;
            values[property.Metadata.Name] = original ? property.OriginalValue : property.CurrentValue;
        }
        return JsonSerializer.Serialize(values);
    }

    private async Task PersistAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !_pending.TryRemove(context.ContextId.InstanceId, out var changes)) return;
        try
        {
            var http = httpContextAccessor.HttpContext;
            var operatorId = http?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var hasPlatformUserId = long.TryParse(operatorId, out var platformUserId);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var change in changes)
            {
                db.DataChangeLogs.Add(new DataChangeLog
                {
                    Id = Guid.NewGuid(), ChangeTimeUtc = DateTimeOffset.UtcNow,
                    EntityName = change.EntityName, EntityId = AuditSanitizer.Truncate(change.EntityId, 450) ?? string.Empty,
                    ChangeType = change.ChangeType, BeforeData = change.BeforeData, AfterData = change.AfterData,
                    ChangedProperties = JsonSerializer.Serialize(change.ChangedProperties),
                    OperatorUserId = hasPlatformUserId ? null : AuditSanitizer.Truncate(operatorId, 450),
                    PlatformUserId = hasPlatformUserId ? platformUserId : null,
                    OperatorUserName = AuditSanitizer.Truncate(http?.User.Identity?.Name, 128),
                    TraceId = AuditSanitizer.Truncate(http?.TraceIdentifier, 64),
                    Source = http is null ? "System" : "HttpApi",
                    BusinessType = AuditSanitizer.Truncate(change.BusinessType, 64),
                    BusinessId = AuditSanitizer.Truncate(change.BusinessId, 64)
                });
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to write data-change audit logs. TraceId: {TraceId}", httpContextAccessor.HttpContext?.TraceIdentifier);
        }
    }

    private void Remove(DbContext? context)
    {
        if (context is not null) _pending.TryRemove(context.ContextId.InstanceId, out _);
    }

    internal static string? GetEntityName(object entity) => entity switch
    {
        User => "User",
        Role => "Role",
        Permission => "Permission",
        Department => "Department",
        PMCDeliveryReview => "PMCDeliveryReview",
        WorkOrderSalesControl => "WorkOrderSalesControl",
        SchedulingAnalysis => "SchedulingAnalysis",
        Attachment => "Attachment",
        DataChangeLog => null,
        _ => null
    };

    private sealed record PendingChange(string EntityName, string EntityId, string ChangeType,
        string? BeforeData, string? AfterData, IReadOnlyCollection<string> ChangedProperties, string? BusinessType, string? BusinessId);
}
