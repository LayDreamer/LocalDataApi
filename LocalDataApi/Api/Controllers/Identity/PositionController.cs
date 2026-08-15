using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Identity;

[ApiController]
[Route("api/identity/positions")]
[EnableRateLimiting("DatabaseHeavy")]
public sealed class PositionController(IPositionService positionService, CurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.PlatformPositionView)]
    public async Task<ActionResult<ApiResponse<List<PositionDto>>>> GetPositions() => Ok(new ApiResponse<List<PositionDto>>
    {
        Success = true,
        Data = await positionService.GetPositionsAsync(HttpContext.RequestAborted)
    });

    [HttpPost]
    [HasPermission(PermissionCodes.PlatformPositionCreate)]
    public async Task<ActionResult<ApiResponse<PositionDto>>> Create(PositionCreateDto dto) => Ok(new ApiResponse<PositionDto>
    {
        Success = true,
        Message = "岗位创建成功",
        Data = await positionService.CreatePositionAsync(dto, currentUser.UserId?.ToString(), HttpContext.RequestAborted)
    });

    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.PlatformPositionEdit)]
    public async Task<ActionResult<ApiResponse<PositionDto>>> Update(long id, PositionUpdateDto dto) => Ok(new ApiResponse<PositionDto>
    {
        Success = true,
        Message = "岗位修改成功",
        Data = await positionService.UpdatePositionAsync(id, dto, currentUser.UserId?.ToString(), HttpContext.RequestAborted)
    });

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionCodes.PlatformPositionDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id)
    {
        await positionService.DisablePositionAsync(id, currentUser.UserId?.ToString(), HttpContext.RequestAborted);
        return Ok(new ApiResponse<object> { Success = true, Message = "岗位已停用" });
    }
}
