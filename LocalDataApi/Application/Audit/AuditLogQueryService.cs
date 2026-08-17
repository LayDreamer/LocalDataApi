using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Utils;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Audit;

public sealed class AuditLogQueryService(AppDbContext context) : IAuditLogQueryService
{
    public async Task<PagedResult<LoginLogListItemDto>> QueryLoginLogsAsync(LoginLogQueryDto query, CancellationToken ct = default)
    {
        var source = context.LoginLogs.AsNoTracking();
        if (query.StartTime.HasValue) source = source.Where(x => x.LoginTimeUtc >= query.StartTime.Value);
        if (query.EndTime.HasValue) source = source.Where(x => x.LoginTimeUtc <= query.EndTime.Value);
        if (!string.IsNullOrWhiteSpace(query.UserId)) source = source.Where(x => x.UserId == query.UserId.Trim());
        if (!string.IsNullOrWhiteSpace(query.UserName)) source = source.Where(x => x.UserName != null && x.UserName.Contains(query.UserName.Trim()));
        if (query.LoginStatus.HasValue) source = source.Where(x => x.Success == query.LoginStatus.Value);
        if (!string.IsNullOrWhiteSpace(query.TraceId)) source = source.Where(x => x.TraceId != null && x.TraceId.Contains(query.TraceId.Trim()));
        var total = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(x => x.LoginTimeUtc)
            .Select(x => new LoginLogListItemDto { Id = x.Id, UserId = x.UserId, UserName = x.UserName, LoginTimeUtc = x.LoginTimeUtc, LoginType = x.LoginType, Success = x.Success, FailReasonCode = x.FailReasonCode, FailReason = x.FailReason, IpAddress = x.IpAddress, ClientType = x.ClientType, Device = x.Device, TraceId = x.TraceId, DurationMs = x.DurationMs })
            .ToPageItemsAsync(query, ct);
        return new PagedResult<LoginLogListItemDto> { Items = items, Total = total, Page = query.Page, PageSize = query.PageSize };
    }

    public async Task<PagedResult<OperationLogListItemDto>> QueryOperationLogsAsync(OperationLogQueryDto query, CancellationToken ct = default)
    {
        var source = context.OperationLogs.AsNoTracking();
        if (query.StartTime.HasValue) source = source.Where(x => x.OperationTimeUtc >= query.StartTime.Value);
        if (query.EndTime.HasValue) source = source.Where(x => x.OperationTimeUtc <= query.EndTime.Value);
        if (!string.IsNullOrWhiteSpace(query.UserId)) source = source.Where(x => x.UserId == query.UserId.Trim());
        if (!string.IsNullOrWhiteSpace(query.UserName)) source = source.Where(x => x.UserName != null && x.UserName.Contains(query.UserName.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Module)) source = source.Where(x => x.Module == query.Module.Trim());
        if (!string.IsNullOrWhiteSpace(query.Action)) source = source.Where(x => x.Action.Contains(query.Action.Trim()));
        if (!string.IsNullOrWhiteSpace(query.ApiPath)) source = source.Where(x => x.ApiPath.Contains(query.ApiPath.Trim()));
        if (query.Success.HasValue) source = source.Where(x => x.Success == query.Success.Value);
        if (!string.IsNullOrWhiteSpace(query.TraceId)) source = source.Where(x => x.TraceId.Contains(query.TraceId.Trim()));
        var total = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(x => x.OperationTimeUtc)
            .Select(x => new OperationLogListItemDto { Id = x.Id, OperationTimeUtc = x.OperationTimeUtc, UserId = x.UserId, UserName = x.UserName, Module = x.Module, Action = x.Action, HttpMethod = x.HttpMethod, ApiPath = x.ApiPath, Parameters = x.Parameters, Success = x.Success, StatusCode = x.StatusCode, ExceptionType = x.ExceptionType, ExceptionMessage = x.ExceptionMessage, DurationMs = x.DurationMs, TraceId = x.TraceId, IpAddress = x.IpAddress })
            .ToPageItemsAsync(query, ct);
        return new PagedResult<OperationLogListItemDto> { Items = items, Total = total, Page = query.Page, PageSize = query.PageSize };
    }

    public async Task<PagedResult<DataChangeLogListItemDto>> QueryDataChangeLogsAsync(DataChangeLogQueryDto query, CancellationToken ct = default)
    {
        var source = context.DataChangeLogs.AsNoTracking();
        if (query.StartTime.HasValue) source = source.Where(x => x.ChangeTimeUtc >= query.StartTime.Value);
        if (query.EndTime.HasValue) source = source.Where(x => x.ChangeTimeUtc <= query.EndTime.Value);
        if (!string.IsNullOrWhiteSpace(query.OperatorUserId)) source = source.Where(x => x.OperatorUserId == query.OperatorUserId.Trim());
        if (!string.IsNullOrWhiteSpace(query.OperatorUserName)) source = source.Where(x => x.OperatorUserName != null && x.OperatorUserName.Contains(query.OperatorUserName.Trim()));
        if (!string.IsNullOrWhiteSpace(query.EntityName)) source = source.Where(x => x.EntityName == query.EntityName.Trim());
        if (!string.IsNullOrWhiteSpace(query.EntityId)) source = source.Where(x => x.EntityId == query.EntityId.Trim());
        if (!string.IsNullOrWhiteSpace(query.ChangeType)) source = source.Where(x => x.ChangeType == query.ChangeType.Trim());
        if (!string.IsNullOrWhiteSpace(query.TraceId)) source = source.Where(x => x.TraceId != null && x.TraceId.Contains(query.TraceId.Trim()));
        var total = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(x => x.ChangeTimeUtc)
            .Select(x => new DataChangeLogListItemDto { Id = x.Id, ChangeTimeUtc = x.ChangeTimeUtc, EntityName = x.EntityName, EntityId = x.EntityId, ChangeType = x.ChangeType, BeforeData = x.BeforeData, AfterData = x.AfterData, ChangedProperties = x.ChangedProperties, OperatorUserId = x.OperatorUserId, OperatorUserName = x.OperatorUserName, TraceId = x.TraceId, Source = x.Source })
            .ToPageItemsAsync(query, ct);
        return new PagedResult<DataChangeLogListItemDto> { Items = items, Total = total, Page = query.Page, PageSize = query.PageSize };
    }
}
