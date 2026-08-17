using LocalDataApi.Dto;

namespace LocalDataApi.Application.Audit;

public interface IAuditLogQueryService
{
    Task<PagedResult<LoginLogListItemDto>> QueryLoginLogsAsync(LoginLogQueryDto query, CancellationToken ct = default);
    Task<PagedResult<OperationLogListItemDto>> QueryOperationLogsAsync(OperationLogQueryDto query, CancellationToken ct = default);
    Task<PagedResult<DataChangeLogListItemDto>> QueryDataChangeLogsAsync(DataChangeLogQueryDto query, CancellationToken ct = default);
}
