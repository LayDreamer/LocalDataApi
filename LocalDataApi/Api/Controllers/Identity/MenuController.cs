using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;

namespace LocalDataApi.Api.Controllers.Identity;

[ApiController]
[Route("api/platform/menu")]
[EnableRateLimiting("DatabaseHeavy")]
public sealed class MenuController(IMenuService menuService, CurrentUserService currentUser) : ControllerBase
{
    [HttpGet("current")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<CurrentMenuDto>>>> GetCurrent()
    {
        var userId = currentUser.UserId;
        if (!userId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "未登录或登录已失效" });

        var menus = await menuService.GetCurrentUserMenusAsync(userId.Value, HttpContext.RequestAborted);
        return Ok(new ApiResponse<List<CurrentMenuDto>> { Success = true, Data = menus });
    }

    [HttpGet("tree")]
    [HasPermission(PermissionCodes.PlatformMenuView)]
    public async Task<ActionResult<ApiResponse<List<MenuDto>>>> GetTree() => Ok(new ApiResponse<List<MenuDto>> { Success = true, Data = await menuService.GetMenuTreeAsync(HttpContext.RequestAborted) });

    [HttpGet("list")]
    [HasPermission(PermissionCodes.PlatformMenuView)]
    public async Task<ActionResult<ApiResponse<List<MenuDto>>>> GetList() => Ok(new ApiResponse<List<MenuDto>> { Success = true, Data = await menuService.GetMenusAsync(HttpContext.RequestAborted) });

    [HttpPost]
    [HasPermission(PermissionCodes.PlatformMenuCreate)]
    public async Task<ActionResult<ApiResponse<MenuDto>>> Create(MenuCreateDto dto) => Ok(new ApiResponse<MenuDto> { Success = true, Message = "菜单创建成功", Data = await menuService.CreateMenuAsync(dto, currentUser.UserId?.ToString(), HttpContext.RequestAborted) });

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.PlatformMenuUpdate)]
    public async Task<ActionResult<ApiResponse<MenuDto>>> Update(Guid id, MenuUpdateDto dto) => Ok(new ApiResponse<MenuDto> { Success = true, Message = "菜单修改成功", Data = await menuService.UpdateMenuAsync(id, dto, currentUser.UserId?.ToString(), HttpContext.RequestAborted) });

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionCodes.PlatformMenuDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        await menuService.DeleteMenuAsync(id, currentUser.UserId?.ToString(), HttpContext.RequestAborted);
        return Ok(new ApiResponse<object> { Success = true, Message = "菜单删除成功" });
    }
}
