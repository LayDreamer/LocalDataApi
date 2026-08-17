using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Audit;
using LocalDataApi.Application.Common;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Api.Controllers.Audit;

[ApiController]
[Route("api/audit")]
public sealed class AuditController(IAuditLogQueryService service) : ControllerBase
{
    [HttpGet("login-logs")]
    [HasPermission(PermissionCodes.PlatformLoginLogView)]
    public async Task<ActionResult<ApiResponse<PagedResult<LoginLogListItemDto>>>> Login([FromQuery] LoginLogQueryDto query) => Ok(new ApiResponse<PagedResult<LoginLogListItemDto>> { Success = true, Data = await service.QueryLoginLogsAsync(query, HttpContext.RequestAborted) });

    [HttpGet("operation-logs")]
    [HasPermission(PermissionCodes.PlatformOperationLogView)]
    public async Task<ActionResult<ApiResponse<PagedResult<OperationLogListItemDto>>>> Operation([FromQuery] OperationLogQueryDto query) => Ok(new ApiResponse<PagedResult<OperationLogListItemDto>> { Success = true, Data = await service.QueryOperationLogsAsync(query, HttpContext.RequestAborted) });

    [HttpGet("data-change-logs")]
    [HasPermission(PermissionCodes.PlatformDataChangeLogView)]
    public async Task<ActionResult<ApiResponse<PagedResult<DataChangeLogListItemDto>>>> DataChange([FromQuery] DataChangeLogQueryDto query) => Ok(new ApiResponse<PagedResult<DataChangeLogListItemDto>> { Success = true, Data = await service.QueryDataChangeLogsAsync(query, HttpContext.RequestAborted) });
}
