using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Identity;

[ApiController]
[Route("api/identity/employees")]
[EnableRateLimiting("DatabaseHeavy")]
public sealed class EmployeeAccountController(IEmployeeAccountService employeeAccountService, CurrentUserService currentUser) : ControllerBase
{
    [HttpPost("{id:long}/bind-user")]
    [HasPermission(PermissionCodes.EmployeeBindUser)]
    public async Task<ActionResult<ApiResponse<EmployeeAccountDto>>> BindUser(long id, BindEmployeeUserRequestDto dto) => Ok(new ApiResponse<EmployeeAccountDto>
    {
        Success = true,
        Message = "员工账号绑定成功",
        Data = await employeeAccountService.BindUserAsync(id, dto, currentUser.UserId, HttpContext.RequestAborted)
    });

    [HttpPost("{id:long}/unbind-user")]
    [HasPermission(PermissionCodes.EmployeeBindUser)]
    public async Task<ActionResult<ApiResponse<EmployeeAccountDto>>> UnbindUser(long id) => Ok(new ApiResponse<EmployeeAccountDto>
    {
        Success = true,
        Message = "员工账号解绑成功",
        Data = await employeeAccountService.UnbindUserAsync(id, currentUser.UserId, HttpContext.RequestAborted)
    });

    [HttpGet("{id:long}/account")]
    [HasPermission(PermissionCodes.UserView)]
    public async Task<ActionResult<ApiResponse<EmployeeAccountDto>>> GetAccount(long id) => Ok(new ApiResponse<EmployeeAccountDto>
    {
        Success = true,
        Data = await employeeAccountService.GetAccountAsync(id, HttpContext.RequestAborted)
    });
}
