using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocalDataApi.Api.Controllers.Identity
{
    /// <summary>
    /// 用户管理接口(查询用户 / 分配用户角色)。
    /// </summary>
    [ApiController]
    [Route("api/identity/[controller]")]
    [EnableRateLimiting("DatabaseHeavy")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRoleService _userRoleService;
        private readonly CurrentUserService _currentUser;
        private readonly IUserService _userService;

        public UsersController(IUserRoleService userRoleService, CurrentUserService currentUser, IUserService userService)
        {
            _userRoleService = userRoleService;
            _currentUser = currentUser;
            _userService = userService;
        }

        /// <summary>分页查询用户列表(含角色编码)。</summary>
        [HttpGet]
        [HasPermission(PermissionCodes.UserView)]
        public async Task<ActionResult<ApiResponse<PagedResult<UserListItemDto>>>> GetUsers([FromQuery] UserQueryDto query)
        {
            var result = await _userRoleService.QueryUsersAsync(query, HttpContext.RequestAborted);
            return Ok(new ApiResponse<PagedResult<UserListItemDto>> { Success = true, Data = result });
        }

        /// <summary>查询用户详情(含部门/角色/有效权限)。</summary>
        [HttpGet("{id}")]
        [HasPermission(PermissionCodes.UserView)]
        public async Task<ActionResult<ApiResponse<UserDetailDto>>> GetUser(long id)
        {
            var detail = await _userRoleService.GetUserDetailAsync(id, HttpContext.RequestAborted);
            return Ok(new ApiResponse<UserDetailDto> { Success = true, Data = detail });
        }

        /// <summary>覆盖式分配用户角色(自动刷新权限版本与缓存)。</summary>
        [HttpPut("{id}/roles")]
        [HasPermission(PermissionCodes.UserAssignRole)]
        public async Task<ActionResult<ApiResponse<object>>> AssignRoles(long id, AssignRolesRequestDto dto)
        {
            await _userRoleService.AssignRolesAsync(id, dto, _currentUser.UserId, HttpContext.RequestAborted);
            return Ok(new ApiResponse<object> { Success = true, Message = "角色分配成功" });
        }

        /// <summary>管理员重置用户密码:生成随机临时密码并返回明文(需 UserUpdate 权限)。</summary>
        [HttpPost("{id}/reset-password")]
        [HasPermission(PermissionCodes.UserUpdate)]
        public async Task<ActionResult<ApiResponse<string>>> ResetPassword(long id)
        {
            var (success, message, tempPassword) = await _userService.ResetPasswordAsync(id);
            if (!success)
            {
                return Ok(new ApiResponse<string> { Success = false, Message = message });
            }
            return Ok(new ApiResponse<string> { Success = true, Message = message, Data = tempPassword });
        }
    }
}
