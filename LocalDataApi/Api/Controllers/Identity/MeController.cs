using LocalDataApi.Application.Common;
using LocalDataApi.Application.Identity;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;

namespace LocalDataApi.Api.Controllers.Identity
{
    /// <summary>
    /// 当前用户信息接口(登录态用户查询自身角色与权限)。
    /// </summary>
    [ApiController]
[Route("api/identity/[controller]")]
[Authorize]
[EnableRateLimiting("DatabaseHeavy")]
    public class MeController : ControllerBase
    {
        private readonly IUserRoleService _userRoleService;
        private readonly CurrentUserService _currentUser;

        public MeController(IUserRoleService userRoleService, CurrentUserService currentUser)
        {
            _userRoleService = userRoleService;
            _currentUser = currentUser;
        }

        /// <summary>获取当前用户信息(含角色与权限)。</summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<MeResultDto>>> Me()
        {
            var userId = _currentUser.UserId;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "未登录或登录已失效" });

            var me = await _userRoleService.GetCurrentUserInfoAsync(userId);
            if (me == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "用户不存在" });

            return Ok(new ApiResponse<MeResultDto> { Success = true, Data = me });
        }

        /// <summary>获取当前用户角色与权限编码。</summary>
        [HttpGet("permissions")]
        public async Task<ActionResult<ApiResponse<MePermissionsDto>>> Permissions()
        {
            var userId = _currentUser.UserId;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "未登录或登录已失效" });

            var me = await _userRoleService.GetCurrentUserInfoAsync(userId);
            if (me == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "用户不存在" });

            return Ok(new ApiResponse<MePermissionsDto>
            {
                Success = true,
                Data = new MePermissionsDto { Roles = me.Roles, Permissions = me.Permissions }
            });
        }
    }
}
